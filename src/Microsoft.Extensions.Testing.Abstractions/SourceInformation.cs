// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Extensions.Testing.Abstractions
{
    public class SourceInformation
    {
        public SourceInformation(string filename, int lineNumber)
        {
            Filename = filename;
            LineNumber = lineNumber;
        }

        public string Filename { get; }

        public int LineNumber { get; }
    }
}