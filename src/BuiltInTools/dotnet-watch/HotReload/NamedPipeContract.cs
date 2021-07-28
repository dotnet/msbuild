// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.HotReload
{
    internal readonly struct UpdatePayload
    {
        private static readonly byte Version = 1;

        public IReadOnlyList<UpdateDelta> Deltas { get; init; }

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
                var delta = new UpdateDelta
                {
                    ModuleId = Guid.Parse(binaryReader.ReadString()),
                    MetadataDelta = await ReadBytesAsync(binaryReader, cancellationToken),
                    ILDelta = await ReadBytesAsync(binaryReader, cancellationToken),
                    UpdatedTypes = ReadIntArray(binaryReader),
                };

                deltas[i] = delta;
            }

            return new UpdatePayload
            {
                Deltas = deltas,
            };

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
        public Guid ModuleId { get; init; }
        public byte[] MetadataDelta { get; init; }
        public byte[] ILDelta { get; init; }
        public int[] UpdatedTypes { get; init; }
    }

    internal enum ApplyResult
    {
        Failed = -1,
        Success = 0,
    }

    internal readonly struct ClientInitializationPayload
    {
        private const byte Version = 0;

        public string Capabilities { get; init; }

        public void Write(Stream stream)
        {
            using var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            binaryWriter.Write(Version);
            binaryWriter.Write(Capabilities);
            binaryWriter.Flush();
        }

        public static ClientInitializationPayload Read(Stream stream)
        {
            using var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var version = binaryReader.ReadByte();
            if (version != Version)
            {
                throw new NotSupportedException($"Unsupported version {version}.");
            }

            var capabilities = binaryReader.ReadString();
            return new ClientInitializationPayload { Capabilities = capabilities };
        }
    }
}
