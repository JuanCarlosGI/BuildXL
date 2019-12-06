using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using BuildXL.Cache.ContentStore.Hashing;
using BuildXL.Cache.ContentStore.Interfaces.Time;
using BuildXL.Utilities.Collections;

#nullable enable

namespace BuildXL.Cache.ContentStore.Distributed.NuCache.Binning
{
    internal class BinManager
    {
        public const int NumberOfBins = 1 << 16;

        public int LocationsPerBin { get; }

        private readonly List<Bin> _bins;
        private readonly IClock _clock;
        private readonly SortedSet<LocationWithBinAssignments> _locationsByAssignmentCount;
        private readonly Dictionary<MachineLocation, LocationWithBinAssignments> _locationToAssignments = new Dictionary<MachineLocation, LocationWithBinAssignments>();

        public BinManager(int locationsPerBin, IEnumerable<MachineLocation> startLocatons, IClock clock)
        {
            _bins = new List<Bin>(NumberOfBins);
            LocationsPerBin = locationsPerBin;
            _clock = clock;

            _locationsByAssignmentCount = new SortedSet<LocationWithBinAssignments>(startLocatons.Select(i => new LocationWithBinAssignments(i)), new ItemComparer());
            foreach (var locationWithBinAssignments in _locationsByAssignmentCount)
            {
                _locationToAssignments[locationWithBinAssignments.Location] = locationWithBinAssignments;
            }

            Initialize();
        }

        private void Initialize()
        {
            Contract.AssertNotNull(_locationsByAssignmentCount);

            for (var i = 0; i < NumberOfBins; i++)
            {
                _bins.Add(new Bin(manager: this, _clock));
            }

            if (_locationsByAssignmentCount.Count <= LocationsPerBin)
            {
                foreach (var bin in _bins)
                {
                    foreach (var locationWithBinAssignments in _locationsByAssignmentCount.ToArray())
                    {
                        bin.AddLocation(locationWithBinAssignments);
                    }
                }
            }
            else
            {
                foreach (var bin in _bins)
                {
                    while (bin.ValidLocationCount < LocationsPerBin)
                    {
                        var minLocation = _locationsByAssignmentCount.Min!;
                        bin.AddLocation(minLocation);
                    }
                }
            }
        }

        public void AddLocation(MachineLocation item)
        {
            var locationWithBinAssignments = _locationToAssignments.GetOrAdd(item, i => new LocationWithBinAssignments(item));
            Contract.Assert(locationWithBinAssignments.BinAssignments.All(e => e.ExpiryTime == null), "Adding locations twice before removing is not supported.");

            _locationsByAssignmentCount.Add(locationWithBinAssignments);

            // Make sure to fill bins which could have had remained unfilled during initialization.
            foreach (var bin in _bins.Where(b => b.ValidLocationCount < LocationsPerBin))
            {
                bin.AddLocation(locationWithBinAssignments);
            }

            // Optimization for when there are a small amount of machines.
            // Adding locations for this case was extremely slow.
            // Instead of looking through all assigned bins, all bins will keep track of which bins the location we're currently adding has been assigned to.
            Dictionary<LocationWithBinAssignments, HashSet<Bin>>? availableBinsDictionary = null;
            if (locationWithBinAssignments.ValidAssignmentCount == 0 && _locationsByAssignmentCount.Max!.ValidAssignmentCount > _locationsByAssignmentCount.Count)
            {
                availableBinsDictionary = new Dictionary<LocationWithBinAssignments, HashSet<Bin>>();
                foreach (var location in _locationsByAssignmentCount.Where(location => location != locationWithBinAssignments))
                {
                    availableBinsDictionary[location] = new HashSet<Bin>(location.BinsAssignedTo);
                }
            }

            // Balance bins by taking bins from items with most assigned bins.
            while (locationWithBinAssignments.ValidAssignmentCount < _locationsByAssignmentCount.Max!.ValidAssignmentCount - 1)
            {
                var max = _locationsByAssignmentCount.Max!;

                BinAssignment assignmentToReplace;

                if (availableBinsDictionary == null)
                {
                    // First assignment which isn't set to expire or is in a bin that contains the item we want to add.
                    assignmentToReplace = max.BinsAssignedTo
                        .Except(locationWithBinAssignments.BinsAssignedTo)
                        .Select(bin => bin.Assignments.First(assignment => assignment.ExpiryTime == null && assignment.LocationWithAssignments == max)).First();
                }
                else
                {
                    var binToReplace = availableBinsDictionary[max].First();
                    assignmentToReplace = binToReplace.Assignments.First(a => a.LocationWithAssignments == max);

                    foreach (var other in availableBinsDictionary)
                    {
                        other.Value.Remove(binToReplace);
                    }
                }

                assignmentToReplace.Bin.ReplaceAssignment(assignmentToReplace, newLocation: locationWithBinAssignments);
            }
        }

