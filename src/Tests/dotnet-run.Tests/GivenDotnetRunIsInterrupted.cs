// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRunIsInterrupted : SdkTest
    {
        private const int WaitTimeout = 30000;

        public GivenDotnetRunIsInterrupted(ITestOutputHelper log) : base(log)
        {
        }

        // This test is Unix only for the same reason that CoreFX does not test Console.CancelKeyPress on Windows
        // See https://github.com/dotnet/corefx/blob/a10890f4ffe0fadf090c922578ba0e606ebdd16c/src/System.Console/tests/CancelKeyPress.Unix.cs#L63-L67
        [UnixOnlyFact]
        public void ItIgnoresSIGINT()
        {
            var asset = _testAssetsManager.CopyTestAsset("TestAppThatWaits")
                .WithSource();

            var command = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(asset.Path);

            bool killed = false;

            Process testProcess = null;

            command.ProcessStartedHandler = p => { testProcess = p; };

            command.CommandOutputHandler = line =>
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
                NativeMethods.Posix.kill(testProcess.Id, NativeMethods.Posix.SIGINT).Should().Be(0); // dotnet run
                NativeMethods.Posix.kill(Convert.ToInt32(line), NativeMethods.Posix.SIGINT).Should().Be(0);   // TestAppThatWaits

                killed = true;
            };

            command
                .Execute()
                .Should()
                .ExitWith(42)
                .And
                .HaveStdOutContaining("Interrupted!");

            killed.Should().BeTrue();
        }

        [UnixOnlyFact]
        public void ItPassesSIGTERMToChild()
        {
            var asset = _testAssetsManager.CopyTestAsset("TestAppThatWaits")
                .WithSource();

            var command = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(asset.Path);

            bool killed = false;
            Process child = null;

            Process testProcess = null;
            command.ProcessStartedHandler = p => { testProcess = p; };

            command.CommandOutputHandler = line =>
            {
                if (killed)
                {
                    return;
                }

                child = Process.GetProcessById(Convert.ToInt32(line));
                NativeMethods.Posix.kill(testProcess.Id, NativeMethods.Posix.SIGTERM).Should().Be(0);

                killed = true;
            };

            command
                .Execute()
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
            var asset = _testAssetsManager.CopyTestAsset("TestAppThatWaits")
                .WithSource();

            var command = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(asset.Path);

            bool killed = false;
            Process child = null;
            Process testProcess = null;
            command.ProcessStartedHandler = p => { testProcess = p; };

            command.CommandOutputHandler = line =>
            {
                if (killed)
                {
                    return;
                }

                child = Process.GetProcessById(Convert.ToInt32(line));
                testProcess.Kill();

                killed = true;
            };

            //  As of porting these tests to dotnet/sdk, it's unclear if the below is still needed
            // A timeout is required to prevent the `Process.WaitForExit` call to hang if `dotnet run` failed to terminate the child on Windows.
            // This is because `Process.WaitForExit()` hangs waiting for the process launched by `dotnet run` to close the redirected I/O pipes (which won't happen).

            Task.Delay(TimeSpan.FromMilliseconds(WaitTimeout)).ContinueWith(t =>
            {
                if (!killed)
                {
                    testProcess.Kill();
                }
            });


            command
                .Execute()
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
