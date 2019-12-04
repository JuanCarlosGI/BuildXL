// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Text;
using BuildXL.Cache.ContentStore.Hashing;
using BuildXL.Cache.ContentStore.Utils;
using BuildXL.Utilities;

namespace BuildXL.Cache.ContentStore.Distributed.NuCache
{
    public class BinManager
    {
        private const int MaxBins = 1 << 16;

        private readonly HashSet<MachineLocation>[] _locationEntries;
        private readonly int _entriesPerLocation;
        private readonly int _locationsPerBin;
        private readonly object _lockObject = new object();
        private readonly ConcurrentDictionary<MachineLocation, bool> _machines = new ConcurrentDictionary<MachineLocation, bool>();

        private MachineLocation[][] _bins;

        /// <nodoc />
        public BinManager(int locationsPerBin, int entriesPerLocation, int numberOfBins)
        {
            Contract.Assert(entriesPerLocation <= byte.MaxValue);
            Contract.Assert(IsNumberOfBinsValid(numberOfBins), $"{nameof(numberOfBins)} should be in range [1, {MaxBins}] and be a power of 2.");
            _locationsPerBin = locationsPerBin;
            _entriesPerLocation = entriesPerLocation;
            _locationEntries = new HashSet<MachineLocation>[numberOfBins];
        }

        private static bool IsNumberOfBinsValid(int amount)
        {
            return amount > 0 && amount <= MaxBins &&
                ((amount & (amount - 1)) == 0); // Is power of 2.
        }

        /// <nodoc />
        public void AddLocation(MachineLocation location) => ProcessLocation(location, isAdd: true);

        /// <nodoc />
        public void RemoveLocation(MachineLocation location) => ProcessLocation(location, isAdd: false);


        private void ProcessLocation(MachineLocation location, bool isAdd)
        {
            if (isAdd)
            {
                _machines[location] = true;
            }
            else
            {
                _machines.TryRemove(location, out _);
            }

            var hasher = ContentHashers.Get(HashType.MD5);
            for (var i = 0; i < _entriesPerLocation; i++)
            {
                var hash = hasher.GetContentHash(Encoding.UTF8.GetBytes(location.Path + i));//  HashCodeHelper.GetOrdinalHashCode(location.Path + i);
                var index = unchecked(BitConverter.ToUInt32(hash.ToByteArray(), 1)) % _locationEntries.Length;
                var entrySet = _locationEntries[index];
                if (entrySet == null)
                {
                    entrySet = new HashSet<MachineLocation>();
                    _locationEntries[index] = entrySet;
                }

                if (isAdd)
                {
                    entrySet.Add(location);
                }
                else
                {
                    entrySet.Remove(location);
                }
            }

            // Invalidate current configuration.
            lock (_lockObject)
            {
                _bins = null;
            }
        }

        /// <nodoc />
        public MachineLocation[] GetLocations(ContentHash hash)
        {
            lock (_lockObject)
            {
                _bins ??= ComputeBins();
                var index = hash[0] | hash[1] << 8;
                return _bins[index % _locationEntries.Length];
            }
        }

        /// <nodoc />
        public IEnumerable<IReadOnlyList<MachineLocation>> GetBins()
        {
            lock (_lockObject)
            {
                _bins ??= ComputeBins();
                return _bins;
            }
        }

        /// <summary>
        ///     Computes the designated locations for each of the bins.
        ///     The way this is done is by getting the next x machines (clockwise in a "circular array"),
        /// avoiding repetitions, and skipping overused locations to balance content between all locations.
        /// </summary>
        private MachineLocation[][] ComputeBins()
        {
            var bins = new MachineLocation[_locationEntries.Length][];

            var maxMachineUsage = 1 + (_locationEntries.Length * _locationsPerBin) / _machines.Count;
            var locationUsage = new ConcurrentDictionary<MachineLocation, int>();

            for (int i = 0, max = _locationEntries.Length; i < max; i++)
            {
                var bin = new List<MachineLocation>();

                for (int j = i, end = max + i; j < end; j++)
                {
                    foreach (var location in getLocationsAt(j % max))
                    {
                        if (!bin.Contains(location))
                        {
                            if (locationUsage.AddOrUpdate(location, 1, (k, v) => v + 1) < maxMachineUsage)
                            {
                                bin.Add(location);
                                if (bin.Count == _locationsPerBin)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    if (bin.Count == _locationsPerBin)
                    {
                        break;
                    }
                }

                bins[i] = bin.ToArray();
            }

            return bins;

            IEnumerable<MachineLocation> getLocationsAt(int entryIndex)
            {
                var entry = _locationEntries[entryIndex];
                if (entry != null)
                {
                    return entry.OrderBy(l => l.Path);
                }

                return Enumerable.Empty<MachineLocation>();
            }
        }

        public override string ToString()
        {
            var locationMappings = string.Join(Environment.NewLine, _locationEntries.Select(e => e == null ? string.Empty : string.Join(", ", e)));

            var bins = GetBins();
            var binText = string.Join(Environment.NewLine, bins.SelectMany((bin, index) => bin.Select(l => (l, index))).GroupBy(e => e.l).OrderBy(g => g.Count()).Select(g => $"{g.Key}: ({g.Count()}) [{string.Join(", ", g.Select(t => t.index))}] "));

            var collisions = _locationEntries.Where(e => e?.Count > 1).Sum(e => e.Count - 1);
            return string.Join(Environment.NewLine, $"Collisions: {collisions}", locationMappings, binText);
        }
    }
}
