// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class LogEntry
    {
        public string EventName { get; set; }
        public IDictionary<string, string> Properties { get; set; }
        public IDictionary<string, double> Measurement { get; set; }
    }

}
