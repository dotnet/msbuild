// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    internal static class BinaryReaderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ReadOptionalString(this BinaryReader reader)
        {
            return reader.ReadByte() == 0 ? null : reader.ReadString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read7BitEncodedInt(this BinaryReader reader)
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                // ReadByte handles end of stream cases for us.
                b = reader.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime ReadTimestamp(this BinaryReader reader)
        {
            long timestampTicks = reader.ReadInt64();
            DateTimeKind kind = (DateTimeKind)reader.ReadInt32();
            var timestamp = new DateTime(timestampTicks, kind);
            return timestamp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BuildEventContext ReadOptionalBuildEventContext(this BinaryReader reader)
        {
            if (reader.ReadByte() == 0)
            {
                return null;
            }

            return reader.ReadBuildEventContext();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BuildEventContext ReadBuildEventContext(this BinaryReader reader)
        {
            int nodeId = reader.ReadInt32();
            int projectContextId = reader.ReadInt32();
            int targetId = reader.ReadInt32();
            int taskId = reader.ReadInt32();
            int submissionId = reader.ReadInt32();
            int projectInstanceId = reader.ReadInt32();
            int evaluationId = reader.ReadInt32();

            var buildEventContext = new BuildEventContext(submissionId, nodeId, evaluationId, projectInstanceId, projectContextId, targetId, taskId);
            return buildEventContext;
        }
    }
}
