// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    internal static class BinaryWriterExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteOptionalString(this BinaryWriter writer, string value)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteTimestamp(this BinaryWriter writer, DateTime timestamp)
        {
            writer.Write(timestamp.Ticks);
            writer.Write((Int32)timestamp.Kind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteOptionalBuildEventContext(this BinaryWriter writer, BuildEventContext context)
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
    }
}
