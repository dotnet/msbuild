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

        [DotNetOnlyFact]
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
                    // Windows is treated as an unsupported OS for command-line retrieval; commandLine is null.
                    commandLine.ShouldBeNull();
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

        [DotNetOnlyFact]
        public async Task TryGetCommandLine_RunningProcess_ContainsArguments()
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
                    // Windows is treated as an unsupported OS for command-line retrieval; commandLine is null.
                    commandLine.ShouldBeNull();
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

        [Fact]
        public void TryGetCommandLine_NullProcess_ReturnsFalse()
        {
            Process process = null;
            process.TryGetCommandLine(out string commandLine).ShouldBeFalse();
            commandLine.ShouldBeNull();
        }

        [Fact]
        public void TryGetCommandLine_ExitedProcess_ReturnsFalse()
        {
            var psi = NativeMethodsShared.IsWindows
                ? new ProcessStartInfo("cmd.exe", "/c echo hello")
                : new ProcessStartInfo("echo", "hello");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using Process p = Process.Start(psi);
            p.WaitForExit(10000);
            p.HasExited.ShouldBeTrue();

            p.TryGetCommandLine(out string commandLine).ShouldBeFalse();
            commandLine.ShouldBeNull();
        }

#if FEATURE_WINDOWSINTEROP && NET
        [WindowsOnlyFact]
        public async Task GetCommandLine_ViaWmi_ContainsExpectedExecutable()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                var sw = Stopwatch.StartNew();
                p.TryGetCommandLine(ProcessExtensions.CommandLineSource.Wmi, out string commandLine).ShouldBeTrue();
                sw.Stop();
                _output.WriteLine($"WMI GetCommandLine elapsed: {sw.Elapsed.TotalMilliseconds:F2} ms");

                commandLine.ShouldNotBeNull();
                commandLine.ShouldContain("ping", Case.Insensitive);
            }
            finally
            {
                if (!p.HasExited)
                {
                    p.KillTree(5000);
                }
            }
        }

        [WindowsOnlyFact]
        public async Task GetCommandLine_ViaWmi_ContainsArguments()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                p.TryGetCommandLine(ProcessExtensions.CommandLineSource.Wmi, out string commandLine).ShouldBeTrue();

                commandLine.ShouldNotBeNull();
                commandLine.ShouldMatch(@"(127\.0\.0\.1|31)");
            }
            finally
            {
                if (!p.HasExited)
                {
                    p.KillTree(5000);
                }
            }
        }

        [WindowsOnlyFact]
        public async Task GetCommandLine_ViaDebugEngine_ContainsExpectedExecutable()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                var sw = Stopwatch.StartNew();
                p.TryGetCommandLine(ProcessExtensions.CommandLineSource.DebugEngine, out string commandLine).ShouldBeTrue();
                sw.Stop();
                _output.WriteLine($"DebugEngine GetCommandLine elapsed: {sw.Elapsed.TotalMilliseconds:F2} ms");

                // DebugEngine may return a non-null result that contains the executable name.
                // For protected or system processes it may fall back to the exe path.
                commandLine.ShouldNotBeNull();
                commandLine.ShouldContain("ping", Case.Insensitive);
            }
            finally
            {
                if (!p.HasExited)
                {
                    p.KillTree(5000);
                }
            }
        }

        [WindowsOnlyFact]
        public async Task GetCommandLine_ViaDebugEngine_ContainsArguments()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                p.TryGetCommandLine(ProcessExtensions.CommandLineSource.DebugEngine, out string commandLine).ShouldBeTrue();

                commandLine.ShouldNotBeNull();
                // DebugEngine description should include command line arguments
                commandLine.ShouldMatch(@"(127\.0\.0\.1|31)");
            }
            finally
            {
                if (!p.HasExited)
                {
                    p.KillTree(5000);
                }
            }
        }

        [WindowsOnlyFact]
        public async Task GetCommandLine_BothSources_ReturnEquivalentResults()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                p.TryGetCommandLine(ProcessExtensions.CommandLineSource.Wmi, out string wmiResult).ShouldBeTrue();
                p.TryGetCommandLine(ProcessExtensions.CommandLineSource.DebugEngine, out string debugResult).ShouldBeTrue();

                _output.WriteLine($"WMI result: {wmiResult}");
                _output.WriteLine($"DebugEngine result: {debugResult}");

                // Both should be non-null and contain "ping"
                wmiResult.ShouldNotBeNull();
                debugResult.ShouldNotBeNull();
                wmiResult.ShouldContain("ping", Case.Insensitive);
                debugResult.ShouldContain("ping", Case.Insensitive);

                // Both should contain the same target address or timeout argument
                wmiResult.ShouldMatch(@"(127\.0\.0\.1|31)");
                debugResult.ShouldMatch(@"(127\.0\.0\.1|31)");
            }
            finally
            {
                if (!p.HasExited)
                {
                    p.KillTree(5000);
                }
            }
        }

        [WindowsOnlyFact]
        public void GetCommandLine_ViaWmi_RunningProcess_ReturnsCommandLine()
        {
            using Process dummy = StartLongRunningProcess();
            try
            {
                dummy.TryGetCommandLine(ProcessExtensions.CommandLineSource.Wmi, out string commandLine).ShouldBeTrue();
                commandLine.ShouldNotBeNull();
                commandLine.ShouldContain("ping", Case.Insensitive);
            }
            finally
            {
                if (!dummy.HasExited)
                {
                    dummy.KillTree(5000);
                }
            }
        }

        [WindowsOnlyFact]
        public void GetCommandLine_ViaDebugEngine_RunningProcess_ReturnsCommandLine()
        {
            using Process dummy = StartLongRunningProcess();
            try
            {
                dummy.TryGetCommandLine(ProcessExtensions.CommandLineSource.DebugEngine, out string commandLine).ShouldBeTrue();
                commandLine.ShouldNotBeNull();
                commandLine.ShouldContain("ping", Case.Insensitive);
            }
            finally
            {
                if (!dummy.HasExited)
                {
                    dummy.KillTree(5000);
                }
            }
        }

        [WindowsOnlyFact]
        public void TryGetCommandLine_WithSource_NullProcess_ReturnsFalse()
        {
            Process process = null;
            process.TryGetCommandLine(ProcessExtensions.CommandLineSource.Wmi, out string commandLine).ShouldBeFalse();
            commandLine.ShouldBeNull();
        }

        [WindowsOnlyFact]
        public void TryGetCommandLine_WithSource_ExitedProcess_ReturnsFalse()
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c echo hello")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process p = Process.Start(psi);
            p.WaitForExit(10000);
            p.HasExited.ShouldBeTrue();

            p.TryGetCommandLine(ProcessExtensions.CommandLineSource.Wmi, out string commandLine).ShouldBeFalse();
            commandLine.ShouldBeNull();

            p.TryGetCommandLine(ProcessExtensions.CommandLineSource.DebugEngine, out commandLine).ShouldBeFalse();
            commandLine.ShouldBeNull();
        }
#endif
    }
}
