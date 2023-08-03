// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable


namespace Microsoft.Extensions.HotReload
{
    internal readonly struct UpdatePayload
    {
        public const byte ApplySuccessValue = 0;

        private static readonly byte Version = 1;

        public IReadOnlyList<UpdateDelta> Deltas { get; }

        public UpdatePayload(IReadOnlyList<UpdateDelta> deltas)
        {
            Deltas = deltas;
        }

        /// <summary>
        /// Called by the dotnet-watch.
        /// </summary>
        public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            await using var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            binaryWriter.Write(Version);
            binaryWriter.Write(Deltas.Count);

            for (var i = 0; i < Deltas.Count; i++)
            {
                var delta = Deltas[i];
                binaryWriter.Write(delta.ModuleId.ToString());
                await WriteBytesAsync(binaryWriter, delta.MetadataDelta, cancellationToken);
                await WriteBytesAsync(binaryWriter, delta.ILDelta, cancellationToken);
                WriteIntArray(binaryWriter, delta.UpdatedTypes);
            }

            static ValueTask WriteBytesAsync(BinaryWriter binaryWriter, byte[] bytes, CancellationToken cancellationToken)
            {
                binaryWriter.Write(bytes.Length);
                binaryWriter.Flush();
                return binaryWriter.BaseStream.WriteAsync(bytes, cancellationToken);
            }

            static void WriteIntArray(BinaryWriter binaryWriter, int[] values)
            {
                if (values is null)
                {
                    binaryWriter.Write(0);
                    return;
                }

                binaryWriter.Write(values.Length);
                foreach (var value in values)
                {
                    binaryWriter.Write(value);
                }
            }
        }

        /// <summary>
        /// Called by delta applier.
        /// </summary>
        public static async ValueTask<UpdatePayload> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var version = binaryReader.ReadByte();
            if (version != Version)
            {
                throw new NotSupportedException($"Unsupported version {version}.");
            }

            var count = binaryReader.ReadInt32();

            var deltas = new UpdateDelta[count];
            for (var i = 0; i < count; i++)
            {
                var moduleId = Guid.Parse(binaryReader.ReadString());
                var metadataDelta = await ReadBytesAsync(binaryReader, cancellationToken);
                var ilDelta = await ReadBytesAsync(binaryReader, cancellationToken);
                var updatedTypes = ReadIntArray(binaryReader);

                deltas[i] = new UpdateDelta(moduleId, metadataDelta: metadataDelta, ilDelta: ilDelta, updatedTypes);
            }

            return new UpdatePayload(deltas);

            static async ValueTask<byte[]> ReadBytesAsync(BinaryReader binaryReader, CancellationToken cancellationToken)
            {
                var numBytes = binaryReader.ReadInt32();

                var bytes = new byte[numBytes];

                var read = 0;
                while (read < numBytes)
                {
                    read += await binaryReader.BaseStream.ReadAsync(bytes.AsMemory(read), cancellationToken);
                }

                return bytes;
            }

            static int[] ReadIntArray(BinaryReader binaryReader)
            {
                var numValues = binaryReader.ReadInt32();
                if (numValues == 0)
                {
                    return Array.Empty<int>();
                }

                var values = new int[numValues];

                for (var i = 0; i < numValues; i++)
                {
                    values[i] = binaryReader.ReadInt32();
                }

                return values;
            }
        }
    }

    internal readonly struct UpdateDelta
    {
        public Guid ModuleId { get; }
        public byte[] MetadataDelta { get; }
        public byte[] ILDelta { get; }
        public int[] UpdatedTypes { get; }

        public UpdateDelta(Guid moduleId, byte[] metadataDelta, byte[] ilDelta, int[] updatedTypes)
        {
            ModuleId = moduleId;
            MetadataDelta = metadataDelta;
            ILDelta = ilDelta;
            UpdatedTypes = updatedTypes;
        }
    }

    internal readonly struct ClientInitializationPayload
    {
        private const byte Version = 0;

        public string Capabilities { get; }

        public ClientInitializationPayload(string capabilities)
        {
            Capabilities = capabilities;
        }

        /// <summary>
        /// Called by delta applier.
        /// </summary>
        public void Write(Stream stream)
        {
            using var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            binaryWriter.Write(Version);
            binaryWriter.Write(Capabilities);
            binaryWriter.Flush();
        }

        /// <summary>
        /// Called by dotnet-watch.
        /// </summary>
        public static ClientInitializationPayload Read(Stream stream)
        {
            using var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var version = binaryReader.ReadByte();
            if (version != Version)
            {
                throw new NotSupportedException($"Unsupported version {version}.");
            }

            var capabilities = binaryReader.ReadString();
            return new ClientInitializationPayload(capabilities);
        }
    }
}
