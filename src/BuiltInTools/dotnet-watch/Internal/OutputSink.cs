// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class OutputSink
    {
        public OutputCapture Current { get; private set; }
        public OutputCapture StartCapture()
        {
            return (Current = new OutputCapture());
        }
    }
}
