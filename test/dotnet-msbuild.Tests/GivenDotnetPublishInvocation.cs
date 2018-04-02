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

        const string ExpectedPrefix = "exec <msbuildpath> -m -v:m";

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-r", "<rid>" }, "-p:RuntimeIdentifier=<rid>")]
        [InlineData(new string[] { "--runtime", "<rid>" }, "-p:RuntimeIdentifier=<rid>")]
        [InlineData(new string[] { "-o", "<publishdir>" }, "-p:PublishDir=<publishdir>")]
        [InlineData(new string[] { "--output", "<publishdir>" }, "-p:PublishDir=<publishdir>")]
        [InlineData(new string[] { "-c", "<config>" }, "-p:Configuration=<config>")]
        [InlineData(new string[] { "--configuration", "<config>" }, "-p:Configuration=<config>")]
        [InlineData(new string[] { "--version-suffix", "<versionsuffix>" }, "-p:VersionSuffix=<versionsuffix>")]
        [InlineData(new string[] { "--manifest", "<manifestfiles>" }, "-p:TargetManifestFiles=<manifestfiles>")]
        [InlineData(new string[] { "-v", "minimal" }, "-verbosity:minimal")]
        [InlineData(new string[] { "--verbosity", "minimal" }, "-verbosity:minimal")]
        [InlineData(new string[] { "<project>" }, "<project>")]
        [InlineData(new string[] { "<project>", "<extra-args>" }, "<project> <extra-args>")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            var command = PublishCommand.FromArgs(args, msbuildPath);

            command.SeparateRestoreCommand
                   .Should()
                   .BeNull();

            command.GetProcessStartInfo()
                   .Arguments.Should()
                   .Be($"{ExpectedPrefix} -restore -t:Publish{expectedAdditionalArgs}");
        }

        [Theory]
        [InlineData(new string[] { "-f", "<tfm>" }, "-p:TargetFramework=<tfm>")]
        [InlineData(new string[] { "--framework", "<tfm>" }, "-p:TargetFramework=<tfm>")]
        public void MsbuildInvocationIsCorrectForSeparateRestore(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            var command = PublishCommand.FromArgs(args, msbuildPath);

            command.SeparateRestoreCommand
                   .GetProcessStartInfo()
                   .Arguments.Should()
                   .Be($"{ExpectedPrefix} -t:Restore");

            command.GetProcessStartInfo()
                   .Arguments.Should()
                   .Be($"{ExpectedPrefix} -nologo -t:Publish{expectedAdditionalArgs}");
        }

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-f", "<tfm>" }, "-p:TargetFramework=<tfm>")]
        [InlineData(new string[] { "--framework", "<tfm>" }, "-p:TargetFramework=<tfm>")]
        [InlineData(new string[] { "-r", "<rid>" }, "-p:RuntimeIdentifier=<rid>")]
        [InlineData(new string[] { "--runtime", "<rid>" }, "-p:RuntimeIdentifier=<rid>")]
        [InlineData(new string[] { "-o", "<publishdir>" }, "-p:PublishDir=<publishdir>")]
        [InlineData(new string[] { "--output", "<publishdir>" }, "-p:PublishDir=<publishdir>")]
        [InlineData(new string[] { "-c", "<config>" }, "-p:Configuration=<config>")]
        [InlineData(new string[] { "--configuration", "<config>" }, "-p:Configuration=<config>")]
        [InlineData(new string[] { "--version-suffix", "<versionsuffix>" }, "-p:VersionSuffix=<versionsuffix>")]
        [InlineData(new string[] { "--manifest", "<manifestfiles>" }, "-p:TargetManifestFiles=<manifestfiles>")]
        [InlineData(new string[] { "-v", "minimal" }, "-verbosity:minimal")]
        [InlineData(new string[] { "--verbosity", "minimal" }, "-verbosity:minimal")]
        public void OptionForwardingIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            var expectedArgs = expectedAdditionalArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var parser = Parser.Instance;

            var result = parser.ParseFrom("dotnet publish", args);

            result["dotnet"]["publish"]
                .OptionValuesToBeForwarded()
                .Should()
                .BeEquivalentTo(expectedArgs);
        }
    }
}