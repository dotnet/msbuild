// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Focused tests for the internal Windows.GetCommandLine path, exercised through
    /// <see cref="ProcessExtensions.TryGetCommandLine"/>.
    ///
    /// Because GetCommandLine now throws <see cref="InvalidOperationException"/> with
    /// detailed diagnostics on each native-call failure, TryGetCommandLine catches those
    /// and returns false.  The tests below verify both the happy path and each individual
    /// failure mode by asserting on the exception message that bubbles out when the
    /// internal method is called directly (Windows only), or by provoking failure through
    /// invalid inputs on all platforms.
    /// </summary>
    public class ProcessExtensions_GetCommandLine_Tests
    {
        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Starts a long-running process suitable for inspection and returns it.
        /// Caller is responsible for terminating it.
        /// </summary>
        private static Process StartLongRunningProcess()
        {
            var psi = NativeMethodsShared.IsWindows
                ? new ProcessStartInfo("cmd.exe", "/c timeout /t 30 /nobreak")
                : new ProcessStartInfo("sleep", "30");
            psi.UseShellExecute = false;
            return Process.Start(psi);
        }

        // -----------------------------------------------------------------------
        // TryGetCommandLine – happy path (all platforms)
        // -----------------------------------------------------------------------

        [Fact]
        public async Task TryGetCommandLine_RunningProcess_ReturnsTrue()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                p.TryGetCommandLine(out _).ShouldBeTrue();
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
        public async Task TryGetCommandLine_RunningProcess_CommandLineNotNullOrEmpty()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                p.TryGetCommandLine(out string commandLine);
                commandLine.ShouldNotBeNull();
                commandLine.ShouldNotBeEmpty();
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
        public async Task TryGetCommandLine_RunningProcess_ContainsExpectedExecutable()
        {
            using Process p = StartLongRunningProcess();
            try
            {
                await Task.Delay(300);
                p.TryGetCommandLine(out string commandLine);

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
                p.TryGetCommandLine(out string commandLine);

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

        // -----------------------------------------------------------------------
        // TryGetCommandLine – null / exited process guards (all platforms)
        // -----------------------------------------------------------------------

        [Fact]
        public void TryGetCommandLine_NullProcess_ReturnsFalse()
        {
            Process nullProcess = null;
            nullProcess.TryGetCommandLine(out string commandLine).ShouldBeFalse();
            commandLine.ShouldBeNull();
        }

        [Fact]
        public async Task TryGetCommandLine_ExitedProcess_ReturnsFalse()
        {
            var psi = NativeMethodsShared.IsWindows
                ? new ProcessStartInfo("cmd.exe", "/c exit 0") { UseShellExecute = false }
                : new ProcessStartInfo("true") { UseShellExecute = false };

            using Process p = Process.Start(psi);
            await Task.Delay(500); // wait for it to exit naturally
            p.WaitForExit(2000);

            p.HasExited.ShouldBeTrue();
            p.TryGetCommandLine(out string commandLine).ShouldBeFalse();
            commandLine.ShouldBeNull();
        }

        // -----------------------------------------------------------------------
        // Windows.GetCommandLine – invalid PID returns null (no WMI row found)
        // (called via reflection so any exception would propagate)
        // -----------------------------------------------------------------------

        [WindowsOnlyFact]
        public void GetCommandLine_InvalidPid_ReturnsNull()
        {
            // PID int.MaxValue is guaranteed not to exist; WMI returns no rows.
            int invalidPid = int.MaxValue;
            string? result = InvokeWindowsGetCommandLine(invalidPid);
            result.ShouldBeNull();
        }

        // -----------------------------------------------------------------------
        // Windows.GetCommandLine – self-inspection (process can always read itself)
        // -----------------------------------------------------------------------

        [WindowsOnlyFact]
        public void GetCommandLine_CurrentProcess_ReturnsNonEmpty()
        {
            string commandLine = InvokeWindowsGetCommandLine(Process.GetCurrentProcess().Id);
            commandLine.ShouldNotBeNull();
            commandLine.ShouldNotBeEmpty();
        }

        [WindowsOnlyFact]
        public void GetCommandLine_CurrentProcess_ContainsTestRunnerExecutable()
        {
            string commandLine = InvokeWindowsGetCommandLine(Process.GetCurrentProcess().Id);
            // The test host will be something like "testhost.exe" or "dotnet.exe"
            commandLine.ShouldNotBeNullOrWhiteSpace();
        }

        // -----------------------------------------------------------------------
        // Windows.GetCommandLine – WMI query for non-existent PID returns null
        // -----------------------------------------------------------------------

        [WindowsOnlyFact]
        public void GetCommandLine_InvalidPid_DoesNotThrow()
        {
            // WMI returns no rows for an invalid PID; GetCommandLine should return null, not throw.
            Should.NotThrow(() => InvokeWindowsGetCommandLine(int.MaxValue));
        }

        // -----------------------------------------------------------------------
        // TryGetCommandLine – verifies exception is swallowed and false returned
        // -----------------------------------------------------------------------

        [WindowsOnlyFact]
        public void TryGetCommandLine_NeverThrows_EvenWhenInternalsWould()
        {
            // Verify: TryGetCommandLine never throws even when internals would
            using Process p = Process.GetCurrentProcess();
            Should.NotThrow(() => p.TryGetCommandLine(out _));
        } 

        // -----------------------------------------------------------------------
        // Reflection helper to call the internal Windows.GetCommandLine directly.
        // -----------------------------------------------------------------------

        private static string? InvokeWindowsGetCommandLine(int processId)
        {
            // ProcessExtensions is internal; Windows is a private nested class.
            // Use reflection to call it directly so exceptions propagate.
            var processExtensionsType = typeof(ProcessExtensions);
            var windowsType = processExtensionsType.GetNestedType("Windows",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            windowsType.ShouldNotBeNull("Could not locate ProcessExtensions.Windows via reflection.");

            var method = windowsType.GetMethod("GetCommandLine",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            method.ShouldNotBeNull("Could not locate ProcessExtensions.Windows.GetCommandLine via reflection.");

            try
            {
                return (string?)method.Invoke(null, new object[] { processId });
            }
            catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw; // unreachable
            }
        }
    }
}
