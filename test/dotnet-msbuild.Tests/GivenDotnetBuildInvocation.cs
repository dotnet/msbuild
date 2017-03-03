// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Build;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetBuildInvocation
    {
        const string ExpectedPrefix = "exec <msbuildpath> /m /v:m";
        const string ExpectedSuffix = "/clp:Summary";

        [Theory]
        [InlineData(new string[] { }, "/t:Build")]
        [InlineData(new string[] { "-o", "foo" }, "/t:Build /p:OutputPath=foo")]
        [InlineData(new string[] { "--output", "foo" }, "/t:Build /p:OutputPath=foo")]
        [InlineData(new string[] { "-o", "foo1 foo2" }, "/t:Build \"/p:OutputPath=foo1 foo2\"")]
        [InlineData(new string[] { "--no-incremental" }, "/t:Rebuild")]
        [InlineData(new string[] { "-f", "framework" }, "/t:Build /p:TargetFramework=framework")]
        [InlineData(new string[] { "--framework", "framework" }, "/t:Build /p:TargetFramework=framework")]
        [InlineData(new string[] { "-r", "runtime" }, "/t:Build /p:RuntimeIdentifier=runtime")]
        [InlineData(new string[] { "--runtime", "runtime" }, "/t:Build /p:RuntimeIdentifier=runtime")]
        [InlineData(new string[] { "-c", "configuration" }, "/t:Build /p:Configuration=configuration")]
        [InlineData(new string[] { "--configuration", "configuration" }, "/t:Build /p:Configuration=configuration")]
        [InlineData(new string[] { "--version-suffix", "mysuffix" }, "/t:Build /p:VersionSuffix=mysuffix")]
        [InlineData(new string[] { "--no-dependencies" }, "/t:Build /p:BuildProjectReferences=false")]
        [InlineData(new string[] { "-v", "verbosity" }, "/t:Build /verbosity:verbosity")]
        [InlineData(new string[] { "--verbosity", "verbosity" }, "/t:Build /verbosity:verbosity")]
        [InlineData(new string[] { "--no-incremental", "-o", "myoutput", "-r", "myruntime", "-v", "diag" }, "/t:Rebuild /p:OutputPath=myoutput /p:RuntimeIdentifier=myruntime /verbosity:diag")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            BuildCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs} {ExpectedSuffix}");
        }
    }
}
