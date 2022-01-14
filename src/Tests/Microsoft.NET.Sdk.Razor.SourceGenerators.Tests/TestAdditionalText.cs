// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
 
namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public sealed class TestAdditionalText : AdditionalText
    {
        private readonly SourceText _text;
 
        public TestAdditionalText(string path, SourceText text)
        {
            Path = path;
            _text = text;
        }
 
        public TestAdditionalText(string text = "", Encoding encoding = null, string path = "dummy")
            : this(path, SourceText.From(text, encoding))
        {
        }
 
        public override string Path { get; }
 
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
