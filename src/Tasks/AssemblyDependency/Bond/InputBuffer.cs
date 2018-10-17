using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Bond.IO;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Implements IInputStream on top of memory buffer
    /// </summary>
    internal class InputBuffer : IInputStream, ICloneable<InputBuffer>
    {
        readonly int offset;
        internal byte[] buffer;
        internal int end;
        internal int position;

        public virtual long Length
        {
            get { return end - offset; }
        }

        public virtual long Position
        {
            get { return position - offset; }
            set { position = offset + checked((int)value); }
        }

        public InputBuffer(byte[] data)
            : this(data, 0, data.Length)
        { }

        public InputBuffer(byte[] data, int length)
            : this(data, 0, length)
        { }

        public InputBuffer(ArraySegment<byte> seg)
            : this(seg.Array, seg.Offset, seg.Count)
        { }

        public InputBuffer(byte[] data, int offset, int length)
        {
            Debug.Assert(BitConverter.IsLittleEndian);
            buffer = data;
            this.offset = offset;
            end = offset + length;
            position = offset;
        }

        internal InputBuffer(InputBuffer that)
            : this(that.buffer, that.position, that.end - that.position)
        { }

        /// <summary>
        /// Create a clone of the current state of the buffer
        /// </summary>
        public InputBuffer Clone()
        {
            return new InputBuffer(this);
        }

        /// <summary>
        /// Skip forward specified number ot bytes
        /// </summary>
        /// <param name="count">Number of bytes to skip</param>
        /// <exception cref="EndOfStreamException"/>
        public void SkipBytes(int count)
        {
            position += count;
        }

        /// <summary>
        /// Read 8-bit unsigned integer
        /// </summary>
        /// <exception cref="EndOfStreamException"/>
        public byte ReadUInt8()
        {
            if (position >= end)
            {
                EndOfStream(1);
            }
            return buffer[position++];
        }

        /// <summary>
        /// Read little-endian encoded 16-bit unsigned integer
        /// </summary>
        /// <exception cref="EndOfStreamException"/>
        public virtual ushort ReadUInt16()
        {
            if (position > end - sizeof(ushort))
            {
                EndOfStream(sizeof(ushort));
            }
            uint result = buffer[position++];
            result |= ((uint)buffer[position++]) << 8;
            return (ushort)result;
        }

        /// <summary>
        /// Read little-endian encoded 32-bit unsigned integer
        /// </summary>
        /// <exception cref="EndOfStreamException"/>
        public virtual uint ReadUInt32()
        {
            if (position > end - sizeof(uint))
            {
                EndOfStream(sizeof(uint));
            }
            uint result = buffer[position++];
            result |= ((uint)buffer[position++]) << 8;
            result |= ((uint)buffer[position++]) << 16;
            result |= ((uint)buffer[position++]) << 24;
            return result;
        }

        /// <summary>
        /// Read little-endian encoded 64-bit unsigned integer
        /// </summary>
        /// <exception cref="EndOfStreamException"/>
        public virtual ulong ReadUInt64()
        {
            if (position > end - sizeof(ulong))
            {
                EndOfStream(sizeof(ulong));
            }
            var result = BitConverter.ToUInt64(buffer, position);
            position += sizeof(ulong);
            return result;
        }

        /// <summary>
        /// Read little-endian encoded single precision IEEE 754 float
        /// </summary>
        /// <exception cref="EndOfStreamException"/>
        public virtual float ReadFloat()
        {
            if (position > end - sizeof(float))
            {
                EndOfStream(sizeof(float));
            }
            var result = BitConverter.ToSingle(buffer, position);
            position += sizeof(float);
            return result;
        }

        /// <summary>
        /// Read little-endian encoded double precision IEEE 754 float
        /// </summary>
        /// <exception cref="EndOfStreamException"/>
        public virtual double ReadDouble()
        {
            if (position > end - sizeof(double))
            {
                EndOfStream(sizeof(double));
            }
            var result = BitConverter.ToDouble(buffer, position);
            position += sizeof(double);
            return result;
        }

        /// <summary>
        /// Read an array of bytes verbatim
        /// </summary>
        /// <param name="count">Number of bytes to read</param>
        /// <exception cref="EndOfStreamException"/>
        public virtual ArraySegment<byte> ReadBytes(int count)
        {
            if (position > end - count)
            {
                EndOfStream(count);
            }
            var result = new ArraySegment<byte>(buffer, position, count);
            position += count;
            return result;
        }

        /// <summary>
        /// Read variable encoded 16-bit unsigned integer
        /// </summary>
        /// <exception cref="EndOfStreamException"/>
        public ushort ReadVarUInt16()
        {
            if (position > end - IntegerHelper.MaxBytesVarInt16)
            {
                return (ushort)DecodeVarUInt64Checked();
            }
            return IntegerHelper.DecodeVarUInt16(buffer, ref position);
        }

        /// <summary>
        /// Read variable encoded 32-bit unsigned integer
        /// </summary>
        /// <exception cref="EndOfStreamException"/>
        public uint ReadVarUInt32()
        {
            if (position > end - IntegerHelper.MaxBytesVarInt32)
            {
                return (uint)DecodeVarUInt64Checked();
            }
            return IntegerHelper.DecodeVarUInt32(buffer, ref position);
        }

        /// <summary>
        /// Read variable encoded 64-bit unsigned integer
        /// </summary>
        /// <exception cref="EndOfStreamException"/>
        public ulong ReadVarUInt64()
        {
            if (position > end - IntegerHelper.MaxBytesVarInt64)
            {
                return DecodeVarUInt64Checked();
            }
            return IntegerHelper.DecodeVarUInt64(buffer, ref position);
        }

        /// <summary>
        /// Read UTF-8 or UTF-16 encoded string
        /// </summary>
        /// <param name="encoding">String encoding</param>
        /// <param name="size">Size of payload in bytes</param>
        public virtual string ReadString(Encoding encoding, int size)
        {
            if (position > end - size)
            {
                EndOfStream(size);
            }
            var result = encoding.GetString(buffer, position, size);
            position += size;
            return result;
        }

        ulong DecodeVarUInt64Checked()
        {
            ulong raw = 0x80;
            ulong result = 0;
            var shift = 0;
            while (0x7Fu < raw && shift < 64)
            {
                if (position >= end)
                {
                    EndOfStream(1);
                }
                raw = buffer[position++];
                result |= (raw & 0x7Fu) << shift;
                shift += 7;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal virtual void EndOfStream(int count)
        {
            throw new EndOfStreamException("Unexpected end of stream reached.");
        }
    }
}
