// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class OsxEnvironmentPathTests
    {
        [UnixOnlyFact]
        public void GivenPathNotSetItPrintsManualInstructions()
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            var environmentPath = new OsxBashEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                FileSystemMockBuilder.Empty.File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(
                string.Format(
                    CommonLocalizableStrings.EnvironmentPathOSXBashManualInstructions,
                    toolsPath.Path));
        }

        [UnixOnlyFact]
        public void GivenPathNotSetAndProfileExistsItPrintsReopenMessage()
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            var environmentPath = new OsxBashEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                new FileSystemMockBuilder()
                    .AddFile(OsxBashEnvironmentPath.DotnetCliToolsPathsDPath, "")
                    .Build()
                    .File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().Equal(CommonLocalizableStrings.EnvironmentPathOSXNeedReopen);
        }

        [UnixOnlyTheory]
        [InlineData("/home/user/.dotnet/tools")]
        [InlineData("~/.dotnet/tools")]
        public void GivenPathSetItPrintsNothing(string toolsDirectoryOnPath)
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue + ":" + toolsDirectoryOnPath);

            var environmentPath = new OsxBashEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                FileSystemMockBuilder.Empty.File);

            environmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            reporter.Lines.Should().BeEmpty();
        }

        [UnixOnlyFact]
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

            var environmentPath = new OsxBashEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                fileSystem);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();

            fileSystem
                .Exists(OsxBashEnvironmentPath.DotnetCliToolsPathsDPath)
                .Should()
                .Be(false);
        }

        [UnixOnlyFact]
        public void GivenPathNotSetItAddsToEnvironment()
        {
            var reporter = new BufferedReporter();
            var toolsPath = new BashPathUnderHomeDirectory("/home/user", ".dotnet/tools");
            var pathValue = @"/usr/bin";
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);
            IFileSystem fileSystem = new FileSystemMockBuilder().Build();
            fileSystem.Directory.CreateDirectory("/etc/paths.d");

            provider
                .Setup(p => p.GetEnvironmentVariable("PATH"))
                .Returns(pathValue);

            var environmentPath = new OsxBashEnvironmentPath(
                toolsPath,
                reporter,
                provider.Object,
                fileSystem.File);

            environmentPath.AddPackageExecutablePathToUserPath();

            reporter.Lines.Should().BeEmpty();

            fileSystem
                .File
                .ReadAllText(OsxBashEnvironmentPath.DotnetCliToolsPathsDPath)
                .Should()
                .Be(toolsPath.PathWithTilde);
        }
    }
}
