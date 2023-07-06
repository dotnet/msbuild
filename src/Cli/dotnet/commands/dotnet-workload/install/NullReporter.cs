// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class NullReporter : IReporter
    {
        public void Write(string message) { }
        public void WriteLine(string message) { }
        public void WriteLine() { }

        public void WriteLine(string format, params object?[] args) => WriteLine(string.Format(format, args));
    }
}
