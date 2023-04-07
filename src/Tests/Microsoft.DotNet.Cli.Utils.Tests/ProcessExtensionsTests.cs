// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
#if NET
    [SupportedOSPlatform("windows")]
#endif
    public class ProcessExtensionsTests
    {
        [WindowsOnlyFact]
        public void ItReturnsTheParentProcessId()
        {
            int expectedParentProcessId = Process.GetCurrentProcess().Id;

            using Process childProcess = new();
            childProcess.StartInfo.CreateNoWindow = true;
            childProcess.StartInfo.FileName = "cmd";
            childProcess.Start();
            Process parentProcess = childProcess.GetParentProcess();
            int ppid = parentProcess.Id;
            childProcess.Kill();

            Assert.Equal(expectedParentProcessId, ppid);
        }
    }
}
