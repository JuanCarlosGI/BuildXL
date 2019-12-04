using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using BuildXL.Cache.ContentStore.Hashing;
using BuildXL.Cache.ContentStore.Interfaces.Time;
using BuildXL.Utilities.Collections;
using Microsoft.IdentityModel.Tokens;

#nullable enable

namespace BuildXL.Cache.ContentStore.Distributed.NuCache
{
    internal class BinManager2
    {
        public const int NumberOfBins = 1 << 16;

        private readonly HashSet<BinAssignment>[] _bins;
        private readonly int _locationsPerBin;
        private readonly IClock _clock;
        private readonly SortedSet<LocationWithBinAssignments> _locationsByTimesUsed;
        private readonly Dictionary<MachineLocation, LocationWithBinAssignments> _locationToEntries = new Dictionary<MachineLocation, LocationWithBinAssignments>();

        public BinManager2(int locationsPerBin, IEnumerable<MachineLocation> startLocatons, IClock clock)
        {
            _bins = new HashSet<BinAssignment>[NumberOfBins];
            _locationsPerBin = locationsPerBin;
            _clock = clock;

            _locationsByTimesUsed = new SortedSet<LocationWithBinAssignments>(startLocatons.Select(i => new LocationWithBinAssignments(i)), new ItemComparer());
            foreach (var locationWithEntries in _locationsByTimesUsed)
            {
                _locationToEntries[locationWithEntries.Location] = locationWithEntries;
            }

            Initialize();
        }

        private void Initialize()
        {
            for (var i = 0; i < _bins.Length; i++)
            {
                _bins[i] = new HashSet<BinAssignment>();
            }

            if (_locationsByTimesUsed.Count <= _locationsPerBin)
            {
                foreach (var bin in _bins)
                {
                    foreach (var locationWithEntries in _locationsByTimesUsed)
                    {
                        var entry = new BinAssignment(locationWithEntries, bin);
                        bin.Add(entry);
                        locationWithEntries.AddEntry(entry);
                    }
                }
            }
            else
            {
                foreach (var bin in _bins)
                {
                    while (bin.Count < _locationsPerBin)
                    {
                        var minLocation = _locationsByTimesUsed.Min;
                        _locationsByTimesUsed.Remove(minLocation);
                        var entry = new BinAssignment(minLocation, bin);
                        bin.Add(entry);
                        minLocation.AddEntry(entry);
                        _locationsByTimesUsed.Add(minLocation);
                    }
                }
            }
        }

        public void AddLocation(MachineLocation item)
        {
            var locationWithEntries = _locationToEntries.GetOrAdd(item, i => new LocationWithBinAssignments(item));

            Contract.Assert(locationWithEntries.BinAssignments.All(e => e.ExpiryTime == null), "Adding locations twice before removing is not supported.");

            // Make sure to fill bins which could have had remained unfilled during initialization.
            foreach (var bin in _bins.Where(b => b.Count < _locationsPerBin))
            {
                var entry = new BinAssignment(locationWithEntries, bin);
                bin.Add(entry);
                locationWithEntries.AddEntry(entry);
            }

            // Balance bins by taking bins from items with most entries.
            while (locationWithEntries.Count < _locationsByTimesUsed.Max.Count - 1)
            {
                var max = _locationsByTimesUsed.Max;
                _locationsByTimesUsed.Remove(max);

                // First entry which isn't set to expire or is in a bin that contains the item we want to add.
                var entry = max.BinAssignments.First(e => e.ExpiryTime == null && !e.Bin.Any(o => o.Location.Equals(item)));
                Replace(entry, newLocation: locationWithEntries);
                _locationsByTimesUsed.Add(max);
            }

            _locationsByTimesUsed.Add(locationWithEntries);
        }

