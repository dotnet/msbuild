// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Publish;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetPublishInvocation
    {
        private readonly ITestOutputHelper output;

        public GivenDotnetPublishInvocation(ITestOutputHelper output)
        {
            this.output = output;
        }

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
        [InlineData(new string[] { "--filter", "<filter>" }, "/p:FilterProjectFiles=<filter>")]
        [InlineData(new string[] { "-v", "minimal" }, "/verbosity:minimal")]
        [InlineData(new string[] { "--verbosity", "minimal" }, "/verbosity:minimal")]
        [InlineData(new string[] { "<project>" }, "<project>")]
        [InlineData(new string[] { "<project>", "<extra-args>" }, "<project> <extra-args>")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            PublishCommand.FromArgs(args, msbuildPath)
                          .GetProcessStartInfo()
                          .Arguments.Should()
                          .Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
        }

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
        [InlineData(new string[] { "--filter", "<filter>" }, "/p:FilterProjectFiles=<filter>")]
        [InlineData(new string[] { "-v", "minimal" }, "/verbosity:minimal")]
        [InlineData(new string[] { "--verbosity", "minimal" }, "/verbosity:minimal")]
        public void OptionForwardingIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            var expectedArgs = expectedAdditionalArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet publish", args);

            output.WriteLine(result.Diagram());

            result["dotnet"]["publish"]
                .OptionValuesToBeForwarded()
                .Should()
                .BeEquivalentTo(expectedArgs);
        }
    }
}