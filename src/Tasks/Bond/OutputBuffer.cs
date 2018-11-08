using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Bond.IO;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Implements IOutputStream on top of memory buffer
    /// </summary>
    internal class OutputBuffer : IOutputStream
    {
        const int BlockCopyMin = 32;
        internal byte[] buffer;
        internal int position;
        internal int length;

        /// <summary>
        /// Gets data inside the buffer
        /// </summary>
        public ArraySegment<byte> Data
        {
            get { return new ArraySegment<byte>(buffer, 0, position); }
        }

        /// <summary>
        /// Gets or sets the current position within the buffer
        /// </summary>
        public virtual long Position
        {
            get { return position; }
            set { position = checked((int) value); }
        }

        public OutputBuffer(int length = 64 * 1024)
            : this(new byte[length])
        {
        }

        public OutputBuffer(byte[] buffer)
        {
            Debug.Assert(BitConverter.IsLittleEndian);
            this.buffer = buffer;
            length = buffer.Length;
            position = 0;
        }

        #region IOutputStream

        /// <summary>
        /// Write 8-bit unsigned integer
        /// </summary>
        public void WriteUInt8(byte value)
        {
            if (position >= length)
            {
                Grow(sizeof(byte));
            }

            buffer[position++] = value;
        }

        /// <summary>
        /// Write little-endian encoded 16-bit unsigned integer
        /// </summary>
        public void WriteUInt16(ushort value)
        {
            if (position + sizeof(ushort) > length)
            {
                Grow(sizeof(ushort));
            }

            var i = position;
            var b = buffer;
            b[i++] = (byte) value;
            b[i++] = (byte) (value >> 8);
            position = i;
        }

        /// <summary>
        /// Write little-endian encoded 32-bit unsigned integer
        /// </summary>
        public virtual void WriteUInt32(uint value)
        {
            if (position + sizeof(uint) > length)
            {
                Grow(sizeof(uint));
            }

            var i = position;
            var b = buffer;
            b[i++] = (byte) value;
            b[i++] = (byte) (value >> 8);
            b[i++] = (byte) (value >> 16);
            b[i++] = (byte) (value >> 24);
            position = i;
        }

        /// <summary>
        /// Write little-endian encoded 64-bit unsigned integer
        /// </summary>
        public virtual void WriteUInt64(ulong value)
        {
            if (position + sizeof(ulong) > length)
            {
                Grow(sizeof(ulong));
            }

            var i = position;
            var b = buffer;
            b[i++] = (byte) value;
            b[i++] = (byte) (value >> 8);
            b[i++] = (byte) (value >> 16);
            b[i++] = (byte) (value >> 24);
            b[i++] = (byte) (value >> 32);
            b[i++] = (byte) (value >> 40);
            b[i++] = (byte) (value >> 48);
            b[i++] = (byte) (value >> 56);
            position = i;
        }

        /// <summary>
        /// Write little-endian encoded single precision IEEE 754 float
        /// </summary>
        public virtual void WriteFloat(float value)
        {
            WriteUInt32(new FloatLayout {value = value}.bytes);
        }

        /// <summary>
        /// Write little-endian encoded double precision IEEE 754 float
        /// </summary>
        public virtual void WriteDouble(double value)
        {
            WriteUInt64(new DoubleLayout {value = value}.bytes);
        }

        /// <summary>
        /// Write an array of bytes verbatim
        /// </summary>
        /// <param name="data">Array segment specifying bytes to write</param>
        public virtual void WriteBytes(ArraySegment<byte> data)
        {
            var newOffset = position + data.Count;
            if (newOffset > length)
            {
                Grow(data.Count);
            }

            if (data.Count < BlockCopyMin)
            {
                for (int i = position, j = data.Offset; i < newOffset; ++i, ++j)
                {
                    buffer[i] = data.Array[j];
                }
            }
            else
            {
                Buffer.BlockCopy(data.Array, data.Offset, buffer, position, data.Count);
            }

            position = newOffset;
        }

        /// <summary>
        /// Write variable encoded 16-bit unsigned integer
        /// </summary>
        public void WriteVarUInt16(ushort value)
        {
            if (position + IntegerHelper.MaxBytesVarInt16 > length)
            {
                Grow(IntegerHelper.MaxBytesVarInt16);
            }

            position = IntegerHelper.EncodeVarUInt16(buffer, value, position);
        }

        /// <summary>
        /// Write variable encoded 32-bit unsigned integer
        /// </summary>
        public void WriteVarUInt32(uint value)
        {
            if (position + IntegerHelper.MaxBytesVarInt32 > length)
            {
                Grow(IntegerHelper.MaxBytesVarInt32);
            }

            position = IntegerHelper.EncodeVarUInt32(buffer, value, position);
        }

        /// <summary>
        /// Write variable encoded 64-bit unsigned integer
        /// </summary>
        public void WriteVarUInt64(ulong value)
        {
            if (position + IntegerHelper.MaxBytesVarInt64 > length)
            {
                Grow(IntegerHelper.MaxBytesVarInt64);
            }

            position = IntegerHelper.EncodeVarUInt64(buffer, value, position);
        }


        /// <summary>
        /// Write UTF-8 or UTF-16 encoded string
        /// </summary>
        /// <param name="encoding">String encoding</param>
        /// <param name="value">String value</param>
        /// <param name="size">Size in bytes of encoded string</param>
        public virtual void WriteString(Encoding encoding, string value, int size)
        {
            if (position + size > length)
            {
                Grow(size);
            }

            position += encoding.GetBytes(value, 0, value.Length, buffer, position);
        }

        #endregion

        // Grow the buffer so that there is enough space to write 'count' bytes
        internal virtual void Grow(int count)
        {
            var minLength = position + count;
            length += length >> 1;
            if (length < minLength) length = minLength;

            Array.Resize(ref buffer, length);
        }

        #region layouts

        [StructLayout(LayoutKind.Explicit)]
        struct DoubleLayout
        {
            [FieldOffset(0)] public readonly ulong bytes;

            [FieldOffset(0)] public double value;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct FloatLayout
        {
            [FieldOffset(0)] public readonly uint bytes;

            [FieldOffset(0)] public float value;
        }

        #endregion
    }
}
