// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Store;
using Microsoft.NET.TestFramework;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetStoreInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        const string ExpectedPrefix = "-maxcpucount -verbosity:m -target:ComposeStore <project>";
        static readonly string[] ArgsPrefix = { "--manifest", "<project>" };
        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetStoreInvocation));

        [Theory]
        [InlineData("-m")]
        [InlineData("--manifest")]
        public void ItAddsProjectToMsbuildInvocation(string optionName)
        {
            var msbuildPath = "<msbuildpath>";
            string[] args = new string[] { optionName, "<project>" };
            StoreCommand.FromArgs(args, msbuildPath)
                .GetArgumentsToMSBuild().Should().Contain($"{ExpectedPrefix}");
        }

        [Theory]
        [InlineData(new string[] { "-f", "<tfm>" }, @"-property:TargetFramework=<tfm>")]
        [InlineData(new string[] { "--framework", "<tfm>" }, @"-property:TargetFramework=<tfm>")]
        [InlineData(new string[] { "-r", "<rid>" }, @"-property:RuntimeIdentifier=<rid> -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--runtime", "<rid>" }, @"-property:RuntimeIdentifier=<rid> -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--use-current-runtime" }, "-property:UseCurrentRuntimeIdentifier=True")]
        [InlineData(new string[] { "--manifest", "one.xml", "--manifest", "two.xml", "--manifest", "three.xml" }, @"-property:AdditionalProjects=<cwd>one.xml%3B<cwd>two.xml%3B<cwd>three.xml")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                args = ArgsPrefix.Concat(args).ToArray();
                expectedAdditionalArgs =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory);

                var msbuildPath = "<msbuildpath>";
                StoreCommand.FromArgs(args, msbuildPath)
                    .GetArgumentsToMSBuild().Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
            });
        }

        [Theory]
        [InlineData("-o")]
        [InlineData("--output")]
        public void ItAddsOutputPathToMsBuildInvocation(string optionName)
        {
            string path = Path.Combine("some", "path");
            var args = ArgsPrefix.Concat(new string[] { optionName, path }).ToArray();

            var msbuildPath = "<msbuildpath>";
            StoreCommand.FromArgs(args, msbuildPath)
                .GetArgumentsToMSBuild().Should().Be($"{ExpectedPrefix} -property:ComposeDir={Path.GetFullPath(path)}");
        }
    }
}
