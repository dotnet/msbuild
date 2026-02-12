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

        [Fact]
        public void GetCommandLine_ReturnsNullForNullProcess()
        {
            Process process = null;
            string commandLine = process.GetCommandLine();
            commandLine.ShouldBeNull();
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

                // On some platforms/configurations, we might not be able to get the command line
                // (e.g., .NET Core on Windows without WMI package)
                // So we just verify it doesn't throw and returns either a string or null
                if (commandLine != null)
                {
                    commandLine.ShouldNotBeEmpty();
                    
                    // On Unix, we should be able to get the command line from /proc
                    if (!NativeMethodsShared.IsWindows)
                    {
                        commandLine.ShouldContain("sleep");
                    }
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

        [WindowsOnlyFact]
        public void GetCommandLine_ReturnsNullForExitedProcess()
        {
            // Start and immediately exit a process
            var psi = new ProcessStartInfo("cmd.exe", "/c exit 0")
            {
                UseShellExecute = false
            };

            using Process p = Process.Start(psi);
            p.WaitForExit(5000);

            string commandLine = p.GetCommandLine();
            
            // Command line should be null for an exited process
            commandLine.ShouldBeNull();
        }

        [UnixOnlyFact]
        public async Task GetCommandLine_WorksOnUnix()
        {
            // On Unix, we should be able to read from /proc
            var psi = new ProcessStartInfo("sleep", "10")
            {
                UseShellExecute = false
            };

            using Process p = Process.Start(psi);
            try
            {
                await Task.Delay(500);

                string commandLine = p.GetCommandLine();
                commandLine.ShouldNotBeNull();
                commandLine.ShouldNotBeEmpty();
                commandLine.ShouldContain("sleep");
            }
            finally
            {
                if (!p.HasExited)
                {
                    p.KillTree(5000);
                }
            }
        }
    }
}
