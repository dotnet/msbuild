// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.Tests.BuildServerTests
{
    public class VBCSCompilerServerManagerTests
    {
        [Fact]
        public async Task GivenAZeroExit()
        {
            var commandMock = new Mock<ICommand>(MockBehavior.Strict);
            commandMock.Setup(c => c.CaptureStdOut()).Returns(commandMock.Object);
            commandMock.Setup(c => c.CaptureStdErr()).Returns(commandMock.Object);
            commandMock.Setup(c => c.Execute()).Returns(new CommandResult(null, 0, "", ""));

            var commandFactoryMock = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactoryMock
                .Setup(
                    f => f.Create(
                        "exec",
                        new string[] { VBCSCompilerServerManager.VBCSCompilerPath, "-shutdown" },
                        It.IsAny<NuGetFramework>(),
                        Constants.DefaultConfiguration))
                .Returns(commandMock.Object);

            var manager = new VBCSCompilerServerManager(commandFactoryMock.Object);

            var result = await manager.ShutdownServerAsync();
            result.Kind.Should().Be(ResultKind.Success);
            result.Message.Should().BeNull();
            result.Exception.Should().BeNull();
        }

        [Fact]
        public async Task GivenANonZeroExitCodeShutdownFails()
        {
            const string ErrorMessage = "failed!";

            var commandMock = new Mock<ICommand>(MockBehavior.Strict);
            commandMock.Setup(c => c.CaptureStdOut()).Returns(commandMock.Object);
            commandMock.Setup(c => c.CaptureStdErr()).Returns(commandMock.Object);
            commandMock.Setup(c => c.Execute()).Returns(new CommandResult(null, 1, "", ErrorMessage));

            var commandFactoryMock = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactoryMock
                .Setup(
                    f => f.Create(
                        "exec",
                        new string[] { VBCSCompilerServerManager.VBCSCompilerPath, "-shutdown" },
                        It.IsAny<NuGetFramework>(),
                        Constants.DefaultConfiguration))
                .Returns(commandMock.Object);

            var manager = new VBCSCompilerServerManager(commandFactoryMock.Object);

            var result = await manager.ShutdownServerAsync();
            result.Kind.Should().Be(ResultKind.Failure);
            result.Message.Should().Be(ErrorMessage);
            result.Exception.Should().BeNull();
        }
    }
}
