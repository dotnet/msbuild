// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using NuGet.Frameworks;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.BuildServer.LocalizableStrings;

namespace Microsoft.DotNet.Tests.BuildServerTests
{
    public class VBCSCompilerServerTests
    {
        [Fact]
        public void GivenAZeroExitShutdownDoesNotThrow()
        {
            var server = new VBCSCompilerServer(CreateCommandFactoryMock().Object);
            server.Shutdown();
        }

        [Fact]
        public void GivenANonZeroExitCodeShutdownThrows()
        {
            const string ErrorMessage = "failed!";

            var server = new VBCSCompilerServer(CreateCommandFactoryMock(exitCode: 1, stdErr: ErrorMessage).Object);

            Action a = () => server.Shutdown();

            a.ShouldThrow<BuildServerException>().WithMessage(
                string.Format(
                    LocalizableStrings.ShutdownCommandFailed,
                    ErrorMessage));
        }

        private Mock<ICommandFactory> CreateCommandFactoryMock(int exitCode = 0, string stdErr = "")
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
                        new string[] { VBCSCompilerServer.VBCSCompilerPath, "-shutdown" },
                        It.IsAny<NuGetFramework>(),
                        Constants.DefaultConfiguration))
                .Returns(commandMock.Object);

            return commandFactoryMock;
        }
    }
}
