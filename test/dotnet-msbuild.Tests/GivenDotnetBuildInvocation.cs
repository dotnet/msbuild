using Microsoft.DotNet.Tools.Build;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetBuildInvocation
    {
        [Theory]
        [InlineData(new string[] { }, @"exec <msbuildpath> /m /v:m /t:Build /clp:Summary")]
        [InlineData(new string[] { "-o", "foo" }, @"exec <msbuildpath> /m /v:m /t:Build /p:OutputPath=foo /clp:Summary")]
        [InlineData(new string[] { "--output", "foo" }, @"exec <msbuildpath> /m /v:m /t:Build /p:OutputPath=foo /clp:Summary")]
        [InlineData(new string[] { "-o", "foo1 foo2" }, @"exec <msbuildpath> /m /v:m /t:Build ""/p:OutputPath=foo1 foo2"" /clp:Summary")]
        public void WhenNoArgsArePassedThenMsbuildInvocationIsCorrect(string[] args, string expectedCommand)
        {
            var msbuildPath = "<msbuildpath>";
            BuildCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be(expectedCommand);
        }
    }
}
