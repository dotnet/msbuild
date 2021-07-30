// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using System.Linq;
using Microsoft.DotNet.Tools.Publish;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetPublishInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetPublishInvocation));
        private readonly ITestOutputHelper output;

        public GivenDotnetPublishInvocation(ITestOutputHelper output)
        {
            this.output = output;
        }

        const string ExpectedPrefix = "-maxcpucount -verbosity:m";

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-r", "<rid>" }, "-property:RuntimeIdentifier=<rid> -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--runtime", "<rid>" }, "-property:RuntimeIdentifier=<rid> -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--use-current-runtime" }, "-property:UseCurrentRuntimeIdentifier=True")]
        [InlineData(new string[] { "-o", "<publishdir>" }, "-property:PublishDir=<cwd><publishdir>")]
        [InlineData(new string[] { "--output", "<publishdir>" }, "-property:PublishDir=<cwd><publishdir>")]
        [InlineData(new string[] { "-c", "<config>" }, "-property:Configuration=<config>")]
        [InlineData(new string[] { "--configuration", "<config>" }, "-property:Configuration=<config>")]
        [InlineData(new string[] { "--version-suffix", "<versionsuffix>" }, "-property:VersionSuffix=<versionsuffix>")]
        [InlineData(new string[] { "--manifest", "<manifestfiles>" }, "-property:TargetManifestFiles=<cwd><manifestfiles>")]
        [InlineData(new string[] { "-v", "minimal" }, "-verbosity:minimal")]
        [InlineData(new string[] { "--verbosity", "minimal" }, "-verbosity:minimal")]
        [InlineData(new string[] { "<project>" }, "<project>")]
        [InlineData(new string[] { "<project>", "<extra-args>" }, "<project> <extra-args>")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory);

                var msbuildPath = "<msbuildpath>";
                var command = PublishCommand.FromArgs(args, msbuildPath);

                command.SeparateRestoreCommand
                    .Should()
                    .BeNull();

                command.GetArgumentsToMSBuild()
                    .Should()
                    .Be($"{ExpectedPrefix} -restore -target:Publish{expectedAdditionalArgs}");
            });
        }

        [Theory]
        [InlineData(new string[] { "-f", "<tfm>" }, "-property:TargetFramework=<tfm>")]
        [InlineData(new string[] { "--framework", "<tfm>" }, "-property:TargetFramework=<tfm>")]
        public void MsbuildInvocationIsCorrectForSeparateRestore(string[] args, string expectedAdditionalArgs)
        {
            expectedAdditionalArgs = (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}");

            var msbuildPath = "<msbuildpath>";
            var command = PublishCommand.FromArgs(args, msbuildPath);

            command.SeparateRestoreCommand
                   .GetArgumentsToMSBuild()
                   .Should()
                   .Be($"{ExpectedPrefix} -target:Restore");

            command.GetArgumentsToMSBuild()
                   .Should()
                   .Be($"{ExpectedPrefix} -nologo -target:Publish{expectedAdditionalArgs}");
        }

        [Fact]
        public void MsbuildInvocationIsCorrectForNoBuild()
        {
            var msbuildPath = "<msbuildpath>";
            var command = PublishCommand.FromArgs(new[] { "--no-build" }, msbuildPath);

            command.SeparateRestoreCommand
                   .Should()
                   .BeNull();

            // NOTE --no-build implies no-restore hence no -restore argument to msbuild below.
            command.GetArgumentsToMSBuild()
                   .Should()
                   .Be($"{ExpectedPrefix} -target:Publish -property:NoBuild=true");
        }

        [Fact]
        public void CommandAcceptsMultipleCustomProperties()
        {
            var msbuildPath = "<msbuildpath>";
            var command = PublishCommand.FromArgs(new[] { "/p:Prop1=prop1", "/p:Prop2=prop2" }, msbuildPath);

            command.GetArgumentsToMSBuild()
               .Should()
               .Be($"{ExpectedPrefix} -restore -target:Publish -property:Prop1=prop1 -property:Prop2=prop2");
        }
    }
}
