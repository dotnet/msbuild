using System;
using System.Buffers;
using System.IO;
using System.Text.Json;

//#define HAS_STREAM_READ_SPAN

namespace Microsoft.Net.Sdk.WorkloadManifestReader
{
    partial class WorkloadManifestReader
    {
        ref struct Utf8JsonStreamReader
        {
            const int segmentSize = 4096;

            Utf8JsonReader reader;
            readonly Stream stream;

#if HAS_STREAM_READ_SPAN
            IMemoryOwner<byte> buffer;
#else
            byte[] buffer;
#endif

            Span<byte> span;

            public Utf8JsonStreamReader(Stream stream, JsonReaderOptions readerOptions)
            {
                this.stream = stream;

#if HAS_STREAM_READ_SPAN
                buffer = MemoryPool<byte>.Shared.Rent(segmentSize);
                var readCount = stream.Read(buffer.Memory.Span);
                span = buffer.Memory.Slice(0, readCount).Span;
#else
                buffer = ArrayPool<byte>.Shared.Rent(segmentSize);
                var readCount = stream.Read(buffer, 0, buffer.Length);
                span = new Span<byte>(buffer, 0, readCount);
#endif

                if (span.StartsWith(utf8Bom))
                {
                    span = span.Slice(utf8Bom.Length, span.Length - utf8Bom.Length);
                }

                reader = new Utf8JsonReader(span, readCount >= stream.Length, new JsonReaderState(readerOptions));
            }

            public bool Read()
            {
                while (!reader.Read())
                {
                    if (reader.IsFinalBlock)
                    {
                        return false;
                    }

                    var newSegmentSize = segmentSize;

                    // if the value was too big to fit in the buffer, get a bigger buffer
                    if (reader.BytesConsumed == span.Length)
                    {
                        newSegmentSize = span.Length * 2;
                    }

                    int remaining = (int) (span.Length - reader.BytesConsumed);

#if HAS_STREAM_READ_SPAN
                    var newBuffer = MemoryPool<byte>.Shared.Rent(newSegmentSize);

                    if (remaining > 0)
                    {
                        span.Slice((int)reader.BytesConsumed).CopyTo(newBuffer.Memory.Span);
                    }

                    var readCount = stream.Read(newBuffer.Memory.Span.Slice (remaining));

                    buffer.Dispose();
                    buffer = newBuffer;
                    span = newBuffer.Memory.Slice(0, remaining + readCount).Span;
#else
                    var newBuffer = ArrayPool<byte>.Shared.Rent(newSegmentSize);

                    if (remaining > 0)
                    {
                        span.Slice((int)reader.BytesConsumed).CopyTo(newBuffer);
                    }

                    var readCount = stream.Read(newBuffer, remaining, newBuffer.Length - remaining);

                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = newBuffer;
                    span = new Span<byte> (newBuffer, 0, remaining + readCount);
#endif

                    reader = new Utf8JsonReader(span, stream.Position >= stream.Length, reader.CurrentState);
                }

                return true;
            }

            public long TokenStartIndex => reader.TokenStartIndex;

            public JsonTokenType TokenType => reader.TokenType;

            public int CurrentDepth => reader.CurrentDepth;

            public string GetString() => reader.GetString();
            public long GetInt64() => reader.GetInt64();
        }
    }
}
