// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.DotNet.CommandUtils
{
    internal sealed class StreamForwarder
    {
        private const char FlushBuilderCharacter = '\n';
        private static readonly char[] IgnoreCharacters = new char[] { '\r' };

        private StringBuilder? _builder;
        private StringWriter? _capture;
        private Action<string>? _writeLine;
        private bool _trimTrailingCapturedNewline;

        public string? CapturedOutput
        {
            get
            {
                string? capture = _capture?.GetStringBuilder()?.ToString();
                if (_trimTrailingCapturedNewline)
                {
                    capture = capture?.TrimEnd('\r', '\n');
                }
                return capture;
            }
        }

        public StreamForwarder Capture(bool trimTrailingNewline = false)
        {
            ThrowIfCaptureSet();

            _capture = new StringWriter();
            _trimTrailingCapturedNewline = trimTrailingNewline;

            return this;
        }

        public StreamForwarder ForwardTo(Action<string> writeLine)
        {
#if NET
            ArgumentNullException.ThrowIfNull(writeLine);
#else
            if (writeLine is null)
            {
                throw new ArgumentNullException(nameof(writeLine));
            }
#endif

            ThrowIfForwarderSet();

            _writeLine = writeLine;

            return this;
        }

        public Task BeginRead(TextReader reader) => Task.Run(() => Read(reader));

        public void Read(TextReader reader)
        {
            int bufferSize = 1;
            char currentCharacter;

            char[] buffer = new char[bufferSize];
            _builder = new StringBuilder();

            // Using Read with buffer size 1 to prevent looping endlessly
            // like we would when using Read() with no buffer
            while ((_ = reader.Read(buffer, 0, bufferSize)) > 0)
            {
                currentCharacter = buffer[0];

                if (currentCharacter == FlushBuilderCharacter)
                {
                    WriteBuilder();
                }
                else if (!IgnoreCharacters.Contains(currentCharacter))
                {
                    _ = _builder.Append(currentCharacter);
                }
            }

            // Flush anything else when the stream is closed
            // Which should only happen if someone used console.Write
            if (_builder.Length > 0)
            {
                WriteBuilder();
            }
        }

        private void WriteBuilder()
        {
            WriteLine(_builder?.ToString());
            _ = (_builder?.Clear());
        }

        private void WriteLine(string? str)
        {
            _capture?.WriteLine(str);

            if (_writeLine != null && str != null)
            {
                _writeLine(str);
            }
        }

        private void ThrowIfForwarderSet()
        {
            if (_writeLine != null)
            {
                throw new InvalidOperationException("WriteLine forwarder set previously");
            }
        }

        private void ThrowIfCaptureSet()
        {
            if (_capture != null)
            {
                throw new InvalidOperationException("Already capturing stream!");
            }
        }
    }
}
