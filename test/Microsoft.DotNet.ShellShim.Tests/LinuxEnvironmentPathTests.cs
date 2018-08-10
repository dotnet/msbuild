// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class LinuxEnvironmentPathTests
    {
        [Fact]
        public void GivenPathNotSetItPrintsManualInstructions()
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            var environmentPath = new LinuxEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                FileSystemMockBuilder.Empty.File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(
                string.Format(
                    CommonLocalizableStrings.EnvironmentPathLinuxManualInstructions,
                    toolsPath.Path));
        }

        [Fact]
        public void GivenPathNotSetAndProfileExistsItPrintsLogoutMessage()
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            var environmentPath = new LinuxEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                new FileSystemMockBuilder()
                    .AddFile(LinuxEnvironmentPath.DotnetCliToolsProfilePath, "")
                    .Build()
                    .File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(CommonLocalizableStrings.EnvironmentPathLinuxNeedLogout);
        }

        [Theory]
        [InlineData("/home/user/.dotnet/tools")]
        [InlineData("~/.dotnet/tools")]
        public void GivenPathSetItPrintsNothing(string toolsDiretoryOnPath)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue + ":" + toolsDiretoryOnPath);

            var environmentPath = new LinuxEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                FileSystemMockBuilder.Empty.File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().BeEmpty();
        }

        [Fact]
        public void GivenPathSetItDoesNotAddPathToEnvironment()
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            var fileSystem = new FileSystemMockBuilder().Build().File;

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue + ":" + toolsPath.Path);

            var environmentPath = new LinuxEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                fileSystem);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();

            fileSystem
                .Exists(LinuxEnvironmentPath.DotnetCliToolsProfilePath)
                .Should()
                .Be(false);
        }

        [Fact]
        public void GivenPathNotSetItAddsToEnvironment()
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            var fileSystem = new FileSystemMockBuilder().Build().File;

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            var environmentPath = new LinuxEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                fileSystem);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();

            fileSystem
                .ReadAllText(LinuxEnvironmentPath.DotnetCliToolsProfilePath)
                .Should()
                .Be($"export PATH=\"$PATH:{toolsPath.PathWithDollar}\"");
        }
    }
}