        public void RemoveLocation(MachineLocation item)
        {
            var locationWithMappings = _locationToAssignments[item];
            _locationsByAssignmentCount.Remove(locationWithMappings);

            // Replace assignments with least used locations.
            foreach (var assignment in locationWithMappings.BinAssignments.Where(e => e.ExpiryTime == null))
            {
                var min = GetLeastUsedValidLocation(forBin: assignment.Bin);

                if (min != null)
                {
                    assignment.Bin.ReplaceAssignment(assignment, newLocation: min);
                }
                else
                {
                    // No item available for replace. Just remove.
                    assignment.Bin.Expire(assignment, _clock.UtcNow); // FIX
                }
            }
        }

        /// <summary>
        /// We need to make sure that the location that we select is not already in the bin we're trying to place it in.
        /// </summary>
        private LocationWithBinAssignments? GetLeastUsedValidLocation(Bin forBin)
        {
            var stashed = new List<LocationWithBinAssignments>();
            try
            {
                while (_locationsByAssignmentCount.Count > 0)
                {
                    var next = _locationsByAssignmentCount.Min!;
                    if (forBin.Assignments.Any(assignment => assignment.Location.Path == next.Location.Path))
                    {
                        _locationsByAssignmentCount.Remove(next);
                        stashed.Add(next);
                    }
                    else
                    {
                        return next;
                    }
                }
            }
            finally
            {
                foreach (var stashedItem in stashed)
                {
                    _locationsByAssignmentCount.Add(stashedItem);
                }
            }

            // No valid location found.
            return null;
        }

        public IReadOnlyList<MachineLocation> GetLocations(ContentHash hash)
        {
            var index = hash[0] | hash[1] << 8;
            return _bins[index].Assignments.Select(e => e.Location).ToArray();
        }

        internal MachineLocation[][] GetBins() => _bins.Select(bin => bin.Assignments.Where(a => a.ExpiryTime == null).Select(assignment => assignment.Location).ToArray()).ToArray();

        // Not sure if the class should manage itself or if it should receive an external signal to prune.
        public void Prune()
        {
            foreach (var bin in _bins)
            {
                bin.Prune();
            }

            var itemsToRemove = new List<LocationWithBinAssignments>();
            foreach (var locationWithBinAssignments in _locationsByAssignmentCount)
            {
                if (locationWithBinAssignments.BinAssignments.Count(assignment => assignment.ExpiryTime == null || assignment.ExpiryTime > _clock.UtcNow) == 0)
                {
                    itemsToRemove.Add(locationWithBinAssignments);
                }
            }

            foreach (var item in itemsToRemove)
            {
                _locationsByAssignmentCount.Remove(item);
                _locationToAssignments.Remove(item.Location);
            }
        }

        internal void Mutate(LocationWithBinAssignments location, Action<LocationWithBinAssignments> action)
        {
            var removed = _locationsByAssignmentCount.Remove(location);

            action(location);

            if (removed)
            {
                _locationsByAssignmentCount.Add(location);
            }
        }

        private class ItemComparer : IComparer<LocationWithBinAssignments>
        {
            public int Compare(LocationWithBinAssignments x, LocationWithBinAssignments y)
            {
                return x.ValidAssignmentCount != y.ValidAssignmentCount
                    ? x.ValidAssignmentCount.CompareTo(y.ValidAssignmentCount)
                    : x.Location.Path.CompareTo(y.Location.Path);
            }
        }
    }
}
