using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Implements IOutputStream on top of System.Stream
    /// </summary>
    internal class OutputStream : OutputBuffer
    {
        readonly Stream stream;

        public override long Position
        {
            get { return stream.Position + position; }
            set
            {
                Flush();
                stream.Position = value;
            }
        }

        public OutputStream(Stream stream, int bufferLength = 64 * 1024)
            : base(bufferLength)
        {
            this.stream = stream;
        }

        /// <summary> 
        /// Flush the buffer to the stream.
        /// </summary>
        /// <remarks> 
        /// Does NOT flush the stream. That may not be advisable unless you
        /// know the alignment and transactional behavior of the storage
        /// medium, so the caller remains separately responsible for that logic
        /// if they need it.
        /// </remarks>
        public void Flush()
        {
            stream.Write(buffer, 0, position);
            position = 0;
        }

        /// <summary>
        /// Write an array of bytes verbatim
        /// </summary>
        /// <param name="data">Array segment specifying bytes to write</param>
        public override void WriteBytes(ArraySegment<byte> data)
        {
            Flush();
            stream.Write(data.Array, data.Offset, data.Count);
        }

        /// <summary>
        /// Write UTF-8 or UTF-16 encoded string
        /// </summary>
        /// <param name="encoding">String encoding</param>
        /// <param name="value">String value</param>
        /// <param name="size">Size in bytes of encoded string</param>
        public override void WriteString(Encoding encoding, string value, int size)
        {
            if (position + size > length)
            {
                Flush();
                var data = new byte[size];
                encoding.GetBytes(value, 0, value.Length, data, 0);
                stream.Write(data, 0, size);
            }
            else
            {
                base.WriteString(encoding, value, size);
            }
        }

        internal override void Grow(int minLength)
        {
            Debug.Assert(minLength <= buffer.Length);
            Flush();
        }
    }
}
