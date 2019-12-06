using System;
using System.Collections.Generic;
using System.Linq;
using BuildXL.Cache.ContentStore.Distributed.NuCache.Binning;
using BuildXL.Cache.ContentStore.Interfaces.Time;
using FluentAssertions;
using Xunit;

namespace BuildXL.Cache.ContentStore.Distributed.Test.ContentLocation.NuCache
{
    public class BinManagerTests
    {
        [Theory]
        [InlineData(3, 0, 1)] // Started with 0 machines
        [InlineData(3, 2, 10)] // Started with small amount of machines
        [InlineData(3, 1000, 24)] // Started with large non-power of two and ends in power of two.
        [InlineData(3, 1024, 10)] // Started in power of two and adds some machines
        public void AddLocationsKeepsBinsBalanced(int locationsPerBin, int initialAmountOfLocations, int locationsToAdd)
        {
            var (manager, locations) = CreateAndValidate(locationsPerBin, initialAmountOfLocations);
            AddLocationsAndValidate(manager, locations, locationsToAdd);
        }

        private (BinManager manager, List<MachineLocation> locations) CreateAndValidate(int locationsPerBin, int amountOfLocations)
        {
            var locations = Enumerable.Range(0, amountOfLocations).Select(num => new MachineLocation(num.ToString())).ToList();
            var manager = new BinManager(locationsPerBin, startLocatons: locations, SystemClock.Instance);

            ValidateBalanced(manager, locations);

            return (manager, locations);
        }

        // Use this to meassure how long it takes to start with 0 machines and add 1024 machines to test performance
        [Fact]
        private void PerfTest()
        {
            var locations = Enumerable.Range(0, 1024).Select(num => new MachineLocation(num.ToString()));
            var manager = new BinManager(locationsPerBin: 3, startLocatons: new MachineLocation[0], SystemClock.Instance);
            foreach (var location in locations)
            {
                manager.AddLocation(location);
            }
        }

        private void AddLocationsAndValidate(BinManager manager, List<MachineLocation> locations, int locationsToAdd)
        {
            var newLocations = Enumerable.Range(locations.Count, locationsToAdd).Select(num => new MachineLocation(num.ToString()));

            foreach (var location in newLocations)
            {
                locations.Add(location);
                manager.AddLocation(location);

                ValidateBalanced(manager, locations);
            }
        }

        private void ValidateBalanced(BinManager manager, List<MachineLocation> locations)
        {
            var expectedLocationsPerBin = locations.Count > manager.LocationsPerBin
                ? manager.LocationsPerBin
                : locations.Count;

            var expectedBinsPerLocation = locations.Count >= manager.LocationsPerBin
                ? manager.LocationsPerBin * BinManager.NumberOfBins / locations.Count
                : BinManager.NumberOfBins;

            static bool isPowerOfTwo(int num) => (num & (num - 1)) == 0;
            var perfectlyBalanced = isPowerOfTwo(locations.Count);

            var bins = manager.GetBins();
            var counts = new Dictionary<string, int>();
            foreach (var location in locations)
            {
                counts[location.Path] = 0;
            }

            foreach (var bin in bins)
            {
                bin.Length.Should().Be(expectedLocationsPerBin);

                foreach (var location in bin)
                {
                    counts[location.Path]++;
                }
            }

            foreach (var count in counts.Values)
            {
                if (perfectlyBalanced)
                {
                    count.Should().Be(expectedBinsPerLocation);
                }
                else
                {
                    count.Should().BeInRange(expectedBinsPerLocation, expectedBinsPerLocation + 1);
                }
            }
        }
    }
}
