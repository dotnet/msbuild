// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class ProcessExtensions_Tests
    {
        private readonly ITestOutputHelper _output;

        public ProcessExtensions_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static Process StartLongRunningProcess()
        {
            var psi = NativeMethodsShared.IsWindows
                ? new ProcessStartInfo("ping", "-n 31 127.0.0.1")
                : new ProcessStartInfo("sleep", "30");
            psi.UseShellExecute = false;
            return Process.Start(psi);
        }

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
        public async Task TryGetCommandLine_RunningProcess_ContainsExpectedExecutable()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                var sw = Stopwatch.StartNew();
                p.TryGetCommandLine(out string commandLine).ShouldBeTrue();
                sw.Stop();
                _output.WriteLine($"TryGetCommandLine elapsed: {sw.Elapsed.TotalMilliseconds:F2} ms");

                if (NativeMethodsShared.IsWindows)
                {
                    commandLine.ShouldContain("ping", Case.Insensitive);
                }
                else
                {
                    commandLine.ShouldContain("sleep");
                }
            }
            finally
            {
                if (!p.HasExited)
                {
                    p.KillTree(5000);
                }
            }
        }

        [Fact]
        public async Task TryGetCommandLine_RunningProcess_ContainsArguments()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                var sw = Stopwatch.StartNew();
                p.TryGetCommandLine(out string commandLine);
                sw.Stop();
                _output.WriteLine($"TryGetCommandLine elapsed: {sw.Elapsed.TotalMilliseconds:F2} ms");

                if (NativeMethodsShared.IsWindows)
                {
                    // ping -n 31 127.0.0.1 – at minimum "127.0.0.1" or "31" should appear
                    commandLine.ShouldMatch(@"(127\.0\.0\.1|31)");
                }
                else
                {
                    commandLine.ShouldContain("30");
                }
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
