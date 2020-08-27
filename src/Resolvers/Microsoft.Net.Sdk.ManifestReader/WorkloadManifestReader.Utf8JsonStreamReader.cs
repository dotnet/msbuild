using System;
using System.Buffers;
using System.IO;
using System.Text.Json;

namespace Microsoft.Net.Sdk.WorkloadManifestReader
{
    public partial class WorkloadManifestReader
    {
        ref struct Utf8JsonStreamReader
        {
            private const int segmentSize = 4096;
            private Utf8JsonReader reader;
            private readonly Stream stream;
            private byte[] buffer;
            private Span<byte> span;

            public Utf8JsonStreamReader(Stream stream, JsonReaderOptions readerOptions)
            {
                this.stream = stream;

                buffer = ArrayPool<byte>.Shared.Rent(segmentSize);
                var readCount = stream.Read(buffer, 0, buffer.Length);
                span = new Span<byte>(buffer, 0, readCount);

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

                    var newBuffer = ArrayPool<byte>.Shared.Rent(newSegmentSize);

                    int remaining = (int)(span.Length - reader.BytesConsumed);

                    if (remaining > 0)
                    {
                        span.Slice((int)reader.BytesConsumed).CopyTo(newBuffer);
                    }

                    var readCount = stream.Read(newBuffer, 0 , newBuffer.Length);

                    buffer = newBuffer;
                    span = newBuffer.AsMemory().Slice(0, remaining + readCount).Span;

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
