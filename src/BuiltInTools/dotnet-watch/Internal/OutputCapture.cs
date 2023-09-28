// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable


namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class OutputCapture
    {
        private readonly List<string> _lines = new();
        public IEnumerable<string> Lines => _lines;
        public void AddLine(string line) => _lines.Add(line);
    }
}
