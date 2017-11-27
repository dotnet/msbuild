// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.ShellShimMaker.Tests
{
    public class ShellShimMakerTests : TestBase
    {
        private readonly string _pathToPlaceShim;

        public ShellShimMakerTests()
        {
            _pathToPlaceShim = Path.GetTempPath();
        }

        [Fact]
        public void GivenAnExecutablePathItCanGenerateShimFile()
        {
            var outputDll = MakeHelloWorldExecutableDll();

            var shellShimMaker = new ShellShimMaker(_pathToPlaceShim);
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();

            shellShimMaker.CreateShim(
                outputDll.FullName,
                shellCommandName);
            var stdOut = ExecuteInShell(shellCommandName);

            stdOut.Should().Contain("Hello World");
        }

        [Fact]
        public void GivenAnExecutablePathWithExistingSameNameShimItThrows()
        {
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();

            MakeNameConflictingCommand(_pathToPlaceShim, shellCommandName);

            var shellShimMaker = new ShellShimMaker(_pathToPlaceShim);

            Action a = () => shellShimMaker.EnsureCommandNameUniqueness(shellCommandName);
            a.ShouldThrow<GracefulException>()
                .And.Message
                .Should().Contain(
                    $"Failed to install tool {shellCommandName}. A command with the same name already exists.");
        }


        [Fact]
        public void GivenAnExecutablePathWithoutExistingSameNameShimItShouldNotThrow()
        {
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();

            var shellShimMaker = new ShellShimMaker(_pathToPlaceShim);

            Action a = () => shellShimMaker.EnsureCommandNameUniqueness(shellCommandName);
            a.ShouldNotThrow();
        }

        private static void MakeNameConflictingCommand(string pathToPlaceShim, string shellCommandName)
        {
            File.WriteAllText(Path.Combine(pathToPlaceShim, shellCommandName), string.Empty);
        }

        private string ExecuteInShell(string shellCommandName)
        {
            ProcessStartInfo processStartInfo;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "CMD.exe",
                    Arguments = $"/C {shellCommandName}",
                    UseShellExecute = false
                };
            }
            else
            {
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "sh",
                    Arguments = shellCommandName,
                    UseShellExecute = false
                };
            }
            processStartInfo.WorkingDirectory = _pathToPlaceShim;
            processStartInfo.EnvironmentVariables["PATH"] = Path.GetDirectoryName(new Muxer().MuxerPath);

            processStartInfo.ExecuteAndCaptureOutput(out var stdOut, out var stdErr);

            stdErr.Should().BeEmpty();

            return stdOut ?? "";
        }

        private static FileInfo MakeHelloWorldExecutableDll()
        {
            const string testAppName = "TestAppSimple";
            const string emptySpaceToTestSpaceInPath = " ";
            TestAssetInstance testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName + emptySpaceToTestSpaceInPath)
                .UseCurrentRuntimeFrameworkVersion()
                .WithRestoreFiles()
                .WithBuildFiles();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            FileInfo outputDll = testInstance.Root.GetDirectory("bin", configuration)
                .GetDirectories().Single()
                .GetFile($"{testAppName}.dll");

            return outputDll;
        }
    }
}
