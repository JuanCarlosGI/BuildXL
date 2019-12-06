using System;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.IO;
using System.Linq;
using BuildXL.Cache.ContentStore.Interfaces.Time;
using BuildXL.Utilities;

#nullable enable

namespace BuildXL.Cache.ContentStore.Distributed.NuCache.Binning
{
    // <nodoc />
    internal class Bin
    {
        private readonly HashSet<BinAssignment> _binAssignments = new HashSet<BinAssignment>();
        private readonly BinManager _manager;
        private readonly IClock _clock;

        public int ValidLocationCount { get; private set; } = 0;

        public Bin(BinManager manager, IClock clock)
        {
            _clock = clock;
            _manager = manager;
        }

        public void AddLocation(LocationWithBinAssignments location)
        {
            _manager.Mutate(location, l =>
            {
                var assignment = new BinAssignment(location, this);

                if (!_binAssignments.Add(assignment))
                {
                    throw new InvalidOperationException("A location should not be added twice");
                }

                l.AddAssignment(assignment);
            });

            ValidLocationCount++;
        }

        public IEnumerable<BinAssignment> Assignments => _binAssignments;

        public void ReplaceAssignment(BinAssignment assignment, LocationWithBinAssignments newLocation)
        {
            Contract.Assert(assignment.Bin == this);
            Contract.Assert(_binAssignments.Contains(assignment));
            Contract.Assert(_binAssignments.All(a => a.LocationWithAssignments.Location.Path != newLocation.Location.Path));

            _manager.Mutate(assignment.LocationWithAssignments, l =>
            {
                Expire(assignment, _clock.UtcNow); //FIX
                AddLocation(newLocation);
            });
        }

        public bool Expire(BinAssignment assignment, DateTime expiryTime)
        {
            Contract.Assert(assignment.Bin == this);
            Contract.Assert(_binAssignments.Contains(assignment));

            var wasExpired = assignment.LocationWithAssignments.Expire(assignment, expiryTime);

            if (wasExpired)
            {
                ValidLocationCount--;
            }

            return wasExpired;
        }

        public void Prune()
        {
            var assignmentsToRemove = new List<BinAssignment>();

            foreach (var assignment in _binAssignments)
            {
                if (assignment.ExpiryTime <= _clock.UtcNow)
                {
                    assignmentsToRemove.Add(assignment);
                }
            }

            foreach (var assignment in assignmentsToRemove)
            {
                _binAssignments.Remove(assignment);
                assignment.LocationWithAssignments.BinAssignments.Remove(assignment);
            }
        }

        public override string ToString()
        {
            return string.Join(",", _binAssignments.Select(a => a.Location.Path));
        }

        /// <summary>
        /// Serializes itself into a stream writer
        /// </summary>
        /// <param name="writer"></param>
        public void Serialize(BuildXLWriter writer)
        {
            writer.WriteReadOnlyList(_binAssignments.ToArray(), (w, assignment) =>
            {
                var data = assignment.Location.Data;
                w.Write(data.Length);
                w.Write(data);
                w.Write(assignment.ExpiryTime != null);
                if (assignment.ExpiryTime != null)
                {
                    w.Write(assignment.ExpiryTime.Value);
                }
            });
        }

        public static (MachineLocation, DateTime?)[] Deserialize(BuildXLReader reader)
        {
            return reader.ReadArray<(MachineLocation, DateTime?)>(r =>
            {
                var locationLength = r.ReadInt32();
                var data = r.ReadBytes(locationLength);
                var hasExpiry = r.ReadBoolean();
                DateTime? expiry = null;
                if (hasExpiry)
                {
                    expiry = r.ReadDateTime();
                }

                return (new MachineLocation(data), expiry);
            });
        }
    }
}
