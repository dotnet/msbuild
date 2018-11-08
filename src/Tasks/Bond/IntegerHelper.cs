using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Helper methods for encoding and decoding integer values.
    /// </summary>
    internal static class IntegerHelper
    {
        public const int MaxBytesVarInt16 = 3;
        public const int MaxBytesVarInt32 = 5;
        public const int MaxBytesVarInt64 = 10;
        public static int EncodeVarUInt16(byte[] data, ushort value, int index)
        {
            // byte 0
            if (value >= 0x80)
            {
                data[index++] = (byte) (value | 0x80);
                value >>= 7;
                // byte 1
                if (value >= 0x80)
                {
                    data[index++] = (byte) (value | 0x80);
                    value >>= 7;
                }
            }

            // byte 2
            data[index++] = (byte) value;
            return index;
        }

        public static int EncodeVarUInt32(byte[] data, uint value, int index)
        {
            // byte 0
            if (value >= 0x80)
            {
                data[index++] = (byte) (value | 0x80);
                value >>= 7;
                // byte 1
                if (value >= 0x80)
                {
                    data[index++] = (byte) (value | 0x80);
                    value >>= 7;
                    // byte 2
                    if (value >= 0x80)
                    {
                        data[index++] = (byte) (value | 0x80);
                        value >>= 7;
                        // byte 3
                        if (value >= 0x80)
                        {
                            data[index++] = (byte) (value | 0x80);
                            value >>= 7;
                        }
                    }
                }
            }

            // last byte
            data[index++] = (byte) value;
            return index;
        }

        public static int EncodeVarUInt64(byte[] data, ulong value, int index)
        {
            // byte 0
            if (value >= 0x80)
            {
                data[index++] = (byte) (value | 0x80);
                value >>= 7;
                // byte 1
                if (value >= 0x80)
                {
                    data[index++] = (byte) (value | 0x80);
                    value >>= 7;
                    // byte 2
                    if (value >= 0x80)
                    {
                        data[index++] = (byte) (value | 0x80);
                        value >>= 7;
                        // byte 3
                        if (value >= 0x80)
                        {
                            data[index++] = (byte) (value | 0x80);
                            value >>= 7;
                            // byte 4
                            if (value >= 0x80)
                            {
                                data[index++] = (byte) (value | 0x80);
                                value >>= 7;
                                // byte 5
                                if (value >= 0x80)
                                {
                                    data[index++] = (byte) (value | 0x80);
                                    value >>= 7;
                                    // byte 6
                                    if (value >= 0x80)
                                    {
                                        data[index++] = (byte) (value | 0x80);
                                        value >>= 7;
                                        // byte 7
                                        if (value >= 0x80)
                                        {
                                            data[index++] = (byte) (value | 0x80);
                                            value >>= 7;
                                            // byte 8
                                            if (value >= 0x80)
                                            {
                                                data[index++] = (byte) (value | 0x80);
                                                value >>= 7;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // last byte
            data[index++] = (byte) value;
            return index;
        }

        public static ushort DecodeVarUInt16(byte[] data, ref int index)
        {
            var i = index;
            // byte 0
            uint result = data[i++];
            if (0x80u <= result)
            {
                // byte 1
                uint raw = data[i++];
                result = (result & 0x7Fu) | ((raw & 0x7Fu) << 7);
                if (0x80u <= raw)
                {
                    // byte 2
                    raw = data[i++];
                    result |= raw << 14;
                }
            }

            index = i;
            return (ushort) result;
        }

        public static uint DecodeVarUInt32(byte[] data, ref int index)
        {
            var i = index;
            // byte 0
            uint result = data[i++];
            if (0x80u <= result)
            {
                // byte 1
                uint raw = data[i++];
                result = (result & 0x7Fu) | ((raw & 0x7Fu) << 7);
                if (0x80u <= raw)
                {
                    // byte 2
                    raw = data[i++];
                    result |= (raw & 0x7Fu) << 14;
                    if (0x80u <= raw)
                    {
                        // byte 3
                        raw = data[i++];
                        result |= (raw & 0x7Fu) << 21;
                        if (0x80u <= raw)
                        {
                            // byte 4
                            raw = data[i++];
                            result |= raw << 28;
                        }
                    }
                }
            }

            index = i;
            return result;
        }

        public static ulong DecodeVarUInt64(byte[] data, ref int index)
        {
            var i = index;
            // byte 0
            ulong result = data[i++];
            if (0x80u <= result)
            {
                // byte 1
                ulong raw = data[i++];
                result = (result & 0x7Fu) | ((raw & 0x7Fu) << 7);
                if (0x80u <= raw)
                {
                    // byte 2
                    raw = data[i++];
                    result |= (raw & 0x7Fu) << 14;
                    if (0x80u <= raw)
                    {
                        // byte 3
                        raw = data[i++];
                        result |= (raw & 0x7Fu) << 21;
                        if (0x80u <= raw)
                        {
                            // byte 4
                            raw = data[i++];
                            result |= (raw & 0x7Fu) << 28;
                            if (0x80u <= raw)
                            {
                                // byte 5
                                raw = data[i++];
                                result |= (raw & 0x7Fu) << 35;
                                if (0x80u <= raw)
                                {
                                    // byte 6
                                    raw = data[i++];
                                    result |= (raw & 0x7Fu) << 42;
                                    if (0x80u <= raw)
                                    {
                                        // byte 7
                                        raw = data[i++];
                                        result |= (raw & 0x7Fu) << 49;
                                        if (0x80u <= raw)
                                        {
                                            // byte 8
                                            raw = data[i++];
                                            result |= raw << 56;
                                            if (0x80u <= raw)
                                            {
                                                // byte 9
                                                i++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            index = i;
            return result;
        }
    }
}
