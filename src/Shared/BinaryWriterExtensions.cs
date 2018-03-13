using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
            writer.Write((Int64)timestamp.Ticks);
            writer.Write((Int32)timestamp.Kind);
        }
    }
}
