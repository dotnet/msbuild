// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Exceptions;
using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using NuGet.Frameworks;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.BuildServer.LocalizableStrings;

namespace Microsoft.DotNet.Tests.BuildServerTests
{
    public class RazorServerManagerTests
    {
        [Fact]
        public async Task GivenNoRazorAssemblyShutdownIsSkipped()
        {
            var resolverMock = new Mock<IRazorAssemblyResolver>(MockBehavior.Strict);
            resolverMock.Setup(r => r.EnumerateRazorToolAssemblies()).Returns(new FilePath[] {});

            var commandFactoryMock = new Mock<ICommandFactory>(MockBehavior.Strict);

            var manager = new RazorServerManager(resolverMock.Object, commandFactoryMock.Object);

            var result = await manager.ShutdownServerAsync();
            result.Kind.Should().Be(ResultKind.Skipped);
            result.Message.Should().Be(LocalizableStrings.NoRazorProjectFound);
            result.Exception.Should().BeNull();
        }

        [Fact]
        public async Task GivenARazorAssemblyShutdownSucceeds()
        {
            const string FakeRazorAssemblyPath = "/path/to/razor.dll";

            var resolverMock = new Mock<IRazorAssemblyResolver>(MockBehavior.Strict);
            resolverMock.Setup(r => r.EnumerateRazorToolAssemblies()).Returns(new FilePath[] { new FilePath(FakeRazorAssemblyPath) });

            var commandMock = new Mock<ICommand>(MockBehavior.Strict);
            commandMock.Setup(c => c.CaptureStdOut()).Returns(commandMock.Object);
            commandMock.Setup(c => c.CaptureStdErr()).Returns(commandMock.Object);
            commandMock.Setup(c => c.Execute()).Returns(new CommandResult(null, 0, "", ""));

            var commandFactoryMock = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactoryMock
                .Setup(
                    f => f.Create(
                        "exec",
                        new string[] { FakeRazorAssemblyPath, "shutdown" },
                        It.IsAny<NuGetFramework>(),
                        Constants.DefaultConfiguration))
                .Returns(commandMock.Object);

            var manager = new RazorServerManager(resolverMock.Object, commandFactoryMock.Object);

            var result = await manager.ShutdownServerAsync();
            result.Kind.Should().Be(ResultKind.Success);
            result.Message.Should().BeNull();
            result.Exception.Should().BeNull();
        }

        [Fact]
        public async Task GivenAnInvalidProjectFileShutdownFails()
        {
            var exception = new InvalidProjectFileException("invalid project!");

            var resolverMock = new Mock<IRazorAssemblyResolver>(MockBehavior.Strict);
            resolverMock.Setup(r => r.EnumerateRazorToolAssemblies()).Throws(exception);

            var commandFactoryMock = new Mock<ICommandFactory>(MockBehavior.Strict);

            var manager = new RazorServerManager(resolverMock.Object, commandFactoryMock.Object);

            var result = await manager.ShutdownServerAsync();
            result.Kind.Should().Be(ResultKind.Failure);
            result.Message.Should().Be(exception.Message);
            result.Exception.Should().Be(exception);
        }

        [Fact]
        public async Task GivenANonZeroExitCodeShutdownFails()
        {
            const string FakeRazorAssemblyPath = "/path/to/razor.dll";
            const string ErrorMessage = "failed!";

            var resolverMock = new Mock<IRazorAssemblyResolver>(MockBehavior.Strict);
            resolverMock.Setup(r => r.EnumerateRazorToolAssemblies()).Returns(new FilePath[] { new FilePath(FakeRazorAssemblyPath) });

            var commandMock = new Mock<ICommand>(MockBehavior.Strict);
            commandMock.Setup(c => c.CaptureStdOut()).Returns(commandMock.Object);
            commandMock.Setup(c => c.CaptureStdErr()).Returns(commandMock.Object);
            commandMock.Setup(c => c.Execute()).Returns(new CommandResult(null, 1, "", ErrorMessage));

            var commandFactoryMock = new Mock<ICommandFactory>(MockBehavior.Strict);
            commandFactoryMock
                .Setup(
                    f => f.Create(
                        "exec",
                        new string[] { FakeRazorAssemblyPath, "shutdown" },
                        It.IsAny<NuGetFramework>(),
                        Constants.DefaultConfiguration))
                .Returns(commandMock.Object);

            var manager = new RazorServerManager(resolverMock.Object, commandFactoryMock.Object);

            var result = await manager.ShutdownServerAsync();
            result.Kind.Should().Be(ResultKind.Failure);
            result.Message.Should().Be(ErrorMessage);
            result.Exception.Should().BeNull();
        }
    }
}
