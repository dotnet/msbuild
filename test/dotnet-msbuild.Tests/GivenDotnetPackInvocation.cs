// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Pack;
using FluentAssertions;
using Xunit;
using System;
using System.Linq;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetPackInvocation
    {
        const string ExpectedPrefix = "exec <msbuildpath> /m /v:m /t:pack";

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-o", "<output>" }, "/p:PackageOutputPath=<output>")]
        [InlineData(new string[] { "--output", "<output>" }, "/p:PackageOutputPath=<output>")]
        [InlineData(new string[] { "--no-build" }, "/p:NoBuild=true")]
        [InlineData(new string[] { "--include-symbols" }, "/p:IncludeSymbols=true")]
        [InlineData(new string[] { "--include-source" }, "/p:IncludeSource=true")]
        [InlineData(new string[] { "-c", "<configuration>" }, "/p:Configuration=<configuration>")]
        [InlineData(new string[] { "--configuration", "<configuration>" }, "/p:Configuration=<configuration>")]
        [InlineData(new string[] { "--version-suffix", "<version-suffix>" }, "/p:VersionSuffix=<version-suffix>")]
        [InlineData(new string[] { "-s" }, "/p:Serviceable=true")]
        [InlineData(new string[] { "--serviceable" }, "/p:Serviceable=true")]
        [InlineData(new string[] { "-v", "<verbosity>" }, @"/verbosity:<verbosity>")]
        [InlineData(new string[] { "--verbosity", "<verbosity>" }, @"/verbosity:<verbosity>")]
        [InlineData(new string[] { "<project>" }, "<project>")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            PackCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
        }
    }
}
