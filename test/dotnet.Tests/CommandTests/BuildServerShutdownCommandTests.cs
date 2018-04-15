// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.BuildServer;
using Microsoft.DotNet.Tools.BuildServer.Shutdown;
using Microsoft.DotNet.Tools.Test.Utilities;
using Moq;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.Shutdown.LocalizableStrings;

namespace Microsoft.DotNet.Tests.Commands
{
    public class BuildServerShutdownCommandTests
    {
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

        private BuildServerShutdownCommand CreateCommand(
            string options = "",
            IBuildServerProvider serverProvider = null,
            IEnumerable<IBuildServer> buildServers = null,
            ServerEnumerationFlags expectedFlags = ServerEnumerationFlags.None)
        {
            ParseResult result = Parser.Instance.Parse("dotnet build-server shutdown " + options);
            return new BuildServerShutdownCommand(
                options: result["dotnet"]["build-server"]["shutdown"],
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
