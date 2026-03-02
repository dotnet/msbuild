// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class ProcessExtensions_Tests
    {
        [Fact]
        public async Task KillTree()
        {
            var psi =
                NativeMethodsShared.IsWindows ?
                    new ProcessStartInfo("rundll32", "kernel32.dll, Sleep") :
                    new ProcessStartInfo("sleep", "600");

            Process p = Process.Start(psi); // sleep 10m.

            // Verify the process is running.
            await Task.Delay(500);
            p.HasExited.ShouldBe(false);

            // Kill the process.
            p.KillTree(timeoutMilliseconds: 5000);
            p.HasExited.ShouldBe(true);
            p.ExitCode.ShouldNotBe(0);
        }
    }
}
