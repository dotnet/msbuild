// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging.TerminalLogger
{
    internal readonly record struct TestSummary(int Total, int Passed, int Skipped, int Failed);
}
