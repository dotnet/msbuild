// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging
{
    internal class TerminalBufferLine
    {
        private static volatile int s_counter = 0;

        private readonly bool _shouldWrapLines;

        private List<string> _wrappedTextList = new();

        private string _text = string.Empty;

        internal TerminalBufferLine()
            : this(string.Empty, false)
        {
        }

        internal TerminalBufferLine(string text, bool shouldWrapLines)
        {
            Id = s_counter++;
            _shouldWrapLines = shouldWrapLines;
            Text = text;
        }

        internal int Id { get; }

        internal int WrappedTextCount => _wrappedTextList.Count;

        internal string Text
        {
            get => _text;
            set
            {
                // Set text value and get wrapped lines
                _text = value;
                _wrappedTextList = _shouldWrapLines
                    ? AnsiBuilder.AnsiWrap(value, Console.BufferWidth)
                    : new List<string> { value };

                // Buffer should rerender
                TerminalBuffer.ShouldRerender = true;
            }
        }

        internal int WrappedTextItemsCount => _wrappedTextList.Count;

        internal IEnumerable<string> GetWrappedTextItems()
        {
            foreach (string x in _wrappedTextList)
            {
                yield return x;
            }
        }
    }
}
