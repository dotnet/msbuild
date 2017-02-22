using Microsoft.DotNet.Tools.Restore;
using FluentAssertions;
using Xunit;
using System;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetRestoreInvocation
    {
        const string ExpectedPrefix = "exec <msbuildpath> /m /v:m /NoLogo /t:Restore /ConsoleLoggerParameters:Verbosity=Minimal";

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-s", "<source>" }, "/p:RestoreSources=<source>")]
        [InlineData(new string[] { "--source", "<source>" }, "/p:RestoreSources=<source>")]
        [InlineData(new string[] { "-s", "<source0>", "-s", "<source1>" }, "/p:RestoreSources=<source0>%3B<source1>")]
        [InlineData(new string[] { "-r", "<runtime>" }, "/p:RuntimeIdentifiers=<runtime>")]
        [InlineData(new string[] { "--runtime", "<runtime>" }, "/p:RuntimeIdentifiers=<runtime>")]
        [InlineData(new string[] { "-r", "<runtime0>", "-r", "<runtime1>" }, "/p:RuntimeIdentifiers=<runtime0>%3B<runtime1>")]
        [InlineData(new string[] { "--packages", "<packages>" }, "/p:RestorePackagesPath=<packages>")]
        [InlineData(new string[] { "--disable-parallel" }, "/p:RestoreDisableParallel=true")]
        [InlineData(new string[] { "--configfile", "<config>" }, "/p:RestoreConfigFile=<config>")]
        [InlineData(new string[] { "--no-cache" }, "/p:RestoreNoCache=true")]
        [InlineData(new string[] { "--ignore-failed-sources" }, "/p:RestoreIgnoreFailedSources=true")]
        [InlineData(new string[] { "--no-dependencies" }, "/p:RestoreRecursive=false")]
        [InlineData(new string[] { "-v", "<verbosity>" }, @"/verbosity:<verbosity>")]
        [InlineData(new string[] { "--verbosity", "<verbosity>" }, @"/verbosity:<verbosity>")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            RestoreCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
        }
    }
}
