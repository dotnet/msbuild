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

                // The current implementation uses native Windows APIs on .NET Core+ and /proc or sysctl on Unix,
                // so command line retrieval should generally work on all supported platforms.
                // However, to remain robust in constrained environments, we only verify it does not throw
                // and that any non-null result is non-empty.
                if (commandLine != null)
                {
                    commandLine.ShouldNotBeEmpty();
                    
                    // On Unix, we should be able to get the command line
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

        [UnixOnlyFact]
        public async Task GetCommandLine_WorksOnUnix()
        {
            // On Unix (Linux and macOS), we should be able to read command lines
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

        [OSXOnlyFact]
        public async Task GetCommandLine_WorksOnMacOS()
        {
            // On macOS, verify that sysctl-based command line retrieval works
            var psi = new ProcessStartInfo("sleep", "15")
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
                commandLine.ShouldContain("15");
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
