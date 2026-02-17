// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Build.Shared;
using Microsoft.DotNet.XUnitExtensions;
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

        [Fact]
        public async Task GetCommandLine_ReturnsCommandLineForRunningProcess()
        {
            // Start a simple process that will run for a bit
            var psi = NativeMethodsShared.IsWindows
                ? new ProcessStartInfo("cmd.exe", "/c timeout 10")
                : new ProcessStartInfo("sleep", "10");
            
            psi.UseShellExecute = false;

            using Process p = Process.Start(psi);
            try
            {
                // Give the process time to start
                await Task.Delay(500);

                string commandLine = p.GetCommandLine();

                // Command line retrieval should work on all platforms
                commandLine.ShouldNotBeNull();
                commandLine.ShouldNotBeEmpty();
                
                // Verify we get the expected process name
                if (NativeMethodsShared.IsWindows)
                {
                    commandLine.ShouldContain("cmd");
                }
                else
                {
                    commandLine.ShouldContain("sleep");
                    commandLine.ShouldContain("10");
                }
            }
            finally
            {
                // Clean up
                if (!p.HasExited)
                {
                    p.KillTree(5000);
                }
            }
        }
    }
}
