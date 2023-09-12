// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using BuildCommand = Microsoft.DotNet.Tools.Build.BuildCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetOsArchOptions : SdkTest
    {
        public GivenDotnetOsArchOptions(ITestOutputHelper log) : base(log)
        {
        }

        const string ExpectedPrefix = "-maxcpucount -verbosity:m";

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetBuildInvocation));

        [Fact]
        public void OsOptionIsCorrectlyResolved()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(new string[] { "--os", "os" }, msbuildPath);
                var expectedArch = RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86";
                command.GetArgumentsToMSBuild()
                    .Should()
                    .StartWith($"{ExpectedPrefix} -restore -consoleloggerparameters:Summary -property:RuntimeIdentifier=os-{expectedArch}");
            });
        }

        [Fact]
        public void ArchOptionIsCorrectlyResolved()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(new string[] { "--arch", "arch" }, msbuildPath);
                var expectedOs = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
                    null;
                if (expectedOs == null)
                {
                    // Not a supported OS for running test
                    return;
                }
                command.GetArgumentsToMSBuild()
                    .Should()
                    .StartWith($"{ExpectedPrefix} -restore -consoleloggerparameters:Summary -property:RuntimeIdentifier={expectedOs}-arch");
            });
        }

        [Fact]
        public void OSAndArchOptionsCanBeCombined()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(new string[] { "--arch", "arch", "--os", "os" }, msbuildPath);
                command.GetArgumentsToMSBuild()
                    .Should()
                    .StartWith($"{ExpectedPrefix} -restore -consoleloggerparameters:Summary -property:RuntimeIdentifier=os-arch");
            });
        }

        [Fact]
        public void OptionsRespectUserSpecifiedSelfContained()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(new string[] { "--arch", "arch", "--os", "os", "--self-contained" }, msbuildPath);
                command.GetArgumentsToMSBuild()
                    .Should()
                    .StartWith($"{ExpectedPrefix} -restore -consoleloggerparameters:Summary -property:SelfContained=True -property:_CommandLineDefinedSelfContained=true -property:RuntimeIdentifier=os-arch");
            });
        }

        [Fact]
        public void OSOptionCannotBeCombinedWithRuntime()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var exceptionThrown = Assert.Throws<GracefulException>(() => BuildCommand.FromArgs(new string[] { "--os", "os", "--runtime", "rid" }, msbuildPath));
                exceptionThrown.Message.Should().Be(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndOsOptions);
            });
        }

        [Fact]
        public void ArchOptionCannotBeCombinedWithRuntime()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var exceptionThrown = Assert.Throws<GracefulException>(() => BuildCommand.FromArgs(new string[] { "--arch", "arch", "--runtime", "rid" }, msbuildPath));
                exceptionThrown.Message.Should().Be(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndArchOptions);
            });
        }

        [WindowsOnlyTheory]
        [InlineData("build")]
        [InlineData("publish")]
        [InlineData("test")]
        [InlineData("run")]
        public void CommandsRunWithOSOption(string command)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld", identifier: command)
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(command, "--os", "win")
                .Should()
                .Pass();
        }

        [WindowsOnlyTheory]
        [InlineData("build")]
        [InlineData("publish")]
        [InlineData("test")]
        [InlineData("run")]
        public void CommandsRunWithArchOption(string command)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld", identifier: command)
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute(command, "--arch", RuntimeInformation.ProcessArchitecture.Equals(Architecture.Arm64) ? "arm64" : Environment.Is64BitOperatingSystem ? "x64" : "x86")
                .Should()
                .Pass();
        }

        [Fact]
        public void ArchOptionsAMD64toX64()
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var msbuildPath = "<msbuildpath>";
                var command = BuildCommand.FromArgs(new string[] { "--arch", "amd64", "--os", "os" }, msbuildPath);
                command.GetArgumentsToMSBuild()
                    .Should()
                    .StartWith($"{ExpectedPrefix} -restore -consoleloggerparameters:Summary -property:RuntimeIdentifier=os-x64");
            });
        }
    }
}
