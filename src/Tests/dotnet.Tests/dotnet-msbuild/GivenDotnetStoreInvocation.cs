// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Store;

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
        [InlineData(new string[] { "-r", "linux-amd64" }, @"-property:RuntimeIdentifier=linux-x64 -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--runtime", "<rid>" }, @"-property:RuntimeIdentifier=<rid> -property:_CommandLineDefinedRuntimeIdentifier=true")]
        [InlineData(new string[] { "--use-current-runtime" }, "-property:UseCurrentRuntimeIdentifier=True")]
        [InlineData(new string[] { "--ucr" }, "-property:UseCurrentRuntimeIdentifier=True")]
        [InlineData(new string[] { "--manifest", "one.xml", "--manifest", "two.xml", "--manifest", "three.xml" }, @"-property:AdditionalProjects=<cwd>one.xml%3B<cwd>two.xml%3B<cwd>three.xml")]
        [InlineData(new string[] { "--disable-build-servers" }, "-p:UseRazorBuildServer=false -p:UseSharedCompilation=false /nodeReuse:false")]
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
                .GetArgumentsToMSBuild().Should().Be($"{ExpectedPrefix} -property:ComposeDir={Path.GetFullPath(path)} -property:_CommandLineDefinedOutputPath=true");
        }
    }
}
