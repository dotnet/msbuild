// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Clean;
using FluentAssertions;
using Xunit;
using System;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetCleanInvocation
    {
        const string ExpectedPrefix = "exec <msbuildpath> -maxcpucount -verbosity:m -verbosity:normal -target:Clean";

        [Fact]
        public void ItAddsProjectToMsbuildInvocation()
        {
            var msbuildPath = "<msbuildpath>";
            CleanCommand.FromArgs(new string[] { "<project>" }, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be("exec <msbuildpath> -maxcpucount -verbosity:m -verbosity:normal <project> -target:Clean");
        }

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-o", "<output>" }, @"-property:OutputPath=\""<output>\""")]
        [InlineData(new string[] { "--output", "<output>" }, @"-property:OutputPath=\""<output>\""")]
        [InlineData(new string[] { "-f", "<framework>" }, @"-property:TargetFramework=\""<framework>\""")]
        [InlineData(new string[] { "--framework", "<framework>" }, @"-property:TargetFramework=\""<framework>\""")]
        [InlineData(new string[] { "-c", "<configuration>" }, @"-property:Configuration=\""<configuration>\""")]
        [InlineData(new string[] { "--configuration", "<configuration>" }, @"-property:Configuration=\""<configuration>\""")]
        [InlineData(new string[] { "-v", "diag" }, "-verbosity:diag")]
        [InlineData(new string[] { "--verbosity", "diag" }, "-verbosity:diag")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            CleanCommand.FromArgs(args, msbuildPath)
                .GetProcessStartInfo().Arguments.Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
        }
    }
}
