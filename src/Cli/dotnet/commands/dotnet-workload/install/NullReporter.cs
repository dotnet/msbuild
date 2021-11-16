// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class NullReporter : IReporter
    {
        public void Write(string message) { }
        public void WriteLine(string message) { }
        public void WriteLine() { }
    }
}
