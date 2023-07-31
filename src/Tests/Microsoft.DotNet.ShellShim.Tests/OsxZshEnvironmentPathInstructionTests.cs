// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Moq;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class OsxZshEnvironmentPathInstructionTests
    {
        [UnixOnlyFact]
        public void GivenPathNotSetItPrintsManualInstructions()
        {
            BufferedReporter reporter = new BufferedReporter();
            BashPathUnderHomeDirectory toolsPath = new BashPathUnderHomeDirectory(
                "/home/user",
                ".dotnet/tools");
            string pathValue = @"/usr/bin";
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns("/bin/bash");

            OsxZshEnvironmentPathInstruction environmentPath = new OsxZshEnvironmentPathInstruction(
                toolsPath,
                reporter,
                provider.Object);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(
                string.Format(
                    CommonLocalizableStrings.EnvironmentPathOSXZshManualInstructions,
                    toolsPath.Path));
        }

        [UnixOnlyTheory]
        [InlineData("/home/user/.dotnet/tools")]
        public void GivenPathSetItPrintsNothing(string toolsDirectoryOnPath)
        {
            BufferedReporter reporter = new BufferedReporter();
            BashPathUnderHomeDirectory toolsPath = new BashPathUnderHomeDirectory(
                "/home/user",
                ".dotnet/tools");
            string pathValue = @"/usr/bin";
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue + ":" + toolsDirectoryOnPath);

            OsxZshEnvironmentPathInstruction environmentPath = new OsxZshEnvironmentPathInstruction(
                toolsPath,
                reporter,
                provider.Object);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().BeEmpty();
        }

        [UnixOnlyTheory]
        [InlineData("~/.dotnet/tools")]
        public void GivenPathSetItPrintsInstruction(string toolsDirectoryOnPath)
        {
            BufferedReporter reporter = new BufferedReporter();
            BashPathUnderHomeDirectory toolsPath = new BashPathUnderHomeDirectory(
                "/home/user",
                ".dotnet/tools");
            string pathValue = @"/usr/bin";
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue + ":" + toolsDirectoryOnPath);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns("/bin/zsh");

            OsxZshEnvironmentPathInstruction environmentPath = new OsxZshEnvironmentPathInstruction(
                toolsPath,
                reporter,
                provider.Object);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(
                string.Format(
                    CommonLocalizableStrings.EnvironmentPathOSXZshManualInstructions,
                    toolsPath.Path));
        }
    }
}
