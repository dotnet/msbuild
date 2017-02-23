using Microsoft.DotNet.Tools.Publish;
using FluentAssertions;
using Xunit;
using System;
using System.Linq;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetPublishInvocation
    {
        const string ExpectedPrefix = "exec <msbuildpath> /m /v:m /t:Publish";

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-f", "<framework>" }, "/p:TargetFramework=<framework>")]
        [InlineData(new string[] { "--framework", "<framework>" }, "/p:TargetFramework=<framework>")]
        [InlineData(new string[] { "-r", "<runtime>" }, "/p:RuntimeIdentifier=<runtime>")]
        [InlineData(new string[] { "--runtime", "<runtime>" }, "/p:RuntimeIdentifier=<runtime>")]
        [InlineData(new string[] { "-o", "<output>" }, "/p:PublishDir=<output>")]
        [InlineData(new string[] { "--output", "<output>" }, "/p:PublishDir=<output>")]
        [InlineData(new string[] { "-c", "<configuration>" }, "/p:Configuration=<configuration>")]
        [InlineData(new string[] { "--configuration", "<configuration>" }, "/p:Configuration=<configuration>")]
        [InlineData(new string[] { "--version-suffix", "<version-suffix>" }, "/p:VersionSuffix=<version-suffix>")]
        [InlineData(new string[] { "--filter", "<filter>" }, "/p:FilterProjFile=<filter>")]
        [InlineData(new string[] { "-v", "<verbosity>" }, "/verbosity:<verbosity>")]
        [InlineData(new string[] { "--verbosity", "<verbosity>" }, "/verbosity:<verbosity>")]
        [InlineData(new string[] { "<project>" }, "<project>")]
        [InlineData(new string[] { "<project>", "<extra-args>" }, "<project> <extra-args>")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            PublishCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
        }
    }
}
