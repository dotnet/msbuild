using System;
using System.IO;

namespace dotnet_new3
{
    public class CoDisposableStream :Stream
    {
        private readonly IDisposable[] _alsoDispose;
        private readonly Stream _source;

        public CoDisposableStream(Stream source, params IDisposable[] alsoDispose)
        {
            _source = source;
            _alsoDispose = alsoDispose;
        }

        public override bool CanRead => _source.CanRead;

        public override bool CanSeek => _source.CanSeek;

        public override bool CanWrite => _source.CanWrite;

        public override long Length => _source.Length;

        public override long Position
        {
            get { return _source.Position; }
            set { _source.Position = value; }
        }

        public override void Flush() => _source.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _source.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _source.Seek(offset, origin);

        public override void SetLength(long value) => _source.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => _source.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            foreach(IDisposable disposable in _alsoDispose)
            {
                disposable.Dispose();
            }

            _source.Dispose();
        }
    }
}