// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using PackCommand = Microsoft.DotNet.Tools.Pack.PackCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetPackInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        const string ExpectedPrefix = "-maxcpucount -verbosity:m -restore -target:pack";
        const string ExpectedNoBuildPrefix = "-maxcpucount -verbosity:m -target:pack";
        const string ExpectedProperties = "--property:_IsPacking=true";

        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetPackInvocation));

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-o", "<packageoutputpath>" }, "-property:PackageOutputPath=<cwd><packageoutputpath>")]
        [InlineData(new string[] { "--output", "<packageoutputpath>" }, "-property:PackageOutputPath=<cwd><packageoutputpath>")]
        [InlineData(new string[] { "--artifacts-path", "foo" }, "-property:ArtifactsPath=<cwd>foo")]
        [InlineData(new string[] { "--no-build" }, "-property:NoBuild=true")]
        [InlineData(new string[] { "--include-symbols" }, "-property:IncludeSymbols=true")]
        [InlineData(new string[] { "--include-source" }, "-property:IncludeSource=true")]
        [InlineData(new string[] { "-c", "<config>" }, "-property:Configuration=<config> -property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true")]
        [InlineData(new string[] { "--configuration", "<config>" }, "-property:Configuration=<config> -property:DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE=true")]
        [InlineData(new string[] { "--version-suffix", "<versionsuffix>" }, "-property:VersionSuffix=<versionsuffix>")]
        [InlineData(new string[] { "-s" }, "-property:Serviceable=true")]
        [InlineData(new string[] { "--serviceable" }, "-property:Serviceable=true")]
        [InlineData(new string[] { "-v", "diag" }, "-verbosity:diag")]
        [InlineData(new string[] { "--verbosity", "diag" }, "-verbosity:diag")]
        [InlineData(new string[] { "<project>" }, "<project>")]
        [InlineData(new string[] { "--disable-build-servers" }, "-p:UseRazorBuildServer=false -p:UseSharedCompilation=false /nodeReuse:false")]

        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory);

                var msbuildPath = "<msbuildpath>";
                var command = PackCommand.FromArgs(args, msbuildPath);
                var expectedPrefix = args.FirstOrDefault() == "--no-build" ? ExpectedNoBuildPrefix : ExpectedPrefix;

                command.SeparateRestoreCommand.Should().BeNull();
                command.GetArgumentsToMSBuild().Should().Be($"{expectedPrefix} {ExpectedProperties}{expectedAdditionalArgs}");
            });
        }
    }
}
