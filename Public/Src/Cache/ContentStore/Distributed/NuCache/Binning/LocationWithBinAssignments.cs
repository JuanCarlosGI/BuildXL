using System;
using System.Collections.Generic;

#nullable enable

namespace BuildXL.Cache.ContentStore.Distributed.NuCache.Binning
{
    internal class LocationWithBinAssignments
    {
        public MachineLocation Location { get; set; }
        public int ValidAssignmentCount { get; private set; } = 0;

        public HashSet<BinAssignment> BinAssignments { get; set; } = new HashSet<BinAssignment>();

        public HashSet<Bin> BinsAssignedTo { get; } = new HashSet<Bin>();

        public LocationWithBinAssignments(MachineLocation location) => Location = location;

        public void AddAssignment(BinAssignment assignment)
        {
            BinAssignments.Add(assignment);
            BinsAssignedTo.Add(assignment.Bin);
            ValidAssignmentCount++;
        }

        public bool Expire(BinAssignment assignment, DateTime expiryTime)
        {
            if (assignment.ExpiryTime != null || !BinAssignments.Contains(assignment))
            {
                return false;
            }

            BinsAssignedTo.Remove(assignment.Bin);
            assignment.ExpiryTime = expiryTime;
            ValidAssignmentCount--;

            return true;
        }
    }
}
