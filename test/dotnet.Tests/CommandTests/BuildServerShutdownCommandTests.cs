// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        public void GivenNoOptionsAllManagersArePresent()
        {
            var command = CreateCommand();

            command.Managers.Select(m => m.ServerName).Should().Equal(
                DotNet.BuildServer.LocalizableStrings.MSBuildServer,
                DotNet.BuildServer.LocalizableStrings.VBCSCompilerServer,
                DotNet.BuildServer.LocalizableStrings.RazorServer
            );
        }

        [Fact]
        public void GivenMSBuildOptionOnlyItIsTheOnlyManager()
        {
            var command = CreateCommand("--msbuild");

            command.Managers.Select(m => m.ServerName).Should().Equal(
                DotNet.BuildServer.LocalizableStrings.MSBuildServer
            );
        }

        [Fact]
        public void GivenVBCSCompilerOptionOnlyItIsTheOnlyManager()
        {
            var command = CreateCommand("--vbcscompiler");

            command.Managers.Select(m => m.ServerName).Should().Equal(
                DotNet.BuildServer.LocalizableStrings.VBCSCompilerServer
            );
        }

        [Fact]
        public void GivenRazorOptionOnlyItIsTheOnlyManager()
        {
            var command = CreateCommand("--razor");

            command.Managers.Select(m => m.ServerName).Should().Equal(
                DotNet.BuildServer.LocalizableStrings.RazorServer
            );
        }

        [Fact]
        public void GivenSuccessfulShutdownsItPrintsSuccess()
        {
            var mocks = new[] {
                CreateManagerMock("first", new Result(ResultKind.Success)),
                CreateManagerMock("second", new Result(ResultKind.Success)),
                CreateManagerMock("third", new Result(ResultKind.Success))
            };

            var command = CreateCommand(managers: mocks.Select(m => m.Object));

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
            const string FailureMessage = "failed!";

            var mocks = new[] {
                CreateManagerMock("first", new Result(ResultKind.Success)),
                CreateManagerMock("second", new Result(ResultKind.Failure, FailureMessage)),
                CreateManagerMock("third", new Result(ResultKind.Success))
            };

            var command = CreateCommand(managers: mocks.Select(m => m.Object));

            command.Execute().Should().Be(1);

            _reporter.Lines.Should().Equal(
                FormatShuttingDownMessage(mocks[0].Object),
                FormatShuttingDownMessage(mocks[1].Object),
                FormatShuttingDownMessage(mocks[2].Object),
                FormatSuccessMessage(mocks[0].Object),
                FormatFailureMessage(mocks[1].Object, FailureMessage),
                FormatSuccessMessage(mocks[2].Object));

            VerifyShutdownCalls(mocks);
        }

        [Fact]
        public void GivenASkippedShutdownItPrintsSkipMessage()
        {
            const string SkipMessage = "skipped!";

            var mocks = new[] {
                CreateManagerMock("first", new Result(ResultKind.Success)),
                CreateManagerMock("second", new Result(ResultKind.Success)),
                CreateManagerMock("third", new Result(ResultKind.Skipped, SkipMessage))
            };

            var command = CreateCommand(managers: mocks.Select(m => m.Object));

            command.Execute().Should().Be(0);

            _reporter.Lines.Should().Equal(
                FormatShuttingDownMessage(mocks[0].Object),
                FormatShuttingDownMessage(mocks[1].Object),
                FormatShuttingDownMessage(mocks[2].Object),
                FormatSuccessMessage(mocks[0].Object),
                FormatSuccessMessage(mocks[1].Object),
                FormatSkippedMessage(mocks[2].Object, SkipMessage));

            VerifyShutdownCalls(mocks);
        }

        [Fact]
        public void GivenSuccessFailureAndSkippedItPrintsAllThree()
        {
            const string FailureMessage = "failed!";
            const string SkipMessage = "skipped!";

            var mocks = new[] {
                CreateManagerMock("first", new Result(ResultKind.Success)),
                CreateManagerMock("second", new Result(ResultKind.Failure, FailureMessage)),
                CreateManagerMock("third", new Result(ResultKind.Skipped, SkipMessage))
            };

            var command = CreateCommand(managers: mocks.Select(m => m.Object));

            command.Execute().Should().Be(1);

            _reporter.Lines.Should().Equal(
                FormatShuttingDownMessage(mocks[0].Object),
                FormatShuttingDownMessage(mocks[1].Object),
                FormatShuttingDownMessage(mocks[2].Object),
                FormatSuccessMessage(mocks[0].Object),
                FormatFailureMessage(mocks[1].Object, FailureMessage),
                FormatSkippedMessage(mocks[2].Object, SkipMessage));

            VerifyShutdownCalls(mocks);
        }

        private BuildServerShutdownCommand CreateCommand(string options = "", IEnumerable<IBuildServerManager> managers = null)
        {
            ParseResult result = Parser.Instance.Parse("dotnet buildserver shutdown " + options);
            return new BuildServerShutdownCommand(
                options: result["dotnet"]["buildserver"]["shutdown"],
                result: result,
                managers: managers,
                useOrderedWait: true,
                reporter: _reporter);
        }

        private Mock<IBuildServerManager> CreateManagerMock(string serverName, Result result)
        {
            var mock = new Mock<IBuildServerManager>(MockBehavior.Strict);

            mock.SetupGet(m => m.ServerName).Returns(serverName);
            mock.Setup(m => m.ShutdownServerAsync()).Returns(Task.FromResult(result));

            return mock;
        }

        private void VerifyShutdownCalls(IEnumerable<Mock<IBuildServerManager>> mocks)
        {
            foreach (var mock in mocks)
            {
                mock.Verify(m => m.ShutdownServerAsync(), Times.Once());
            }
        }

        private static string FormatShuttingDownMessage(IBuildServerManager manager)
        {
            return string.Format(LocalizableStrings.ShuttingDownServer, manager.ServerName);
        }

        private static string FormatSuccessMessage(IBuildServerManager manager)
        {
            return string.Format(LocalizableStrings.ShutDownSucceeded, manager.ServerName).Green();
        }

        private static string FormatFailureMessage(IBuildServerManager manager, string message)
        {
            return string.Format(LocalizableStrings.ShutDownFailed, manager.ServerName, message).Red();
        }

        private static string FormatSkippedMessage(IBuildServerManager manager, string message)
        {
            return string.Format(LocalizableStrings.ShutDownSkipped, manager.ServerName, message).Cyan();
        }
    }
}
