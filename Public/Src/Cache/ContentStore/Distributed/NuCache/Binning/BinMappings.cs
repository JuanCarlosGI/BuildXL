using System;
using System.Collections.Generic;
using System.IO;
using BuildXL.Cache.ContentStore.Hashing;
using BuildXL.Utilities;

# nullable enable

namespace BuildXL.Cache.ContentStore.Distributed.NuCache.Binning
{
    internal class BinMappings
    {
        private readonly MappingWithExpiry[][] _bins;

        public BinMappings(MappingWithExpiry[][] bins) => _bins = bins;

        public static BinMappings Deserialize(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var reader = new BuildXLReader(debug: false, stream, leaveOpen: false);

            var bins = reader.ReadArray(DeserializeBin);

            return new BinMappings(bins);
        }

        private static MappingWithExpiry[] DeserializeBin(BuildXLReader reader)
        {
            return reader.ReadArray(r =>
            {
                var locationLength = r.ReadInt32();
                var data = r.ReadBytes(locationLength);
                var hasExpiry = r.ReadBoolean();
                DateTime? expiry = null;
                if (hasExpiry)
                {
                    expiry = r.ReadDateTime();
                }

                return new MappingWithExpiry(new MachineLocation(data), expiry);
            });
        }

        public byte[] Serialize()
        {
            using var stream = new MemoryStream();
            using var writer = new BuildXLWriter(debug: false, stream, leaveOpen: false, logStats: false);

            writer.WriteReadOnlyList(_bins, SerializeBin);
            return stream.ToArray();
        }

        private void SerializeBin(BuildXLWriter writer, MappingWithExpiry[] bin)
        {
            writer.WriteReadOnlyList(bin, (w, assignment) =>
            {
                var data = assignment.Location.Data;
                w.Write(data.Length);
                w.Write(data);
                w.Write(assignment.IsExpired);
                if (assignment.IsExpired)
                {
                    w.Write(assignment.Expiry!.Value);
                }
            });
        }

        public IReadOnlyList<MappingWithExpiry> GetLocations(ContentHash hash)
        {
            var index = hash[0] | hash[1] << 8;
            return _bins[index];
        }

        public IReadOnlyList<IReadOnlyList<MappingWithExpiry>> GetBins() => _bins;
    }

    public struct MappingWithExpiry
    {
        public MachineLocation Location { get; }
        public DateTime? Expiry { get; }

        public MappingWithExpiry(MachineLocation location, DateTime? expiry)
        {
            Location = location;
            Expiry = expiry;
        }

        public bool IsExpired => Expiry != null;
    }
}
