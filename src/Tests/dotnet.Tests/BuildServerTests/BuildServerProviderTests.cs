// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Moq;
using LocalizableStrings = Microsoft.DotNet.BuildServer.LocalizableStrings;

namespace Microsoft.DotNet.Tests.BuildServerTests
{
    public class BuildServerProviderTests : SdkTest
    {
        public BuildServerProviderTests(ITestOutputHelper log) : base(log)
        {

        }

        [Fact]
        public void GivenMSBuildFlagItYieldsMSBuild()
        {
            var provider = new BuildServerProvider(
                new FileSystemMockBuilder().Build(),
                CreateEnvironmentProviderMock().Object);

            provider
                .EnumerateBuildServers(ServerEnumerationFlags.MSBuild)
                .Select(s => s.Name)
                .Should()
                .Equal(LocalizableStrings.MSBuildServer);
        }

        [Fact]
        public void GivenVBCSCompilerFlagItYieldsVBCSCompiler()
        {
            var provider = new BuildServerProvider(
                new FileSystemMockBuilder().Build(),
                CreateEnvironmentProviderMock().Object);

            provider
                .EnumerateBuildServers(ServerEnumerationFlags.VBCSCompiler)
                .Select(s => s.Name)
                .Should()
                .Equal(LocalizableStrings.VBCSCompilerServer);
        }

        [Fact]
        public void GivenRazorFlagAndNoPidDirectoryTheEnumerationIsEmpty()
        {
            var provider = new BuildServerProvider(
                new FileSystemMockBuilder().Build(),
                CreateEnvironmentProviderMock().Object);

            provider
                .EnumerateBuildServers(ServerEnumerationFlags.Razor)
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void GivenNoEnvironmentVariableItUsesTheDefaultPidDirectory()
        {
            var provider = new BuildServerProvider(
                new FileSystemMockBuilder().Build(),
                CreateEnvironmentProviderMock().Object);

            provider
                .GetPidFileDirectory()
                .Value
                .Should()
                .Be(Path.Combine(
                    CliFolderPathCalculator.DotnetUserProfileFolderPath,
                    "pids",
                    "build"));
        }

        [Fact]
        public void GivenEnvironmentVariableItUsesItForThePidDirectory()
        {
            IFileSystem fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            var pidDirectory = Path.Combine(fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath, "path/to/some/directory");
            var provider = new BuildServerProvider(
                fileSystem,
                CreateEnvironmentProviderMock(pidDirectory).Object);

            provider
                .GetPidFileDirectory()
                .Value
                .Should()
                .Be(pidDirectory);
        }

        [Fact]
        public void GivenARazorPidFileItReturnsARazorBuildServer()
        {
            const int ProcessId = 1234;
            const string PipeName = "some-pipe-name";

            string pidDirectory = Path.GetFullPath("var/pids/build");
            string pidFilePath = Path.Combine(pidDirectory, $"{RazorPidFile.FilePrefix}{ProcessId}");

            var fileSystemMockBuilder = new FileSystemMockBuilder();
            fileSystemMockBuilder.UseCurrentSystemTemporaryDirectory();
            var serverPath = Path.Combine(fileSystemMockBuilder.TemporaryFolder, "path/to/rzc.dll");

            IFileSystem fileSystemMock = fileSystemMockBuilder.AddFile(
                    pidFilePath,
                    $"{ProcessId}{Environment.NewLine}{RazorPidFile.RazorServerType}{Environment.NewLine}{serverPath}{Environment.NewLine}{PipeName}")
                .AddFile(
                    Path.Combine(pidDirectory, $"{RazorPidFile.FilePrefix}not-a-pid-file"),
                    "not-a-pid-file")
                    .Build();

            var provider = new BuildServerProvider(
                fileSystemMock,
                CreateEnvironmentProviderMock(pidDirectory).Object);

            var servers = provider.EnumerateBuildServers(ServerEnumerationFlags.Razor).ToArray();
            servers.Length.Should().Be(1);

            var razorServer = servers.First() as RazorServer;
            razorServer.Should().NotBeNull();
            razorServer.ProcessId.Should().Be(ProcessId);
            razorServer.Name.Should().Be(LocalizableStrings.RazorServer);
            razorServer.PidFile.Should().NotBeNull();
            razorServer.PidFile.Path.Value.Should().Be(pidFilePath);
            razorServer.PidFile.ProcessId.Should().Be(ProcessId);
            razorServer.PidFile.ServerPath.Value.Should().Be(serverPath);
            razorServer.PidFile.PipeName.Should().Be(PipeName);
        }

        [Theory]
        [InlineData(typeof(UnauthorizedAccessException))]
        [InlineData(typeof(IOException))]
        public void GivenAnExceptionAccessingTheRazorPidFileItPrintsAWarning(Type exceptionType)
        {
            const int ProcessId = 1234;
            const string ErrorMessage = "failed!";

            string pidDirectory = Path.GetFullPath("var/pids/build");
            string pidFilePath = Path.Combine(pidDirectory, $"{RazorPidFile.FilePrefix}{ProcessId}");

            var directoryMock = new Mock<IDirectory>();
            directoryMock.Setup(d => d.Exists(pidDirectory)).Returns(true);
            directoryMock.Setup(d => d.EnumerateFiles(pidDirectory)).Returns(new [] { pidFilePath });

            var fileMock = new Mock<IFile>();
            fileMock
                .Setup(f => f.OpenFile(
                    pidFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Write | FileShare.Delete,
                    4096,
                    FileOptions.None))
                .Throws((Exception)Activator.CreateInstance(exceptionType, new object[] { ErrorMessage } ));

            var fileSystemMock = new Mock<IFileSystem>();
            fileSystemMock.SetupGet(fs => fs.Directory).Returns(directoryMock.Object);
            fileSystemMock.SetupGet(fs => fs.File).Returns(fileMock.Object);

            var reporter = new BufferedReporter();

            var provider = new BuildServerProvider(
                fileSystemMock.Object,
                CreateEnvironmentProviderMock(pidDirectory).Object,
                reporter);

            var servers = provider.EnumerateBuildServers(ServerEnumerationFlags.Razor).ToArray();
            servers.Should().BeEmpty();

            reporter.Lines.Should().Equal(
                string.Format(
                    LocalizableStrings.FailedToReadPidFile,
                    pidFilePath,
                    ErrorMessage).Yellow());
        }

        private Mock<IEnvironmentProvider> CreateEnvironmentProviderMock(string value = null)
        {
            var provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable(BuildServerProvider.PidFileDirectoryVariableName))
                .Returns(value);

            return provider;
        }
    }
}