        public void RemoveLocation(MachineLocation item)
        {
            var locationWithMappings = _locationToEntries[item];
            _locationsByTimesUsed.Remove(locationWithMappings);

            // Replace entries with least used items.
            foreach (var entry in locationWithMappings.BinAssignments.Where(e => e.ExpiryTime == null))
            {
                var min = GetMinimumValidItem(forBin: entry.Bin);

                if (min != null)
                {
                    Replace(entry, newLocation: min);
                }
                else
                {
                    // No item available for replace. Just remove.
                    locationWithMappings.Expire(entry, _clock.UtcNow); // FIX
                }
            }
        }

        private LocationWithBinAssignments? GetMinimumValidItem(HashSet<BinAssignment> forBin)
        {
            var stashed = new List<LocationWithBinAssignments>();
            while (_locationsByTimesUsed.Count > 0)
            {
                var next = _locationsByTimesUsed.Min;
                if (forBin.Any(entry => entry.Location.Path == next.Location.Path))
                {
                    _locationsByTimesUsed.Remove(next);
                    stashed.Add(next);
                }
                else
                {
                    foreach (var stashedItem in stashed)
                    {
                        _locationsByTimesUsed.Add(stashedItem);
                    }

                    return next;
                }
            }

            // No valid item could be found. Refill queue and return null.

            foreach (var stashedItem in stashed)
            {
                _locationsByTimesUsed.Add(stashedItem);
            }

            return null;
        }

        private void Replace(BinAssignment entry, LocationWithBinAssignments newLocation)
        {
            entry.LocationWithEntries.Expire(entry, _clock.UtcNow); //FIX

            var bin = entry.Bin;
            var newEntry = new BinAssignment(newLocation, bin);
            bin.Add(newEntry);
            newLocation.AddEntry(newEntry);
        }

        public IReadOnlyList<MachineLocation> GetLocations(ContentHash hash)
        {
            var index = hash[0] | hash[1] << 8;
            return _bins[index].Select(e => e.Location).ToArray();
        }

        internal MachineLocation[][] GetBins() => _bins.Select(bin => bin.Select(entry => entry.Location).ToArray()).ToArray();

        // Not sure if the class should manage itself or if it should receive an external signal to prune.
        public void Prune()
        {
            foreach (var bin in _bins)
            {
                var entriesToRemove = new List<BinAssignment>();

                foreach (var entry in bin)
                {
                    if (entry.ExpiryTime <= _clock.UtcNow)
                    {
                        entriesToRemove.Add(entry);
                    }
                }

                foreach (var entry in entriesToRemove)
                {
                    bin.Remove(entry);
                    _locationToEntries[entry.Location].BinAssignments.Remove(entry);
                }
            }

            var itemsToRemove = new List<LocationWithBinAssignments>();
            foreach (var item in _locationsByTimesUsed)
            {
                if (item.BinAssignments.Count(entry => entry.ExpiryTime <= _clock.UtcNow) == 0)
                {
                    itemsToRemove.Add(item);
                }
            }

            foreach (var item in itemsToRemove)
            {
                _locationsByTimesUsed.Remove(item);
                _locationToEntries.Remove(item.Location);
            }
        }

        private class Bin
        {
            public HashSet<BinAssignment> Assignments { get; set; } = new HashSet<BinAssignment>();

            public readonly SortedSet<LocationWithBinAssignments> _locationsByTimesUsed;

            public Bin(SortedSet<LocationWithBinAssignments> locationsByTimesUsed)
            {
                _locationsByTimesUsed = locationsByTimesUsed;
            }

            public void AddLocation(LocationWithBinAssignments location)
            {
                _locationsByTimesUsed.Remove(location);

                var entry = new BinAssignment(location, Assignments);
                Assignments.Add(entry);
                location.AddEntry(entry);

                _locationsByTimesUsed.Add(location);
            }
        }

        private class BinAssignment
        {
            public LocationWithBinAssignments LocationWithEntries { get; }

            public DateTime? ExpiryTime { get; set; }

            public HashSet<BinAssignment> Bin { get; }

            public BinAssignment(LocationWithBinAssignments location, HashSet<BinAssignment> bin)
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

            public void AddEntry(BinAssignment entry)
            {
                BinAssignments.Add(entry);
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
