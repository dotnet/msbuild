// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using NuGet.Frameworks;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.BuildServer.LocalizableStrings;

namespace Microsoft.DotNet.Tests.BuildServerTests
{
    public class RazorServerTests
    {
        [Fact]
        public void GivenAFailedShutdownCommandItThrows()
        {
            const int ProcessId = 1234;
            const string PipeName = "some-pipe-name";
            const string ErrorMessage = "error!";

            string pidDirectory = Path.GetFullPath("var/pids/build");
            string pidFilePath = Path.Combine(pidDirectory, $"{RazorPidFile.FilePrefix}{ProcessId}");

            var fileSystemMock = new FileSystemMockBuilder()
                .AddFile(pidFilePath, "")
                .UseCurrentSystemTemporaryDirectory()
                .Build();

            fileSystemMock.File.Exists(pidFilePath).Should().BeTrue();

            var serverPath = Path.Combine(fileSystemMock.Directory.CreateTemporaryDirectory().DirectoryPath, "path/to/rzc.dll");

            var server = new RazorServer(
                pidFile: new RazorPidFile(
                    path: new FilePath(pidFilePath),
                    processId: ProcessId,
                    serverPath: new FilePath(serverPath),
                    pipeName: PipeName),
                commandFactory: CreateCommandFactoryMock(serverPath, PipeName, exitCode: 1, stdErr: ErrorMessage).Object,
                fileSystem: fileSystemMock);

            Action a = () => server.Shutdown();

            a.ShouldThrow<BuildServerException>().WithMessage(
                string.Format(
                    LocalizableStrings.ShutdownCommandFailed,
                    ErrorMessage));

            fileSystemMock.File.Exists(pidFilePath).Should().BeTrue();
        }

        [Fact]
        public void GivenASuccessfulShutdownItDoesNotThrow()
        {
            const int ProcessId = 1234;
            const string PipeName = "some-pipe-name";

            string pidDirectory = Path.GetFullPath("var/pids/build");
            string pidFilePath = Path.Combine(pidDirectory, $"{RazorPidFile.FilePrefix}{ProcessId}");

            var fileSystemMock = new FileSystemMockBuilder()
                .AddFile(pidFilePath, "")
                .UseCurrentSystemTemporaryDirectory()
                .Build();

            fileSystemMock.File.Exists(pidFilePath).Should().BeTrue();

            var serverPath = Path.Combine(fileSystemMock.Directory.CreateTemporaryDirectory().DirectoryPath, "path/to/rzc.dll");

            var server = new RazorServer(
                pidFile: new RazorPidFile(
                    path: new FilePath(pidFilePath),
                    processId: ProcessId,
                    serverPath: new FilePath(serverPath),
                    pipeName: PipeName),
                commandFactory: CreateCommandFactoryMock(serverPath, PipeName).Object,
                fileSystem: fileSystemMock);

            server.Shutdown();

            fileSystemMock.File.Exists(pidFilePath).Should().BeFalse();
        }

        private Mock<ICommandFactory> CreateCommandFactoryMock(string serverPath, string pipeName, int exitCode = 0, string stdErr = "")
        {
            var commandMock = new Mock<ICommand>(MockBehavior.Strict);
            commandMock.Setup(c => c.CaptureStdOut()).Returns(commandMock.Object);
            commandMock.Setup(c => c.CaptureStdErr()).Returns(commandMock.Object);
            commandMock.Setup(c => c.Execute()).Returns(new CommandResult(null, exitCode, "", stdErr));

            var commandFactoryMock = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactoryMock
                .Setup(
                    f => f.Create(
                        "exec",
                        new string[] { serverPath, "shutdown", "-w", "-p", pipeName },
                        It.IsAny<NuGetFramework>(),
                        Constants.DefaultConfiguration))
                .Returns(commandMock.Object);

            return commandFactoryMock;
        }
    }
}
