using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.DotNet.Cli.Utils
{
    public sealed class StreamForwarder
    {
        private const int DefaultBufferSize = 256;

        private readonly int _bufferSize;
        private StringBuilder _builder;
        private StringWriter _capture;
        private Action<string> _write;
        private Action<string> _writeLine;

        public StreamForwarder(int bufferSize = DefaultBufferSize)
        {
            _bufferSize = bufferSize;
        }

        public void Capture()
        {
            if (_capture != null)
            {
                throw new InvalidOperationException("Already capturing stream!");
            }
            _capture = new StringWriter();
        }

        public string GetCapturedOutput()
        {
            return _capture?.GetStringBuilder()?.ToString();
        }

        public void ForwardTo(Action<string> write, Action<string> writeLine)
        {
            if (writeLine == null)
            {
                throw new ArgumentNullException(nameof(writeLine));
            }
            if (_writeLine != null)
            {
                throw new InvalidOperationException("Already handling stream!");
            }
            _write = write;
            _writeLine = writeLine;
        }

        public Thread BeginRead(TextReader reader)
        {
            var thread = new Thread(() => Read(reader)) { IsBackground = true };
            thread.Start();
            return thread;
        }

        public void Read(TextReader reader)
        {
            _builder = new StringBuilder();
            var buffer = new char[_bufferSize];
            int n;
            while ((n = reader.Read(buffer, 0, _bufferSize)) > 0)
            {
                _builder.Append(buffer, 0, n);
                WriteBlocks();
            }
            WriteRemainder();
        }

        private void WriteBlocks()
        {
            int n = _builder.Length;
            if (n == 0)
            {
                return;
            }

            int offset = 0;
            bool sawReturn = false;
            for (int i = 0; i < n; i++)
            {
                char c = _builder[i];
                switch (c)
                {
                    case '\r':
                        sawReturn = true;
                        continue;
                    case '\n':
                        WriteLine(_builder.ToString(offset, i - offset - (sawReturn ? 1 : 0)));
                        offset = i + 1;
                        break;
                }
                sawReturn = false;
            }

            // If the buffer contains no line breaks and _write is
            // supported, send the buffer content.
            if (!sawReturn &&
                (offset == 0) &&
                ((_write != null) || (_writeLine == null)))
            {
                WriteRemainder();
            }
            else
            {
                _builder.Remove(0, offset);
            }
        }

        private void WriteRemainder()
        {
            if (_builder.Length == 0)
            {
                return;
            }
            Write(_builder.ToString());
            _builder.Clear();
        }

        private void WriteLine(string str)
        {
            if (_capture != null)
            {
                _capture.WriteLine(str);
            }
            // If _write is supported, so is _writeLine.
            if (_writeLine != null)
            {
                _writeLine(str);
            }
        }

        private void Write(string str)
        {
            if (_capture != null)
            {
                _capture.Write(str);
            }
            if (_write != null)
            {
                _write(str);
            }
            else if (_writeLine != null)
            {
                _writeLine(str);
            }
        }
    }
}