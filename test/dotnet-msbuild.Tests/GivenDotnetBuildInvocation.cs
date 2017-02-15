using Microsoft.DotNet.Tools.Build;
using FluentAssertions;
using Xunit;
using WindowsOnlyFactAttribute = Microsoft.DotNet.Tools.Test.Utilities.WindowsOnlyFactAttribute;
using NonWindowsOnlyFactAttribute = Microsoft.DotNet.Tools.Test.Utilities.NonWindowsOnlyFactAttribute;
using System.Collections.Generic;

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

        [Fact]
        public void WhenInvokingBuildCommandItSetsEnvironmentVariables()
        {
            var expectedEnvironmentalVariables = new HashSet<string> {
                "MSBuildExtensionsPath",
                "CscToolExe",
                "MSBuildSDKsPath",
                "DOTNET_CLI_TELEMETRY_SESSIONID"
            };

            var msbuildPath = "<msbuildpath>";
            var startInfo = BuildCommand.FromArgs(new string[0], msbuildPath).GetProcessStartInfo();

            foreach (var envVarName in expectedEnvironmentalVariables)
            {
                startInfo.Environment.ContainsKey(envVarName).Should().BeTrue();
            }
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
