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
        private static readonly byte Version = 0;

        public string ChangedFile { get; init; }
        public IReadOnlyList<UpdateDelta> Deltas { get; init; }

        public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            await using var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            binaryWriter.Write(Version);
            binaryWriter.Write(ChangedFile);
            binaryWriter.Write(Deltas.Count);

            for (var i = 0; i < Deltas.Count; i++)
            {
                var delta = Deltas[i];
                binaryWriter.Write(delta.ModuleId.ToString());
                await WriteBytesAsync(binaryWriter, delta.MetadataDelta, cancellationToken);
                await WriteBytesAsync(binaryWriter, delta.ILDelta, cancellationToken);
            }

            static ValueTask WriteBytesAsync(BinaryWriter binaryWriter, byte[] bytes, CancellationToken cancellationToken)
            {
                binaryWriter.Write(bytes.Length);
                binaryWriter.Flush();
                return binaryWriter.BaseStream.WriteAsync(bytes, cancellationToken);
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

            var changedFile = binaryReader.ReadString();
            var count = binaryReader.ReadInt32();

            var deltas = new UpdateDelta[count];
            for (var i = 0; i < count; i++)
            {
                var delta = new UpdateDelta
                {
                    ModuleId = Guid.Parse(binaryReader.ReadString()),
                    MetadataDelta = await ReadBytesAsync(binaryReader, cancellationToken),
                    ILDelta = await ReadBytesAsync(binaryReader, cancellationToken),
                };

                deltas[i] = delta;
            }

            return new UpdatePayload
            {
                ChangedFile = changedFile,
                Deltas = deltas,
            };
        
            static async ValueTask<byte[]> ReadBytesAsync(BinaryReader binaryReader, CancellationToken cancellationToken)
            {
                var numBytes = binaryReader.ReadInt32();
                var bytes = new byte[numBytes];

                var read = 0;
                while (read < numBytes)
                {
                    read += await binaryReader.BaseStream.ReadAsync(bytes.AsMemory(read));
                }

                return bytes;
            }
        }
    }

    internal readonly struct UpdateDelta
    {
        public Guid ModuleId { get; init; }
        public byte[] MetadataDelta { get; init; }
        public byte[] ILDelta { get; init; }
    }

    internal enum ApplyResult
    {
        Failed = -1,
        Success = 0,
    }
}
