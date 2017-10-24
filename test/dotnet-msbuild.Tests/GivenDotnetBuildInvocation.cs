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

        [Theory]
        [InlineData(new string[] { }, "/t:Build")]
        [InlineData(new string[] { "-o", "foo" }, "/t:Build /p:OutputPath=foo")]
        [InlineData(new string[] { "-p:Verbosity=diag" }, "/t:Build -p:Verbosity=diag")]
        [InlineData(new string[] { "--output", "foo" }, "/t:Build /p:OutputPath=foo")]
        [InlineData(new string[] { "-o", "foo1 foo2" }, "/t:Build \"/p:OutputPath=foo1 foo2\"")]
        [InlineData(new string[] { "--no-incremental" }, "/t:Rebuild")]
        [InlineData(new string[] { "-r", "rid" }, "/t:Build /p:RuntimeIdentifier=rid")]
        [InlineData(new string[] { "--runtime", "rid" }, "/t:Build /p:RuntimeIdentifier=rid")]
        [InlineData(new string[] { "-c", "config" }, "/t:Build /p:Configuration=config")]
        [InlineData(new string[] { "--configuration", "config" }, "/t:Build /p:Configuration=config")]
        [InlineData(new string[] { "--version-suffix", "mysuffix" }, "/t:Build /p:VersionSuffix=mysuffix")]
        [InlineData(new string[] { "--no-dependencies" }, "/t:Build /p:BuildProjectReferences=false")]
        [InlineData(new string[] { "-v", "diag" }, "/t:Build /verbosity:diag")]
        [InlineData(new string[] { "--verbosity", "diag" }, "/t:Build /verbosity:diag")]
        [InlineData(new string[] { "--no-incremental", "-o", "myoutput", "-r", "myruntime", "-v", "diag", "/ArbitrarySwitchForMSBuild" },
                                  "/t:Rebuild /p:OutputPath=myoutput /p:RuntimeIdentifier=myruntime /verbosity:diag /ArbitrarySwitchForMSBuild")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            var command = BuildCommand.FromArgs(args, msbuildPath);

            command.SeparateRestoreCommand.Should().BeNull();

            command.GetProcessStartInfo()
                   .Arguments.Should()
                   .Be($"{ExpectedPrefix} /restore /clp:Summary{expectedAdditionalArgs}");
        }

        [Theory]
        [InlineData(new string[] { "-f", "tfm" }, "/t:Restore", "/t:Build /p:TargetFramework=tfm")]
        [InlineData(new string[] { "-o", "myoutput", "-f", "tfm", "-v", "diag", "/ArbitrarySwitchForMSBuild" },
                                  "/t:Restore /p:OutputPath=myoutput /verbosity:diag /ArbitrarySwitchForMSBuild",
                                  "/t:Build /p:OutputPath=myoutput /p:TargetFramework=tfm /verbosity:diag /ArbitrarySwitchForMSBuild")]
        public void MsbuildInvocationIsCorrectForSeparateRestore(
            string[] args, 
            string expectedAdditionalArgsForRestore, 
            string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            var command = BuildCommand.FromArgs(args, msbuildPath);

            command.SeparateRestoreCommand.GetProcessStartInfo()
                   .Arguments.Should()
                   .Be($"{ExpectedPrefix} {expectedAdditionalArgsForRestore}");

            command.GetProcessStartInfo()
                   .Arguments.Should()
                   .Be($"{ExpectedPrefix} /nologo /clp:Summary{expectedAdditionalArgs}");

        }
    }
}
