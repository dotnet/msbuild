// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using TestCommand = Microsoft.DotNet.Tools.Test.TestCommand;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetTestInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        private const string ExpectedPrefix = "-maxcpucount -verbosity:m -restore -target:VSTest -nodereuse:false -nologo";
        private static readonly string WorkingDirectory = TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetTestInvocation));

        [Theory]
        [InlineData(new string[] { "--disable-build-servers" }, "-p:UseRazorBuildServer=false -p:UseSharedCompilation=false /nodeReuse:false -property:VSTestArtifactsProcessingMode=collect -property:VSTestSessionCorrelationId=<testSessionCorrelationId>")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                Telemetry.Telemetry.DisableForTests();

                expectedAdditionalArgs =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory);

                var testSessionCorrelationId = "<testSessionCorrelationId>";
                var msbuildPath = "<msbuildpath>";

                TestCommand.FromArgs(args, testSessionCorrelationId, msbuildPath)
                    .GetArgumentsToMSBuild()
                    .Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
            });
        }
    }
}
