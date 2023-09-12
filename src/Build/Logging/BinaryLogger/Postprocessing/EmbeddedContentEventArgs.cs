// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Logging
{
    public sealed class EmbeddedContentEventArgs : EventArgs
    {
        public EmbeddedContentEventArgs(EmbeddedContentKind contentKind, Stream contentStream, int length)
        {
            ContentKind = contentKind;
            ContentStream = contentStream;
            Length = length;
        }

        public EmbeddedContentKind ContentKind { get; }
        public Stream ContentStream { get; }
        public int Length { get; }
    }
}
