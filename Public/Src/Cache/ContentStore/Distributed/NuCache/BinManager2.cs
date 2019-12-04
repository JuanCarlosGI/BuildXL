using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using BuildXL.Cache.ContentStore.Hashing;
using BuildXL.Cache.ContentStore.Interfaces.Time;
using BuildXL.Utilities.Collections;

#nullable enable

namespace BuildXL.Cache.ContentStore.Distributed.NuCache
{
    internal class BinManager2
    {
        public const int NumberOfBins = 1 << 16;

        private readonly Bin[] _bins;
        private readonly int _locationsPerBin;
        private readonly IClock _clock;
        private readonly SortedSet<LocationWithBinAssignments> _locationsByAssignmentCount;
        private readonly Dictionary<MachineLocation, LocationWithBinAssignments> _locationToAssignments = new Dictionary<MachineLocation, LocationWithBinAssignments>();

        public BinManager2(int locationsPerBin, IEnumerable<MachineLocation> startLocatons, IClock clock)
        {
            _bins = new Bin[NumberOfBins];
            _locationsPerBin = locationsPerBin;
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
            for (var i = 0; i < _bins.Length; i++)
            {
                _bins[i] = new Bin(_locationsByAssignmentCount, _clock);
            }

            if (_locationsByAssignmentCount.Count <= _locationsPerBin)
            {
                foreach (var bin in _bins)
                {
                    foreach (var locationWithBinAssignments in _locationsByAssignmentCount)
                    {
                        bin.AddLocation(locationWithBinAssignments);
                    }
                }
            }
            else
            {
                foreach (var bin in _bins)
                {
                    while (bin.Count < _locationsPerBin)
                    {
                        var minLocation = _locationsByAssignmentCount.Min;
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
            foreach (var bin in _bins.Where(b => b.ValidLocationCount < _locationsPerBin))
            {
                bin.AddLocation(locationWithBinAssignments);
            }

            // Balance bins by taking bins from items with most assigned bins.
            while (locationWithBinAssignments.Count < _locationsByAssignmentCount.Max.Count - 1)
            {
                var max = _locationsByAssignmentCount.Max;

                // First assignment which isn't set to expire or is in a bin that contains the item we want to add.
                var assignmentToReplace = max.BinAssignments.First(assignment => assignment.ExpiryTime == null && !assignment.Bin.Any(other => other.Location.Equals(item)));

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
        private LocationWithBinAssignments? GetLeastUsedValidLocation(HashSet<BinAssignment> forBin)
        {
            var stashed = new List<LocationWithBinAssignments>();
            try
            {
                while (_locationsByAssignmentCount.Count > 0)
                {
                    var next = _locationsByAssignmentCount.Min;
                    if (forBin.Any(assignment => assignment.Location.Path == next.Location.Path))
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
            return _bins[index].Select(e => e.Location).ToArray();
        }

        internal MachineLocation[][] GetBins() => _bins.Select(bin => bin.Select(assignment => assignment.Location).ToArray()).ToArray();

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

        private class Bin : HashSet<BinAssignment>
        {
            private readonly SortedSet<LocationWithBinAssignments> _locationsByTimesUsed;
            private readonly IClock _clock;

            public int ValidLocationCount { get; private set; } = 0;

            public Bin(SortedSet<LocationWithBinAssignments> locationsByTimesUsed, IClock clock)
            {
                _locationsByTimesUsed = locationsByTimesUsed;
                _clock = clock;
            }

            public void AddLocation(LocationWithBinAssignments location)
            {
                Mutate(location, l =>
                {
                    var assignment = new BinAssignment(location, this);

                    if (!Add(assignment))
                    {
                        throw new InvalidOperationException("A location should not be added twice");
                    }

                    l.AddEntry(assignment);
                });

                ValidLocationCount++;
            }

            public void ReplaceAssignment(BinAssignment assignment, LocationWithBinAssignments newLocation)
            {
                Mutate(assignment.LocationWithEntries, l =>
                {
                    Expire(assignment, _clock.UtcNow); //FIX
                    AddLocation(newLocation);
                });
            }

            public void Expire(BinAssignment assignment, DateTime expiryTime)
            {
                if (assignment.ExpiryTime != null)
                {
                    return;
                }

                ValidLocationCount--;

                assignment.LocationWithEntries.Expire(assignment, expiryTime);
            }

            public void Prune()
            {
                var assignmentsToRemove = new List<BinAssignment>();

                foreach (var assignment in this)
                {
                    if (assignment.ExpiryTime <= _clock.UtcNow)
                    {
                        assignmentsToRemove.Add(assignment);
                    }
                }

                foreach (var assignment in assignmentsToRemove)
                {
                    Remove(assignment);
                    assignment.LocationWithEntries.BinAssignments.Remove(assignment);
                }
            }

            private void Mutate(LocationWithBinAssignments location, Action<LocationWithBinAssignments> action)
            {
                var removed = _locationsByTimesUsed.Remove(location);

                action(location);

                if (removed)
                {
                    _locationsByTimesUsed.Add(location);
                }
            }
        }

        private class BinAssignment
        {
            public LocationWithBinAssignments LocationWithEntries { get; }

            public DateTime? ExpiryTime { get; set; }

            public Bin Bin { get; }

            public BinAssignment(LocationWithBinAssignments location, Bin bin)
            {
                LocationWithEntries = location;
                ExpiryTime = null;
                Bin = bin;
            }

            public MachineLocation Location => LocationWithEntries.Location;
        }

        private class LocationWithBinAssignments
        {
            public MachineLocation Location { get; set; }
            public int Count { get; private set; } = 0;

            public List<BinAssignment> BinAssignments { get; set; } = new List<BinAssignment>();
            public LocationWithBinAssignments(MachineLocation location) => Location = location;

            public void AddEntry(BinAssignment assignment)
            {
                BinAssignments.Add(assignment);
                Count++;
            }

            public void Expire(BinAssignment assignment, DateTime expiryTime)
            {
                if (assignment.ExpiryTime != null)
                {
                    return;
                }

                assignment.ExpiryTime = expiryTime;
                Count--;
            }
        }

        private class ItemComparer : IComparer<LocationWithBinAssignments>
        {
            public int Compare(LocationWithBinAssignments x, LocationWithBinAssignments y)
            {
                return x.Count != y.Count
                    ? x.Count.CompareTo(y.Count)
                    : x.Location.Path.CompareTo(y.Location.Path);
            }
        }
    }
}
