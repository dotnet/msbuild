// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class LogEntry
    {
        public string EventName { get; set; }
        public IDictionary<string, string> Properties { get; set; }
        public IDictionary<string, double> Measurement { get; set; }
    }

}
