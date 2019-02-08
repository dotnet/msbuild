// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit.Sdk;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRunIsInterrupted : TestBase
    {
        private const int WaitTimeout = 30000;

        // This test is Unix only for the same reason that CoreFX does not test Console.CancelKeyPress on Windows
        // See https://github.com/dotnet/corefx/blob/a10890f4ffe0fadf090c922578ba0e606ebdd16c/src/System.Console/tests/CancelKeyPress.Unix.cs#L63-L67
        [UnixOnlyFact]
        public void ItIgnoresSIGINT()
        {
            var asset = TestAssets.Get("TestAppThatWaits")
                .CreateInstance()
                .WithSourceFiles();

            var command = new RunCommand()
                .WithWorkingDirectory(asset.Root.FullName);

            bool killed = false;
            command.OutputDataReceived += (s, e) =>
            {
                if (killed)
                {
                    return;
                }

                // Simulate a SIGINT sent to a process group (i.e. both `dotnet run` and `TestAppThatWaits`).
                // Ideally we would send SIGINT to an actual process group, but the new child process (i.e. `dotnet run`)
                // will inherit the current process group from the `dotnet test` process that is running this test.
                // We would need to fork(), setpgid(), and then execve() to break out of the current group and that is
                // too complex for a simple unit test.
                NativeMethods.Posix.kill(command.CurrentProcess.Id, NativeMethods.Posix.SIGINT).Should().Be(0); // dotnet run
                NativeMethods.Posix.kill(Convert.ToInt32(e.Data), NativeMethods.Posix.SIGINT).Should().Be(0);   // TestAppThatWaits

                killed = true;
            };

            command
                .ExecuteWithCapturedOutput()
                .Should()
                .ExitWith(42)
                .And
                .HaveStdOutContaining("Interrupted!");

            killed.Should().BeTrue();
        }

        [UnixOnlyFact]
        public void ItPassesSIGTERMToChild()
        {
            var asset = TestAssets.Get("TestAppThatWaits")
                .CreateInstance()
                .WithSourceFiles();

            var command = new RunCommand()
                .WithWorkingDirectory(asset.Root.FullName);

            bool killed = false;
            Process child = null;
            command.OutputDataReceived += (s, e) =>
            {
                if (killed)
                {
                    return;
                }

                child = Process.GetProcessById(Convert.ToInt32(e.Data));
                NativeMethods.Posix.kill(command.CurrentProcess.Id, NativeMethods.Posix.SIGTERM).Should().Be(0);

                killed = true;
            };

            command
                .ExecuteWithCapturedOutput()
                .Should()
                .ExitWith(43)
                .And
                .HaveStdOutContaining("Terminating!");

            killed.Should().BeTrue();

            if (!child.WaitForExit(WaitTimeout))
            {
                child.Kill();
                throw new XunitException("child process failed to terminate.");
            }
        }

        [WindowsOnlyFact]
        public void ItTerminatesTheChildWhenKilled()
        {
            var asset = TestAssets.Get("TestAppThatWaits")
                .CreateInstance()
                .WithSourceFiles();

            var command = new RunCommand()
                .WithWorkingDirectory(asset.Root.FullName);

            bool killed = false;
            Process child = null;
            command.OutputDataReceived += (s, e) =>
            {
                if (killed)
                {
                    return;
                }

                child = Process.GetProcessById(Convert.ToInt32(e.Data));
                command.CurrentProcess.Kill();

                killed = true;
            };

            // A timeout is required to prevent the `Process.WaitForExit` call to hang if `dotnet run` failed to terminate the child on Windows.
            // This is because `Process.WaitForExit()` hangs waiting for the process launched by `dotnet run` to close the redirected I/O pipes (which won't happen).
            command.TimeoutMiliseconds = WaitTimeout;

            command
                .ExecuteWithCapturedOutput()
                .Should()
                .ExitWith(-1);

            killed.Should().BeTrue();

            if (!child.WaitForExit(WaitTimeout))
            {
                child.Kill();
                throw new XunitException("child process failed to terminate.");
            }
        }
    }
}
