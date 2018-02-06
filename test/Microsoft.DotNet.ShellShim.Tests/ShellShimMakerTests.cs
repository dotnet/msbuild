// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Transactions;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class ShellShimMakerTests : TestBase
    {
        private readonly ITestOutputHelper _output;

        public ShellShimMakerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("my_native_app.exe", null)]
        [InlineData("./my_native_app.js", "nodejs")]
        [InlineData(@"C:\tools\my_native_app.dll", "dotnet")]
        public void GivenAnRunnerOrEntryPointItCanCreateConfig(string entryPointPath, string runner)
        {
            var entryPoint = new FilePath(entryPointPath);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            var cleanFolderUnderTempRoot = GetNewCleanFolderUnderTempRoot();
            var shellShimMaker = new ShellShimMaker(cleanFolderUnderTempRoot);

            var tmpFile = new FilePath(Path.Combine(cleanFolderUnderTempRoot, Path.GetRandomFileName()));

            shellShimMaker.CreateConfigFile(tmpFile, entryPoint, runner);

            new FileInfo(tmpFile.Value).Should().Exist();

            var generated = XDocument.Load(tmpFile.Value);

            generated.Descendants("appSettings")
                .Descendants("add")
                .Should()
                .Contain(e => e.Attribute("key").Value == "runner" && e.Attribute("value").Value == (runner ?? string.Empty))
                .And
                .Contain(e => e.Attribute("key").Value == "entryPoint" && e.Attribute("value").Value == entryPoint.Value);
        }

        [Fact]
        public void GivenAnExecutablePathItCanGenerateShimFile()
        {
            var outputDll = MakeHelloWorldExecutableDll();

            var cleanFolderUnderTempRoot = GetNewCleanFolderUnderTempRoot();
            var shellShimMaker = new ShellShimMaker(cleanFolderUnderTempRoot);
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();

            shellShimMaker.CreateShim(outputDll, shellCommandName);

            var stdOut = ExecuteInShell(shellCommandName, cleanFolderUnderTempRoot);

            stdOut.Should().Contain("Hello World");
        }
        
        [Fact]
        public void GivenAnExecutablePathItCanGenerateShimFileInTransaction()
        {
            var outputDll = MakeHelloWorldExecutableDll();

            var cleanFolderUnderTempRoot = GetNewCleanFolderUnderTempRoot();
            var shellShimMaker = new ShellShimMaker(cleanFolderUnderTempRoot);
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();

            using (var transactionScope = new TransactionScope())
            {
                shellShimMaker.CreateShim(outputDll, shellCommandName);
                transactionScope.Complete();
            }

            var stdOut = ExecuteInShell(shellCommandName, cleanFolderUnderTempRoot);

            stdOut.Should().Contain("Hello World");
        }

        [Fact]
        public void GivenAnExecutablePathDirectoryThatDoesNotExistItCanGenerateShimFile()
        {
            var outputDll = MakeHelloWorldExecutableDll();
            var extraNonExistDirectory = Path.GetRandomFileName();
            var shellShimMaker = new ShellShimMaker(Path.Combine(TempRoot.Root, extraNonExistDirectory));
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();

            Action a = () => shellShimMaker.CreateShim(outputDll, shellCommandName);

            a.ShouldNotThrow<DirectoryNotFoundException>();
        }

        [Theory]
        [InlineData("arg1 arg2", new[] { "arg1", "arg2" })]
        [InlineData(" \"arg1 with space\" arg2", new[] { "arg1 with space", "arg2" })]
        [InlineData(" \"arg with ' quote\" ", new[] { "arg with ' quote" })]
        public void GivenAShimItPassesThroughArguments(string arguments, string[] expectedPassThru)
        {
            var outputDll = MakeHelloWorldExecutableDll();

            var cleanFolderUnderTempRoot = GetNewCleanFolderUnderTempRoot();
            var shellShimMaker = new ShellShimMaker(cleanFolderUnderTempRoot);
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();

            shellShimMaker.CreateShim(outputDll, shellCommandName);

            var stdOut = ExecuteInShell(shellCommandName, cleanFolderUnderTempRoot, arguments);

            for (int i = 0; i < expectedPassThru.Length; i++)
            {
                stdOut.Should().Contain($"{i} = {expectedPassThru[i]}");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnExecutablePathWithExistingSameNameShimItThrows(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();
            var cleanFolderUnderTempRoot = GetNewCleanFolderUnderTempRoot();
            MakeNameConflictingCommand(cleanFolderUnderTempRoot, shellCommandName);

            IShellShimMaker shellShimMaker;
            if (testMockBehaviorIsInSync)
            {
                shellShimMaker = new ShellShimMakerMock(cleanFolderUnderTempRoot);
            }
            else
            {
                shellShimMaker = new ShellShimMaker(cleanFolderUnderTempRoot);
            }

            Action a = () => shellShimMaker.EnsureCommandNameUniqueness(shellCommandName);
            a.ShouldThrow<GracefulException>()
                .And.Message
                .Should().Contain(
                    $"Failed to install tool {shellCommandName}. A command with the same name already exists.");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnExecutablePathWithExistingSameNameShimItRollsBack(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();

            var pathToShim = GetNewCleanFolderUnderTempRoot();
            MakeNameConflictingCommand(pathToShim, shellCommandName);

            IShellShimMaker shellShimMaker;
            if (testMockBehaviorIsInSync)
            {
                shellShimMaker = new ShellShimMakerMock(pathToShim);
            }
            else
            {
                shellShimMaker = new ShellShimMaker(pathToShim);
            }

            Action a = () =>
            {
                using (var t = new TransactionScope())
                {
                    shellShimMaker.CreateShim(new FilePath("dummy.dll"), shellCommandName);

                    t.Complete();
                }
            };
            a.ShouldThrow<GracefulException>();

            Directory.GetFiles(pathToShim).Should().HaveCount(1, "there is only intent conflicted command");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnExecutablePathErrorHappensItRollsBack(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();

            var pathToShim = GetNewCleanFolderUnderTempRoot();

            IShellShimMaker shellShimMaker;
            if (testMockBehaviorIsInSync)
            {
                shellShimMaker = new ShellShimMakerMock(pathToShim);
            }
            else
            {
                shellShimMaker = new ShellShimMaker(pathToShim);
            }
               
            Action intendedError = () => throw new PackageObtainException();

            Action a = () =>
            {
                using (var t = new TransactionScope())
                {
                    shellShimMaker.CreateShim(new FilePath("dummy.dll"), shellCommandName);

                    intendedError();
                    t.Complete();
                }
            };
            a.ShouldThrow<PackageObtainException>();

            Directory.GetFiles(pathToShim).Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnExecutablePathWithoutExistingSameNameShimItShouldNotThrow(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimMakerTests) + Path.GetRandomFileName();
            var cleanFolderUnderTempRoot = GetNewCleanFolderUnderTempRoot();

            IShellShimMaker shellShimMaker;
            if (testMockBehaviorIsInSync)
            {
                shellShimMaker = new ShellShimMakerMock(cleanFolderUnderTempRoot);
            }
            else
            {
                shellShimMaker = new ShellShimMaker(cleanFolderUnderTempRoot);
            }

            Action a = () => shellShimMaker.EnsureCommandNameUniqueness(shellCommandName);
            a.ShouldNotThrow();
        }

        private static void MakeNameConflictingCommand(string pathToPlaceShim, string shellCommandName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shellCommandName = shellCommandName + ".exe";
            }

            File.WriteAllText(Path.Combine(pathToPlaceShim, shellCommandName), string.Empty);
        }

        private string ExecuteInShell(string shellCommandName, string cleanFolderUnderTempRoot, string arguments = "")
        {
            ProcessStartInfo processStartInfo;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var file = Path.Combine(cleanFolderUnderTempRoot, shellCommandName + ".exe");
                processStartInfo = new ProcessStartInfo
                {
                    FileName = file,
                    UseShellExecute = false,
                    Arguments = arguments,
                };
            }
            else
            {
                processStartInfo = new ProcessStartInfo
                {
                    FileName = "sh",
                    Arguments = shellCommandName + " " + arguments,
                    UseShellExecute = false
                };
            }

            _output.WriteLine($"Launching '{processStartInfo.FileName} {processStartInfo.Arguments}'");
            processStartInfo.WorkingDirectory = cleanFolderUnderTempRoot;
            processStartInfo.EnvironmentVariables["PATH"] = Path.GetDirectoryName(new Muxer().MuxerPath);

            processStartInfo.ExecuteAndCaptureOutput(out var stdOut, out var stdErr);

            stdErr.Should().BeEmpty();

            return stdOut ?? "";
        }

        private static FilePath MakeHelloWorldExecutableDll()
        {
            const string testAppName = "TestAppSimple";
            const string emptySpaceToTestSpaceInPath = " ";
            TestAssetInstance testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName + emptySpaceToTestSpaceInPath + "test")
                .UseCurrentRuntimeFrameworkVersion()
                .WithRestoreFiles()
                .WithBuildFiles();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            FileInfo outputDll = testInstance.Root.GetDirectory("bin", configuration)
                .GetDirectories().Single()
                .GetFile($"{testAppName}.dll");

            return new FilePath(outputDll.FullName);
        }

        private static string GetNewCleanFolderUnderTempRoot()
        {
            DirectoryInfo CleanFolderUnderTempRoot = new DirectoryInfo(Path.Combine(TempRoot.Root, "cleanfolder" + Path.GetRandomFileName()));
            CleanFolderUnderTempRoot.Create();

            return CleanFolderUnderTempRoot.FullName;
        }
    }
}
