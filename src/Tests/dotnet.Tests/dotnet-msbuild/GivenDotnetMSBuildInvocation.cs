// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using FluentAssertions;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]

    public class GivenDotnetMSBuildInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        const string ExpectedPrefix = "-maxcpucount -verbosity:m";
       
        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetPackInvocation));

        [Theory]
        [InlineData(new string[] { "--disable-build-servers" }, "-p:UseRazorBuildServer=false -p:UseSharedCompilation=false /nodeReuse:false")]

        public void MsbuildInvocationIsCorrect(string[] args, string expectedAdditionalArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                expectedAdditionalArgs =
                    (string.IsNullOrEmpty(expectedAdditionalArgs) ? "" : $" {expectedAdditionalArgs}")
                    .Replace("<cwd>", WorkingDirectory);

                var msbuildPath = "<msbuildpath>";
                var command = MSBuildCommand.FromArgs(args, msbuildPath);
 
                command.GetArgumentsToMSBuild().Should().Be($"{ExpectedPrefix}{expectedAdditionalArgs}");
            });
        }
    }
}
