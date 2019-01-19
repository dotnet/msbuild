// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRunIsInterrupted : TestBase
    {
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
                kill(command.CurrentProcess.Id, SIGINT).Should().Be(0); // dotnet run
                kill(Convert.ToInt32(e.Data), SIGINT).Should().Be(0);   // TestAppThatWaits

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

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);

        private const int SIGINT = 2;
    }
}
