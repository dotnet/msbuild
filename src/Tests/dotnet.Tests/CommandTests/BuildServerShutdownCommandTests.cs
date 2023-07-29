// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using CommandLocalizableStrings = Microsoft.DotNet.BuildServer.LocalizableStrings;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.Shutdown.LocalizableStrings;
using Microsoft.DotNet.BuildServer;
using System.CommandLine;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands
{
    public class BuildServerShutdownCommandTests : SdkTest
    {
        public BuildServerShutdownCommandTests(ITestOutputHelper log) : base(log)
        {
        }

        private readonly BufferedReporter _reporter = new BufferedReporter();

        [Fact]
        public void GivenNoOptionsItEnumeratesAllServers()
        {
            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.All))
                .Returns(Array.Empty<IBuildServer>());

            var command = CreateCommand(serverProvider: provider.Object);

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(LocalizableStrings.NoServersToShutdown.Green());

            provider.Verify(p => p.EnumerateBuildServers(ServerEnumerationFlags.All), Times.Once);
        }

        [Fact]
        public void GivenMSBuildOptionOnlyItEnumeratesOnlyMSBuildServers()
        {
            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.MSBuild))
                .Returns(Array.Empty<IBuildServer>());

            var command = CreateCommand(options: "--msbuild", serverProvider: provider.Object);

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(LocalizableStrings.NoServersToShutdown.Green());

            provider.Verify(p => p.EnumerateBuildServers(ServerEnumerationFlags.MSBuild), Times.Once);
        }

        [Fact]
        public void GivenVBCSCompilerOptionOnlyItEnumeratesOnlyVBCSCompilers()
        {
            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.VBCSCompiler))
                .Returns(Array.Empty<IBuildServer>());

            var command = CreateCommand(options: "--vbcscompiler", serverProvider: provider.Object);

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(LocalizableStrings.NoServersToShutdown.Green());

            provider.Verify(p => p.EnumerateBuildServers(ServerEnumerationFlags.VBCSCompiler), Times.Once);
        }

        [Fact]
        public void GivenRazorOptionOnlyItEnumeratesOnlyRazorServers()
        {
            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.Razor))
                .Returns(Array.Empty<IBuildServer>());

            var command = CreateCommand(options: "--razor", serverProvider: provider.Object);

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(LocalizableStrings.NoServersToShutdown.Green());

            provider.Verify(p => p.EnumerateBuildServers(ServerEnumerationFlags.Razor), Times.Once);
        }

        [Fact]
        public void GivenSuccessfulShutdownsItPrintsSuccess()
        {
            var mocks = new[] {
                CreateServerMock("first"),
                CreateServerMock("second"),
                CreateServerMock("third")
            };

            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.All))
                .Returns(mocks.Select(m => m.Object));

            var command = CreateCommand(serverProvider: provider.Object);

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(
                FormatShuttingDownMessage(mocks[0].Object),
                FormatShuttingDownMessage(mocks[1].Object),
                FormatShuttingDownMessage(mocks[2].Object),
                FormatSuccessMessage(mocks[0].Object),
                FormatSuccessMessage(mocks[1].Object),
                FormatSuccessMessage(mocks[2].Object));

            VerifyShutdownCalls(mocks);
        }

        [Fact]
        public void GivenAFailingShutdownItPrintsFailureMessage()
        {
            const string FirstFailureMessage = "first failed!";
            const string ThirdFailureMessage = "third failed!";

            var mocks = new[] {
                CreateServerMock("first", exceptionMessage: FirstFailureMessage),
                CreateServerMock("second"),
                CreateServerMock("third", exceptionMessage: ThirdFailureMessage)
            };

            var provider = new Mock<IBuildServerProvider>(MockBehavior.Strict);
            provider
                .Setup(p => p.EnumerateBuildServers(ServerEnumerationFlags.All))
                .Returns(mocks.Select(m => m.Object));

            var command = CreateCommand(serverProvider: provider.Object);

            command.Execute().Should().Be(1);

            _reporter.Lines.Should().Equal(
                FormatShuttingDownMessage(mocks[0].Object),
                FormatShuttingDownMessage(mocks[1].Object),
                FormatShuttingDownMessage(mocks[2].Object),
                FormatFailureMessage(mocks[0].Object, FirstFailureMessage),
                FormatSuccessMessage(mocks[1].Object),
                FormatFailureMessage(mocks[2].Object, ThirdFailureMessage));

            VerifyShutdownCalls(mocks);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/3684")]
        public void GivenARunningRazorServerItShutsDownSuccessfully()
        {
            var pipeName = Path.GetRandomFileName();

            var pidDirectory = _testAssetsManager.CreateTestDirectory(identifier: "pidDirectory").Path;

            var testInstance = _testAssetsManager
                .CopyTestAsset("TestRazorApp")
                .WithSource();

            new BuildCommand(testInstance)
                .WithEnvironmentVariable(BuildServerProvider.PidFileDirectoryVariableName, pidDirectory)
                .Execute($"/p:_RazorBuildServerPipeName={pipeName}")
                .Should()
                .Pass();

            var files = Directory.GetFiles(pidDirectory, RazorPidFile.FilePrefix + "*");
            files.Length.Should().Be(1);

            var pidFile = RazorPidFile.Read(new FilePath(files.First()));
            pidFile.PipeName.Should().Be(pipeName);

            new BuildServerCommand(Log)
                .WithWorkingDirectory(testInstance.TestRoot)
                .WithEnvironmentVariable(BuildServerProvider.PidFileDirectoryVariableName, pidDirectory)
                .Execute("shutdown", "--razor")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(
                    string.Format(
                        LocalizableStrings.ShutDownSucceededWithPid,
                        CommandLocalizableStrings.RazorServer,
                        pidFile.ProcessId));
        }

        private Tools.BuildServer.Shutdown.BuildServerShutdownCommand CreateCommand(
            string options = "",
            IBuildServerProvider serverProvider = null,
            IEnumerable<IBuildServer> buildServers = null,
            ServerEnumerationFlags expectedFlags = ServerEnumerationFlags.None)
        {
            ParseResult result = Parser.Instance.Parse($"dotnet build-server shutdown {options}".Trim());
            return new Tools.BuildServer.Shutdown.BuildServerShutdownCommand(
                result: result,
                serverProvider: serverProvider,
                useOrderedWait: true,
                reporter: _reporter);
        }

        private Mock<IBuildServer> CreateServerMock(string name, int pid = 0, string exceptionMessage = null)
        {
            var mock = new Mock<IBuildServer>(MockBehavior.Strict);

            mock.SetupGet(s => s.ProcessId).Returns(pid);
            mock.SetupGet(s => s.Name).Returns(name);

            if (exceptionMessage == null)
            {
                mock.Setup(s => s.Shutdown());
            }
            else
            {
                mock.Setup(s => s.Shutdown()).Throws(new Exception(exceptionMessage));
            }

            return mock;
        }

        private void VerifyShutdownCalls(IEnumerable<Mock<IBuildServer>> mocks)
        {
            foreach (var mock in mocks)
            {
                mock.Verify(s => s.Shutdown(), Times.Once);
            }
        }

        private static string FormatShuttingDownMessage(IBuildServer server)
        {
            if (server.ProcessId != 0)
            {
                return string.Format(LocalizableStrings.ShuttingDownServerWithPid, server.Name, server.ProcessId);
            }
            return string.Format(LocalizableStrings.ShuttingDownServer, server.Name);
        }

        private static string FormatSuccessMessage(IBuildServer server)
        {
            if (server.ProcessId != 0)
            {
                return string.Format(LocalizableStrings.ShutDownSucceededWithPid, server.Name, server.ProcessId).Green();
            }
            return string.Format(LocalizableStrings.ShutDownSucceeded, server.Name).Green();
        }

        private static string FormatFailureMessage(IBuildServer server, string message)
        {
            if (server.ProcessId != 0)
            {
                return string.Format(LocalizableStrings.ShutDownFailedWithPid, server.Name, server.ProcessId, message).Red();
            }
            return string.Format(LocalizableStrings.ShutDownFailed, server.Name, message).Red();
        }
    }
}
