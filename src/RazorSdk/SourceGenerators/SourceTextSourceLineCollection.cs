// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public class SourceTextSourceLineCollection : RazorSourceLineCollection
    {
        private readonly string _filePath;
        private readonly TextLineCollection _textLines;

        public SourceTextSourceLineCollection(string filePath, TextLineCollection textLines)
        {
            _filePath = filePath;
            _textLines = textLines;
        }

        public override int Count => _textLines.Count;

        public override int GetLineLength(int index)
        {
            var line = _textLines[index];
            return line.EndIncludingLineBreak - line.Start;
        }

        internal override SourceLocation GetLocation(int position)
        {
            var line = _textLines.GetLineFromPosition(position);
            return new SourceLocation(_filePath, position, line.LineNumber, position - line.Start);
        }
    }
}
