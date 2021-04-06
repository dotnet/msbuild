// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal class SourceTextRazorSourceDocument : RazorSourceDocument
    {
        private readonly SourceText _sourceText;

        public SourceTextRazorSourceDocument(string filePath, string relativePath, SourceText sourceText)
        {
            FilePath = filePath;
            RelativePath = relativePath;
            _sourceText = sourceText;
            Lines = new SourceTextSourceLineCollection(filePath, sourceText.Lines);
        }

        public override char this[int position] => _sourceText[position];

        public override Encoding? Encoding => _sourceText.Encoding;

        public override string FilePath { get; }

        public override int Length => _sourceText.Length;

        public override string RelativePath { get; }

        public override RazorSourceLineCollection Lines { get; }

        public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            _sourceText.CopyTo(sourceIndex, destination, destinationIndex, count);
        }

        public override byte[] GetChecksum() => _sourceText.GetChecksum().ToArray();

        public override string GetChecksumAlgorithm() => _sourceText.ChecksumAlgorithm.ToString().ToUpperInvariant();
    }
}
