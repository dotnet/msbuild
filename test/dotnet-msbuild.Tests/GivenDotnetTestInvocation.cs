using Microsoft.DotNet.Tools.Build;
using FluentAssertions;
using Xunit;
using System;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetTestInvocation
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
        public void MsbuildInvocationIsCorrect(string[] args, string expectedCommand)
        {
            var msbuildPath = "<msbuildpath>";
            BuildCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be(expectedCommand);
            throw new NotImplementedException();
        }
    }
}
