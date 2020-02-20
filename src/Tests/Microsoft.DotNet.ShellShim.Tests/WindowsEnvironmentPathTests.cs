// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework.Utilities;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class WindowsEnvironmentPathTests
    {
        [Fact]
        public void GivenPathNotSetItPrintsManualInstructions()
        {
            var reporter = new BufferedReporter();
            var toolsPath = @"C:\Tools";
            var pathValue = @"C:\Other";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable(
                    "PATH",
                    It.Is<EnvironmentVariableTarget>(t =>
                        t == EnvironmentVariableTarget.Process ||
                        t == EnvironmentVariableTarget.User ||
                        t == EnvironmentVariableTarget.Machine)))
                .Returns(pathValue);

            var environmentPath = new WindowsEnvironmentPath(toolsPath, reporter, provider.Object);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(
                string.Format(
                    CommonLocalizableStrings.EnvironmentPathWindowsManualInstructions,
                    toolsPath));
        }

        [Fact]
        public void GivenPathNotSetInProcessItPrintsReopenNotice()
        {
            var reporter = new BufferedReporter();
            var toolsPath = @"C:\Tools";
            var pathValue = @"C:\Other";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process))
                .Returns(pathValue);

            provider
                .Setup(p => p.GetEnvironmentVariable(
                    "PATH",
                    It.Is<EnvironmentVariableTarget>(t =>
                        t == EnvironmentVariableTarget.User ||
                        t == EnvironmentVariableTarget.Machine)))
                .Returns(pathValue + ";" + toolsPath);

            var environmentPath = new WindowsEnvironmentPath(toolsPath, reporter, provider.Object);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(CommonLocalizableStrings.EnvironmentPathWindowsNeedReopen);
        }

        [Fact]
        public void GivenPathSetInProcessAndEnvironmentItPrintsNothing()
        {
            var reporter = new BufferedReporter();
            var toolsPath = @"C:\Tools";
            var pathValue = @"C:\Other";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable(
                    "PATH",
                    It.Is<EnvironmentVariableTarget>(t =>
                        t == EnvironmentVariableTarget.Process ||
                        t == EnvironmentVariableTarget.User ||
                        t == EnvironmentVariableTarget.Machine)))
                .Returns(pathValue + ";" + toolsPath);

            var environmentPath = new WindowsEnvironmentPath(toolsPath, reporter, provider.Object);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().BeEmpty();
        }

        [Fact]
        public void GivenPathSetItDoesNotAddPathToEnvironment()
        {
            var reporter = new BufferedReporter();
            var toolsPath = @"C:\Tools";
            var pathValue = @"C:\Other";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable(
                    "PATH",
                    It.Is<EnvironmentVariableTarget>(t =>
                        t == EnvironmentVariableTarget.Process ||
                        t == EnvironmentVariableTarget.User ||
                        t == EnvironmentVariableTarget.Machine)))
                .Returns(pathValue + ";" + toolsPath);

            var environmentPath = new WindowsEnvironmentPath(toolsPath, reporter, provider.Object);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();
        }

        [Fact]
        public void GivenPathNotSetItAddsToEnvironment()
        {
            var reporter = new BufferedReporter();
            var toolsPath = @"C:\Tools";
            var pathValue = @"C:\Other";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable(
                    "PATH",
                    It.Is<EnvironmentVariableTarget>(t =>
                        t == EnvironmentVariableTarget.Process ||
                        t == EnvironmentVariableTarget.User ||
                        t == EnvironmentVariableTarget.Machine)))
                .Returns(pathValue);
            provider
                .Setup(p => p.SetEnvironmentVariable("PATH", pathValue + ";" + toolsPath, EnvironmentVariableTarget.User));

            var environmentPath = new WindowsEnvironmentPath(toolsPath, reporter, provider.Object);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();
        }

        [Fact]
        public void GivenSecurityExceptionItPrintsWarning()
        {
            var reporter = new BufferedReporter();
            var toolsPath = @"C:\Tools";
            var pathValue = @"C:\Other";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable(
                    "PATH",
                    It.Is<EnvironmentVariableTarget>(t =>
                        t == EnvironmentVariableTarget.Process ||
                        t == EnvironmentVariableTarget.User ||
                        t == EnvironmentVariableTarget.Machine)))
                .Returns(pathValue);
            provider
                .Setup(p => p.SetEnvironmentVariable("PATH", pathValue + ";" + toolsPath, EnvironmentVariableTarget.User))
                .Throws(new System.Security.SecurityException());

            var environmentPath = new WindowsEnvironmentPath(toolsPath, reporter, provider.Object);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().Equal(
                string.Format(
                    CommonLocalizableStrings.FailedToSetToolsPathEnvironmentVariable,
                    toolsPath).Yellow());
        }
    }
}
