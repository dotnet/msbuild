// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Logging
{
    internal sealed class EmbeddedContentEventArgs : EventArgs
    {
        public EmbeddedContentEventArgs(EmbeddedContentKind contentKind, Stream contentStream)
        {
            ContentKind = contentKind;
            ContentStream = contentStream;
        }

        public EmbeddedContentKind ContentKind { get; }
        public Stream ContentStream { get; }
    }
}
