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
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class ShellShimRepositoryTests : TestBase
    {
        private readonly ITestOutputHelper _output;

        public ShellShimRepositoryTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsOnlyTheory]
        [InlineData("my_native_app.exe", null)]
        [InlineData("./my_native_app.js", "nodejs")]
        [InlineData(@"C:\tools\my_native_app.dll", "dotnet")]
        public void GivenAnRunnerOrEntryPointItCanCreateConfig(string entryPointPath, string runner)
        {
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            var shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            var tmpFile = new FilePath(Path.Combine(pathToShim, Path.GetRandomFileName()));

            shellShimRepository.CreateConfigFile(tmpFile, new FilePath(entryPointPath), runner);

            new FileInfo(tmpFile.Value).Should().Exist();

            var generated = XDocument.Load(tmpFile.Value);

            generated.Descendants("appSettings")
                .Descendants("add")
                .Should()
                .Contain(e => e.Attribute("key").Value == "runner" && e.Attribute("value").Value == (runner ?? string.Empty))
                .And
                .Contain(e => e.Attribute("key").Value == "entryPoint" && e.Attribute("value").Value == entryPointPath);
        }

        [Fact]
        public void GivenAnExecutablePathItCanGenerateShimFile()
        {
            var outputDll = MakeHelloWorldExecutableDll();
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            var shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();

            shellShimRepository.CreateShim(outputDll, shellCommandName);

            var stdOut = ExecuteInShell(shellCommandName, pathToShim);

            stdOut.Should().Contain("Hello World");
        }

        [Fact]
        public void GivenAnExecutablePathItCanGenerateShimFileInTransaction()
        {
            var outputDll = MakeHelloWorldExecutableDll();
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            var shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();

            using (var transactionScope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                shellShimRepository.CreateShim(outputDll, shellCommandName);
                transactionScope.Complete();
            }

            var stdOut = ExecuteInShell(shellCommandName, pathToShim);

            stdOut.Should().Contain("Hello World");
        }

        [Fact]
        public void GivenAnExecutablePathDirectoryThatDoesNotExistItCanGenerateShimFile()
        {
            var outputDll = MakeHelloWorldExecutableDll();
            var extraNonExistDirectory = Path.GetRandomFileName();
            var shellShimRepository = new ShellShimRepository(new DirectoryPath(Path.Combine(TempRoot.Root, extraNonExistDirectory)));
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();

            Action a = () => shellShimRepository.CreateShim(outputDll, shellCommandName);

            a.ShouldNotThrow<DirectoryNotFoundException>();
        }

        [Theory]
        [InlineData("arg1 arg2", new[] { "arg1", "arg2" })]
        [InlineData(" \"arg1 with space\" arg2", new[] { "arg1 with space", "arg2" })]
        [InlineData(" \"arg with ' quote\" ", new[] { "arg with ' quote" })]
        public void GivenAShimItPassesThroughArguments(string arguments, string[] expectedPassThru)
        {
            var outputDll = MakeHelloWorldExecutableDll();
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            var shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();

            shellShimRepository.CreateShim(outputDll, shellCommandName);

            var stdOut = ExecuteInShell(shellCommandName, pathToShim, arguments);

            for (int i = 0; i < expectedPassThru.Length; i++)
            {
                stdOut.Should().Contain($"{i} = {expectedPassThru[i]}");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAShimConflictItWillRollback(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            MakeNameConflictingCommand(pathToShim, shellCommandName);

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = new ShellShimRepositoryMock(new DirectoryPath(pathToShim));
            }
            else
            {
                shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            }

            Action a = () =>
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    shellShimRepository.CreateShim(new FilePath("dummy.dll"), shellCommandName);

                    scope.Complete();
                }
            };

            a.ShouldThrow<ShellShimException>().Where(
                ex => ex.Message ==
                    string.Format(
                        CommonLocalizableStrings.ShellShimConflict,
                        shellCommandName));

            Directory
                .EnumerateFileSystemEntries(pathToShim)
                .Should()
                .HaveCount(1, "should only be the original conflicting command");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnExceptionItWillRollback(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = new ShellShimRepositoryMock(new DirectoryPath(pathToShim));
            }
            else
            {
                shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            }

            Action intendedError = () => throw new ToolPackageException("simulated error");

            Action a = () =>
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    shellShimRepository.CreateShim(new FilePath("dummy.dll"), shellCommandName);

                    intendedError();
                    scope.Complete();
                }
            };
            a.ShouldThrow<ToolPackageException>().WithMessage("simulated error");

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenANonexistentShimRemoveDoesNotThrow(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = new ShellShimRepositoryMock(new DirectoryPath(pathToShim));
            }
            else
            {
                shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

            shellShimRepository.RemoveShim(shellCommandName);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledShimRemoveDeletesTheShimFiles(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = new ShellShimRepositoryMock(new DirectoryPath(pathToShim));
            }
            else
            {
                shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

            shellShimRepository.CreateShim(new FilePath("dummy.dll"), shellCommandName);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().NotBeEmpty();

            shellShimRepository.RemoveShim(shellCommandName);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledShimRemoveRollsbackIfTransactionIsAborted(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = new ShellShimRepositoryMock(new DirectoryPath(pathToShim));
            }
            else
            {
                shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

            shellShimRepository.CreateShim(new FilePath("dummy.dll"), shellCommandName);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().NotBeEmpty();

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                shellShimRepository.RemoveShim(shellCommandName);

                Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().NotBeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledShimRemoveCommitsIfTransactionIsCompleted(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = new ShellShimRepositoryMock(new DirectoryPath(pathToShim));
            }
            else
            {
                shellShimRepository = new ShellShimRepository(new DirectoryPath(pathToShim));
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

            shellShimRepository.CreateShim(new FilePath("dummy.dll"), shellCommandName);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().NotBeEmpty();

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                shellShimRepository.RemoveShim(shellCommandName);

                Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

                scope.Complete();
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
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
            const string directoryNamePostFix = "Test";
            TestAssetInstance testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName + emptySpaceToTestSpaceInPath + directoryNamePostFix)
                .UseCurrentRuntimeFrameworkVersion()
                .WithRestoreFiles()
                .WithBuildFiles();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            FileInfo outputDll = testInstance.Root.GetDirectory("bin", configuration)
                .EnumerateDirectories()
                .Single()
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
