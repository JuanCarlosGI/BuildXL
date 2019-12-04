using System.Collections.Generic;
using System.Linq;
using BuildXL.Cache.ContentStore.Distributed.NuCache;
using BuildXL.Cache.ContentStore.Interfaces.Time;
using FluentAssertions;
using Xunit;

namespace BuildXL.Cache.ContentStore.Distributed.Test.ContentLocation.NuCache
{
    public class BinManagerTests
    {
        [Fact]
        public void CreateFromLocationsBalanaces()
        {
            var locationsPerBin = 4;
            var amountOfLocations = 8;

            var totalEntries = BinManager2.NumberOfBins * locationsPerBin;
            var entriesPerLocation = totalEntries / amountOfLocations;

            var locations = Enumerable.Range(1, 8).Select(num => new MachineLocation(num.ToString())).ToArray();
            var manager = new BinManager2(locationsPerBin: 4, startLocatons: locations, SystemClock.Instance);

            var bins = manager.GetBins();
            var counts = new Dictionary<string, int>();
            foreach (var location in locations)
            {
                counts[location.Path] = 0;
            }

            foreach (var bin in bins)
            {
                foreach (var location in bin)
                {
                    counts[location.Path]++;
                }
            }

            foreach (var count in counts.Values)
            {
                count.Should().Be(entriesPerLocation);
            }
        }
    }
}
