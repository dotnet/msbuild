// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Moq;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.Tools.Run.Tests
{
    public class GivenARunCommand : TestBase
    {
        private const int RunExitCode = 29;

        [Fact]
        public void ItDoesntRedirectStandardOutAndError()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppSimple")
                                         .WithLockFiles();

            new BuildCommand(instance.TestRoot)
                .Execute()
                .Should()
                .Pass();

            // use MockBehavior.Strict to ensure the RunCommand doesn't call CaptureStdOut, ForwardStdOut, etc.
            Mock<ICommand> failOnRedirectOutputCommand = new Mock<ICommand>(MockBehavior.Strict);
            failOnRedirectOutputCommand
                .Setup(c => c.Execute())
                .Returns(new CommandResult(null, RunExitCode, null, null));

            Mock<ICommandFactory> commandFactoryMock = new Mock<ICommandFactory>();
            commandFactoryMock
                .Setup(c => c.Create(
                                It.IsAny<string>(),
                                It.IsAny<IEnumerable<string>>(),
                                It.IsAny<NuGetFramework>(),
                                It.IsAny<string>()))
                .Returns(failOnRedirectOutputCommand.Object);

            RunCommand runCommand = new RunCommand(commandFactoryMock.Object);
            runCommand.Project = instance.TestRoot;

            runCommand.Start()
                .Should()
                .Be(RunExitCode);
        }
    }
}
