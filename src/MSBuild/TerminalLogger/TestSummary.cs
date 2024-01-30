// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging.TerminalLogger
{
    internal class TestSummary
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
