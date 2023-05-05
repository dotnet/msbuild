// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Logging.LiveLogger;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A test implementation of <see cref="ITerminal"/>.
    /// </summary>
    internal sealed class MockTerminal : ITerminal
    {
        private readonly int _width;
        private readonly int _height;

        /// <summary>
        /// Contains output lines written to the terminal.
        /// </summary>
        private List<string> _outputLines = new();

        private StringBuilder _bufferedOutput = new();
        private bool _isBuffering = false;

        public MockTerminal(int width, int height)
        {
            _width = width;
            _height = height;
            _outputLines.Add("");
        }

        /// <summary>
        /// Gets the last line written to the terminal.
        /// </summary>
        /// <remarks>
        /// If the last character was \n, it returns characters between the second to last \n and last \n.
        /// If the last character was not \n, it returns characters between the last \n and the end of the output.
        /// </remarks>
        public string GetLastLine()
        {
            string lastLine = _outputLines[^1];
            if (lastLine.Length == 0 && _outputLines.Count > 1)
            {
                lastLine = _outputLines[^2];
            }
            return lastLine;
        }

        /// <summary>
        /// Adds a string to <see cref="_outputLines"/>.
        /// </summary>
        private void AddOutput(string text)
        {
            if (_isBuffering)
            {
                _bufferedOutput.Append(text);
            }
            else
            {
                string[] lines = text.Split('\n');
                _outputLines[^1] += lines[0];
                for (int i = 1; i < lines.Length; i++)
                {
                    _outputLines.Add("");
                    _outputLines[^1] += lines[i];
                }
            }
        }

        #region ITerminal implementation

        public int Width => _width;
        public int Height => _height;

        public void BeginUpdate()
        {
            if (_isBuffering)
            {
                throw new InvalidOperationException();
            }
            _isBuffering = true;
        }

        public void EndUpdate()
        {
            if (!_isBuffering)
            {
                throw new InvalidOperationException();
            }
            _isBuffering = false;

            AddOutput(_bufferedOutput.ToString());
            _bufferedOutput.Clear();
        }

        public void Write(string text) => AddOutput(text);
        public void Write(ReadOnlySpan<char> text) { AddOutput(text.ToString()); }
        public void WriteColor(TerminalColor color, string text) => AddOutput(text);
        public void WriteColorLine(TerminalColor color, string text) { AddOutput(text); AddOutput("\n"); }

        public void WriteLine(string text) { AddOutput(text); AddOutput("\n"); }
        public void WriteLineFitToWidth(ReadOnlySpan<char> text)
        {
            AddOutput(text.Slice(0, Math.Min(text.Length, _width - 1)).ToString());
            AddOutput("\n");
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        { }

        #endregion
    }
}
