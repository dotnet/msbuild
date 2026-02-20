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
                ? new ProcessStartInfo("cmd.exe", "/c timeout /t 30 /nobreak")
                : new ProcessStartInfo("sleep", "30");
            psi.UseShellExecute = false;
            return Process.Start(psi);
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
                    commandLine.ShouldContain("cmd", Case.Insensitive);
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
                    // cmd /c timeout /t 30 /nobreak – at minimum "timeout" or "30" should appear
                    commandLine.ShouldMatch(@"(timeout|30)");
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
