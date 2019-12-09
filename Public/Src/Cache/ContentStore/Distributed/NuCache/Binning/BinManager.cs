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

        internal int LocationsPerBin { get; }

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

        public BinManager(int locationsPerBin, BinMappings binMappings, IClock clock)
        {
            LocationsPerBin = locationsPerBin;
            _clock = clock;
            _locationsByAssignmentCount = new SortedSet<LocationWithBinAssignments>(new ItemComparer());
            _bins = new List<Bin>(NumberOfBins);
            _locationToAssignments = new Dictionary<MachineLocation, LocationWithBinAssignments>();

            var bins = binMappings.GetBins();
            Contract.Assert(bins.Count == NumberOfBins);

            var previousLocationsPerBin = bins[0].Count(m => !m.IsExpired);

            if (previousLocationsPerBin == locationsPerBin)
            {
                InitializeFromSameAmountOfLocations(bins);
            }
            else if (previousLocationsPerBin < locationsPerBin)
            {
                InitializeFromLessAmountOfLocations(bins, previousLocationsPerBin);
            }
            else
            {
                InitializeFromMoreAmountOfLocations(bins);
            }
        }

        private void InitializeFromMoreAmountOfLocations(IReadOnlyList<IReadOnlyList<MappingWithExpiry>> bins)
        {
            var activeLocations = new HashSet<LocationWithBinAssignments>();

            foreach (var mapping in bins)
            {
                var bin = new Bin(this, _clock);
                _bins.Add(bin);

                var added = 0;

                // Add everything to fill bin capacity.
                foreach (var locationWithExpiry in mapping)
                {
                    var locationWithAssignments = _locationToAssignments.GetOrAdd(locationWithExpiry.Location, m => new LocationWithBinAssignments(m));

                    if (locationWithExpiry.Expiry.HasValue)
                    {
                        bin.AddLocation(locationWithAssignments);

                        if (locationWithExpiry.Expiry.HasValue)
                        {
                            var assignment = bin.Assignments.First(a => a.LocationWithAssignments == locationWithAssignments);
                            bin.Expire(assignment, locationWithExpiry.Expiry.Value);
                        }
                    }
                    else
                    {
                        activeLocations.Add(locationWithAssignments);

                        if (added < LocationsPerBin)
                        {
                            added++;
                            bin.AddLocation(locationWithAssignments);
                        }
                    }
                }
            }

            Prune();

            // Make sure that all acitve locations are in the queue.
            foreach (var location in activeLocations)
            {
                _locationsByAssignmentCount.Add(location);
            }

            // Balance by giving assignments from the most assigned to the least assigned.
            while (_locationsByAssignmentCount.Min!.ValidAssignmentCount < _locationsByAssignmentCount.Max!.ValidAssignmentCount - 1)
            {
                var min = _locationsByAssignmentCount.Min!;
                var max = _locationsByAssignmentCount.Max!;

                var assignmentToReplace = max.BinsAssignedTo
                    .Except(min.BinsAssignedTo)
                    .Select(bin => bin.Assignments.First(assignment => assignment.ExpiryTime == null && assignment.LocationWithAssignments == max)).First();

                assignmentToReplace.Bin.ReplaceAssignment(assignmentToReplace, min);
            }
        }

        private void InitializeFromSameAmountOfLocations(IReadOnlyList<IReadOnlyList<MappingWithExpiry>> bins)
        {
            foreach (var mapping in bins)
            {
                var bin = new Bin(this, _clock);
                _bins.Add(bin);

                foreach (var locationWithExpiry in mapping)
                {
                    var locationWithAssignments = _locationToAssignments.GetOrAdd(locationWithExpiry.Location, m => new LocationWithBinAssignments(m));
                    bin.AddLocation(locationWithAssignments);

                    if (locationWithExpiry.Expiry.HasValue)
                    {
                        var assignment = bin.Assignments.First(a => a.LocationWithAssignments == locationWithAssignments);
                        bin.Expire(assignment, locationWithExpiry.Expiry.Value);
                    }
                }
            }

            foreach (var locationWithAssignments in _locationToAssignments.Values)
            {
                _locationsByAssignmentCount.Add(locationWithAssignments);
            }
        }

        private void InitializeFromLessAmountOfLocations(IReadOnlyList<IReadOnlyList<MappingWithExpiry>> bins, int previousLocationsPerBin)
        {
            InitializeFromSameAmountOfLocations(bins);

            // Only attempt to fill if there are enough locations to do so.
            if (_locationToAssignments.Count > previousLocationsPerBin)
            {
                if (_locationToAssignments.Count < LocationsPerBin)
                {
                    // Still more locations we can assign, but not enough to fill all the bins

                    foreach (var bin in _bins)
                    {
                        foreach (var location in _locationToAssignments.Values.Where(l => bin.Assignments.All(a => a.LocationWithAssignments != l)))
                        {
                            bin.AddLocation(location);
                        }
                    }
                }
                else
                {
                    // We can fill all our bins.

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
        }

        public void AddLocation(MachineLocation item)
        {
            var locationWithBinAssignments = _locationToAssignments.GetOrAdd(item, i => new LocationWithBinAssignments(item));
            Contract.Assert(locationWithBinAssignments.ValidAssignmentCount == 0, "Adding locations twice before removing is not supported.");

            _locationsByAssignmentCount.Add(locationWithBinAssignments);

            // Make sure to fill bins which could have had remained unfilled during initialization.
            foreach (var bin in _bins.Where(b => b.ValidLocationCount < LocationsPerBin))
            {
                bin.AddLocation(locationWithBinAssignments);
            }

            Func<LocationWithBinAssignments, BinAssignment> getNextAssignmentToReplace;
            if (locationWithBinAssignments.ValidAssignmentCount == 0 && _locationsByAssignmentCount.Max!.ValidAssignmentCount > _locationsByAssignmentCount.Count)
            {
                // This implementation performs better when there is a small amount of total locations
                // Track, for all locations, the bins that are still valid for the location we're adding.

                var availableBinsDictionary = new Dictionary<LocationWithBinAssignments, HashSet<Bin>>();
                foreach (var location in _locationsByAssignmentCount.Where(location => location != locationWithBinAssignments))
                {
                    // Initially all assignments from all locations are eligible
                    availableBinsDictionary[location] = new HashSet<Bin>(location.BinsAssignedTo);
                }

                getNextAssignmentToReplace = fromLocation =>
                {
                    var binToReplace = availableBinsDictionary[fromLocation].First();

                    // Assignments in this bin are now ineligible for all locations.
                    foreach (var other in availableBinsDictionary)
                    {
                        other.Value.Remove(binToReplace);
                    }

                    return binToReplace.Assignments.First(a => a.LocationWithAssignments == fromLocation);
                };
            }
            else
            {
                // This implementation performs better when there are lots of machines, thus each having less assigned bins.
                // Walk through assignments until one of them is in a bin that does not contain the location we're adding.

                getNextAssignmentToReplace = fromLocation =>
                {
                    return fromLocation.BinsAssignedTo
                        .Except(locationWithBinAssignments.BinsAssignedTo)
                        .Select(bin => bin.Assignments.First(assignment => assignment.ExpiryTime == null && assignment.LocationWithAssignments == fromLocation)).First();
                };
            }

            // Balance bins by taking bins from items with most assigned bins.
            while (locationWithBinAssignments.ValidAssignmentCount < _locationsByAssignmentCount.Max!.ValidAssignmentCount - 1)
            {
                var max = _locationsByAssignmentCount.Max!;
                var assignmentToReplace = getNextAssignmentToReplace(max);
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

        public BinMappings GetBinMappings() => new BinMappings(_bins.Select(bin => bin.Assignments.Select(assignment => new MappingWithExpiry(assignment.Location, assignment.ExpiryTime)).ToArray()).ToArray());

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
