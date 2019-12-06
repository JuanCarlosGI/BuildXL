using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace BuildXL.Cache.ContentStore.Distributed.NuCache.Binning
{
    // <nodoc />
    internal class BinAssignment
    {
        // <nodoc />
        public LocationWithBinAssignments LocationWithAssignments { get; }

        // <nodoc />
        public DateTime? ExpiryTime { get; set; }

        // <nodoc />
        public Bin Bin { get; }

        // <nodoc />
        public BinAssignment(LocationWithBinAssignments location, Bin bin)
        {
            LocationWithAssignments = location;
            ExpiryTime = null;
            Bin = bin;
        }

        // <nodoc />
        public MachineLocation Location => LocationWithAssignments.Location;

        public override string ToString()
        {
            return $"{Location.Path} -> {Bin}";
        }
    }
}
