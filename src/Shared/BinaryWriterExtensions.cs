// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    internal static class BinaryWriterExtensions
    {
#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void WriteOptionalString(this BinaryWriter writer, string? value)
        {
            if (value == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(value);
            }
        }

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void WriteOptionalInt32(this BinaryWriter writer, int? value)
        {
            if (value == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(value.Value);
            }
        }

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void WriteTimestamp(this BinaryWriter writer, DateTime timestamp)
        {
            writer.Write(timestamp.Ticks);
            writer.Write((Int32)timestamp.Kind);
        }

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void Write7BitEncodedInt(this BinaryWriter writer, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }

            writer.Write((byte)v);
        }

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteOptionalBuildEventContext(this BinaryWriter writer, BuildEventContext? context)
        {
            if (context == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.WriteBuildEventContext(context);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBuildEventContext(this BinaryWriter writer, BuildEventContext context)
        {
            writer.Write(context.NodeId);
            writer.Write(context.ProjectContextId);
            writer.Write(context.TargetId);
            writer.Write(context.TaskId);
            writer.Write(context.SubmissionId);
            writer.Write(context.ProjectInstanceId);
            writer.Write(context.EvaluationId);
        }
#endif

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void WriteGuid(this BinaryWriter writer, Guid value)
        {
            Guid val = value;
            unsafe
            {
                byte* ptr = (byte*)&val;
                for (int i = 0; i < sizeof(Guid); i++, ptr++)
                {
                    writer.Write(*ptr);
                }
            }
        }

        public static void WriteExtendedBuildEventData(this BinaryWriter writer, IExtendedBuildEventArgs data)
        {
            writer.Write(data.ExtendedType);
            writer.WriteOptionalString(data.ExtendedData);

            writer.Write(data.ExtendedMetadata != null);
            if (data.ExtendedMetadata != null)
            {
                writer.Write7BitEncodedInt(data.ExtendedMetadata.Count);
                foreach (KeyValuePair<string, string?> kvp in data.ExtendedMetadata)
                {
                    writer.Write(kvp.Key);
                    writer.WriteOptionalString(kvp.Value);
                }
            }
        }

        public static void WriteDurationsDictionary(this BinaryWriter writer, Dictionary<string, TimeSpan> durations)
        {
            writer.Write7BitEncodedInt(durations.Count);
            foreach (KeyValuePair<string, TimeSpan> kvp in durations)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value.Ticks);
            }
        }
    }
}
