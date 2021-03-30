// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal readonly struct RazorInputItem
    {
        public RazorInputItem(AdditionalText additionalText, string relativePath, string fileKind, string? cssScope)
        {
            AdditionalText = additionalText;
            RelativePath = relativePath;
            CssScope = cssScope;
            NormalizedPath = '/' + relativePath
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace("//", "/");
            FileKind = fileKind;
        }

        public AdditionalText AdditionalText { get; }

        public string RelativePath { get; }

        public string NormalizedPath { get; }

        public string FileKind { get; }

        public string? CssScope { get; }
    }
}
