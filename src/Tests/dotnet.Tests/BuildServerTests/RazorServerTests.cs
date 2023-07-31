// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using NuGet.Frameworks;
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

            var serverPath = Path.GetFullPath("path/to/rzc.dll");

            var fileSystemMock = new FileSystemMockBuilder()
                .AddFile(pidFilePath, "")
                .AddFile(serverPath, "")
                .UseCurrentSystemTemporaryDirectory()
                .Build();

            fileSystemMock.File.Exists(pidFilePath).Should().BeTrue();
            fileSystemMock.File.Exists(serverPath).Should().BeTrue();

            var server = new RazorServer(
                pidFile: new RazorPidFile(
                    path: new FilePath(pidFilePath),
                    processId: ProcessId,
                    serverPath: new FilePath(serverPath),
                    pipeName: PipeName),
                commandFactory: CreateCommandFactoryMock(serverPath, PipeName, exitCode: 1, stdErr: ErrorMessage).Object,
                fileSystem: fileSystemMock);

            Action a = () => server.Shutdown();

            a.Should().Throw<BuildServerException>().WithMessage(
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

            var serverPath = Path.GetFullPath("path/to/rzc.dll");

            var fileSystemMock = new FileSystemMockBuilder()
                .AddFile(pidFilePath, "")
                .AddFile(serverPath, "")
                .UseCurrentSystemTemporaryDirectory()
                .Build();

            fileSystemMock.File.Exists(pidFilePath).Should().BeTrue();
            fileSystemMock.File.Exists(serverPath).Should().BeTrue();

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

        [Fact]
        public void GivenANonExistingRazorServerPathItDeletesPidFileAndDoesNotThrow()
        {
            const int ProcessId = 1234;
            const string PipeName = "some-pipe-name";

            string pidDirectory = Path.GetFullPath("var/pids/build");
            string pidFilePath = Path.Combine(pidDirectory, $"{RazorPidFile.FilePrefix}{ProcessId}");

            var serverPath = Path.GetFullPath("path/to/rzc.dll");

            var fileSystemMock = new FileSystemMockBuilder()
                .AddFile(pidFilePath, "")
                .UseCurrentSystemTemporaryDirectory()
                .Build();

            fileSystemMock.File.Exists(pidFilePath).Should().BeTrue();
            fileSystemMock.File.Exists(serverPath).Should().BeFalse();

            var commandFactoryMock = CreateCommandFactoryMock(serverPath, PipeName);
            var server = new RazorServer(
                pidFile: new RazorPidFile(
                    path: new FilePath(pidFilePath),
                    processId: ProcessId,
                    serverPath: new FilePath(serverPath),
                    pipeName: PipeName),
                commandFactory: commandFactoryMock.Object,
                fileSystem: fileSystemMock);

            Action a = () => server.Shutdown();

            a.Should().NotThrow();
            commandFactoryMock.Verify(c => c.Create(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<NuGetFramework>(), It.IsAny<string>()), Times.Never);

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
