using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Tools.Build;
using FluentAssertions;
using Xunit;
using static Microsoft.DotNet.Tools.Test.Utilities.DirectoryInfoExtensions;
using WindowsOnlyFactAttribute = Microsoft.DotNet.Tools.Test.Utilities.WindowsOnlyFactAttribute;
using NonWindowsOnlyFactAttribute = Microsoft.DotNet.Tools.Test.Utilities.NonWindowsOnlyFactAttribute;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetBuildInvocation
    {
        [Theory]
        [InlineData(new string[] { }, @"exec <msbuildpath> /m /v:m /t:Build /clp:Summary")]
        [InlineData(new string[] { "-o", "foo" }, @"exec <msbuildpath> /m /v:m /t:Build /p:OutputPath=foo /clp:Summary")]
        [InlineData(new string[] { "--output", "foo" }, @"exec <msbuildpath> /m /v:m /t:Build /p:OutputPath=foo /clp:Summary")]
        [InlineData(new string[] { "-o", "foo1 foo2" }, @"exec <msbuildpath> /m /v:m /t:Build ""/p:OutputPath=foo1 foo2"" /clp:Summary")]
        [InlineData(new string[] { "--no-incremental" }, @"exec <msbuildpath> /m /v:m /t:Rebuild /clp:Summary")]
        [InlineData(new string[] { "-f", "framework" }, @"exec <msbuildpath> /m /v:m /t:Build /p:TargetFramework=framework /clp:Summary")]
        [InlineData(new string[] { "--framework", "framework" }, @"exec <msbuildpath> /m /v:m /t:Build /p:TargetFramework=framework /clp:Summary")]
        [InlineData(new string[] { "-r", "runtime" }, @"exec <msbuildpath> /m /v:m /t:Build /p:RuntimeIdentifier=runtime /clp:Summary")]
        [InlineData(new string[] { "--runtime", "runtime" }, @"exec <msbuildpath> /m /v:m /t:Build /p:RuntimeIdentifier=runtime /clp:Summary")]
        [InlineData(new string[] { "-c", "configuration" }, @"exec <msbuildpath> /m /v:m /t:Build /p:Configuration=configuration /clp:Summary")]
        [InlineData(new string[] { "--configuration", "configuration" }, @"exec <msbuildpath> /m /v:m /t:Build /p:Configuration=configuration /clp:Summary")]
        [InlineData(new string[] { "--version-suffix", "mysuffix" }, @"exec <msbuildpath> /m /v:m /t:Build /p:VersionSuffix=mysuffix /clp:Summary")]
        [InlineData(new string[] { "--no-dependencies" }, @"exec <msbuildpath> /m /v:m /t:Build /p:BuildProjectReferences=false /clp:Summary")]
        [InlineData(new string[] { "-v", "verbosity" }, @"exec <msbuildpath> /m /v:m /t:Build /verbosity:verbosity /clp:Summary")]
        [InlineData(new string[] { "--verbosity", "verbosity" }, @"exec <msbuildpath> /m /v:m /t:Build /verbosity:verbosity /clp:Summary")]
        [InlineData(new string[] { "--no-incremental", "-o", "myoutput", "-r", "myruntime", "-v", "diag" }, @"exec <msbuildpath> /m /v:m /t:Rebuild /p:OutputPath=myoutput /p:RuntimeIdentifier=myruntime /verbosity:diag /clp:Summary")]
        public void WhenArgsArePassedThenMsbuildInvocationIsCorrect(string[] args, string expectedCommand)
        {
            var msbuildPath = "<msbuildpath>";
            BuildCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be(expectedCommand);
        }

        [WindowsOnlyFact]
        public void WhenInvokingBuildCommandOnWindowsTheDotnetIsExecuted()
        {
            var msbuildPath = "<msbuildpath>";
            BuildCommand.FromArgs(new string[0], msbuildPath)
                .GetProcessStartInfo().FileName.Should().Be("dotnet.exe");
        }

        [NonWindowsOnlyFact]
        public void WhenInvokingBuildCommandOnNonWindowsTheDotnetIsExecuted()
        {
            var msbuildPath = "<msbuildpath>";
            BuildCommand.FromArgs(new string[0], msbuildPath)
                .GetProcessStartInfo().FileName.Should().Be("dotnet");
        }

        [Theory]
        [InlineData("MSBuildExtensionsPath")]
        [InlineData("CscToolExe")]
        [InlineData("MSBuildSDKsPath")]
        [InlineData("DOTNET_CLI_TELEMETRY_SESSIONID")]
        public void WhenInvokingBuildCommandItSetsEnvironmentalVariables(string envVarName)
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = BuildCommand.FromArgs(new string[0], msbuildPath).GetProcessStartInfo();
            startInfo.Environment.ContainsKey(envVarName).Should().BeTrue();
        }

        [Fact]
        public void WhenInvokingBuildCommandItSetsMSBuildExtensionPathToExistingPath()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "MSBuildExtensionsPath";
            new DirectoryInfo(BuildCommand.FromArgs(new string[0], msbuildPath)
                                .GetProcessStartInfo()
                                .Environment[envVar])
                .Should()
                .Exist();
        }

        [Fact]
        public void WhenInvokingBuildCommandItSetsMSBuildSDKsPathToExistingPath()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "MSBuildSDKsPath";
            new DirectoryInfo(BuildCommand.FromArgs(new string[0], msbuildPath)
                                .GetProcessStartInfo()
                                .Environment[envVar])
                .Should()
                .Exist();
        }

        [Fact]
        public void WhenInvokingBuildCommandItSetsCscToolExePathToValidPath()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "CscToolExe";
            new FileInfo(BuildCommand.FromArgs(new string[0], msbuildPath)
                                .GetProcessStartInfo()
                                .Environment[envVar])
                .Should().NotBeNull("constructor will throw on invalid path");
        }

        [Fact]
        public void WhenInvokingBuildCommandItSetsTelemetryEnvVar()
        {
            var msbuildPath = "<msbuildpath>";
            var envVar = "DOTNET_CLI_TELEMETRY_SESSIONID";
            var startInfo = BuildCommand.FromArgs(new string[0], msbuildPath).GetProcessStartInfo();
            (startInfo.Environment[envVar] == null || int.TryParse(startInfo.Environment[envVar], out _))
                .Should().BeTrue("DOTNET_CLI_TELEMETRY_SESSIONID should be null or current session id");
        }

        [Fact]
        public void WhenInvokingBuildCommandItDoesNotSetCurrentWorkingDirectory()
        {
            var msbuildPath = "<msbuildpath>";
            var startInfo = BuildCommand.FromArgs(new string[0], msbuildPath)
                .GetProcessStartInfo().WorkingDirectory.Should().Be("");
        }
    }
}
