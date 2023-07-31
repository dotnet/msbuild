// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.Versioning;

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
