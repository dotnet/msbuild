// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    public sealed class StreamForwarder
    {
        private static readonly char[] s_ignoreCharacters = new char[] { '\r' };
        private static readonly char s_flushBuilderCharacter = '\n';

        private StringBuilder _builder;
        private StringWriter _capture;
        private Action<string> _writeLine;
        private bool _trimTrailingCapturedNewline;

        public string CapturedOutput
        {
            get
            {
                var capture = _capture?.GetStringBuilder()?.ToString();
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
            ThrowIfNull(writeLine);

            ThrowIfForwarderSet();

            _writeLine = writeLine;

            return this;
        }

        public Task BeginRead(TextReader reader)
        {
            return Task.Run(() => Read(reader));
        }

        public void Read(TextReader reader)
        {
            var bufferSize = 1;

            int readCharacterCount;
            char currentCharacter;

            var buffer = new char[bufferSize];
            _builder = new StringBuilder();

            // Using Read with buffer size 1 to prevent looping endlessly
            // like we would when using Read() with no buffer
            while ((readCharacterCount = reader.Read(buffer, 0, bufferSize)) > 0)
            {
                currentCharacter = buffer[0];

                if (currentCharacter == s_flushBuilderCharacter)
                {
                    WriteBuilder();
                }
                else if (!s_ignoreCharacters.Contains(currentCharacter))
                {
                    _builder.Append(currentCharacter);
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
            WriteLine(_builder.ToString());
            _builder.Clear();
        }

        private void WriteLine(string str)
        {
            if (_capture != null)
            {
                _capture.WriteLine(str);
            }

            if (_writeLine != null)
            {
                _writeLine(str);
            }
        }

        private void ThrowIfNull(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
        }

        private void ThrowIfForwarderSet()
        {
            if (_writeLine != null)
            {
                throw new InvalidOperationException(LocalizableStrings.WriteLineForwarderSetPreviously);
            }
        }

        private void ThrowIfCaptureSet()
        {
            if (_capture != null)
            {
                throw new InvalidOperationException(LocalizableStrings.AlreadyCapturingStream);
            }
        }
    }
}
