// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Clean;
using FluentAssertions;
using Xunit;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetCleanInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        const string ExpectedPrefix = "-maxcpucount -verbosity:m -verbosity:normal -target:Clean";

        private static readonly string WorkingDirectory = 
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetCleanInvocation));

        [Fact]
        public void ItAddsProjectToMsbuildInvocation()
        {
            var msbuildPath = "<msbuildpath>";
            CleanCommand.FromArgs(new string[] { "<project>" }, msbuildPath)
                .GetArgumentsToMSBuild().Should().Be("-maxcpucount -verbosity:m -verbosity:normal <project> -target:Clean");
        }

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-o", "<output>" }, "-property:OutputPath=<cwd><output>")]
        [InlineData(new string[] { "--output", "<output>" }, "-property:OutputPath=<cwd><output>")]
        [InlineData(new string[] { "-f", "<framework>" }, "-property:TargetFramework=<framework>")]
        [InlineData(new string[] { "--framework", "<framework>" }, "-property:TargetFramework=<framework>")]
        [InlineData(new string[] { "-c", "<configuration>" }, "-property:Configuration=<configuration>")]
        [InlineData(new string[] { "--configuration", "<configuration>" }, "-property:Configuration=<configuration>")]
        [InlineData(new string[] { "-v", "diag" }, "-verbosity:diag")]
        [InlineData(new string[] { "--verbosity", "diag" }, "-verbosity:diag")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory);

                var msbuildPath = "<msbuildpath>";
                CleanCommand.FromArgs(args, msbuildPath)
                    .GetArgumentsToMSBuild().Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
            });
        }
    }
}
