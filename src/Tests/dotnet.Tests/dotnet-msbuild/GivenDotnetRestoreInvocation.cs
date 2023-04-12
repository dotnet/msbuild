// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Restore;
using FluentAssertions;
using Xunit;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetRestoreInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        private const string ExpectedPrefix =
            "-maxcpucount -verbosity:m -nologo -target:Restore";
        private static readonly string WorkingDirectory = 
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetRestoreInvocation));

        [Theory]
        [InlineData(new string[] { }, "")]
        [InlineData(new string[] { "-s", "<source>" }, "-property:RestoreSources=<source>")]
        [InlineData(new string[] { "--source", "<source>" }, "-property:RestoreSources=<source>")]
        [InlineData(new string[] { "-s", "<source0>", "-s", "<source1>" }, "-property:RestoreSources=<source0>%3B<source1>")]
        [InlineData(new string[] { "-r", "<runtime>" }, "-property:RuntimeIdentifiers=<runtime>")]
        [InlineData(new string[] { "--runtime", "<runtime>" }, "-property:RuntimeIdentifiers=<runtime>")]
        [InlineData(new string[] { "-r", "<runtime0>", "-r", "<runtime1>" }, "-property:RuntimeIdentifiers=<runtime0>%3B<runtime1>")]
        [InlineData(new string[] { "--packages", "<packages>" }, "-property:RestorePackagesPath=<cwd><packages>")]
        [InlineData(new string[] { "--disable-parallel" }, "-property:RestoreDisableParallel=true")]
        [InlineData(new string[] { "--configfile", "<config>" }, "-property:RestoreConfigFile=<cwd><config>")]
        [InlineData(new string[] { "--no-cache" }, "-property:RestoreNoCache=true")]
        [InlineData(new string[] { "--ignore-failed-sources" }, "-property:RestoreIgnoreFailedSources=true")]
        [InlineData(new string[] { "--no-dependencies" }, "-property:RestoreRecursive=false")]
        [InlineData(new string[] { "-v", "minimal" }, @"-verbosity:minimal")]
        [InlineData(new string[] { "--verbosity", "minimal" }, @"-verbosity:minimal")]
        [InlineData(new string[] { "--use-lock-file" }, "-property:RestorePackagesWithLockFile=true")]
        [InlineData(new string[] { "--locked-mode" }, "-property:RestoreLockedMode=true")]
        [InlineData(new string[] { "--force-evaluate" }, "-property:RestoreForceEvaluate=true")]
        [InlineData(new string[] { "--lock-file-path", "<lockFilePath>" }, "-property:NuGetLockFilePath=<lockFilePath>")]
        [InlineData(new string[] { "--disable-build-servers" }, "-p:UseRazorBuildServer=false -p:UseSharedCompilation=false /nodeReuse:false")]
        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                Telemetry.Telemetry.DisableForTests();

                expectedAdditionalArgs =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory);

                var msbuildPath = "<msbuildpath>";
                RestoreCommand.FromArgs(args, msbuildPath)
                    .GetArgumentsToMSBuild()
                    .Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
            });
        }
    }
}
