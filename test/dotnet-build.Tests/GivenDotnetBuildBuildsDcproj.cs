// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Build.Tests
{
    public class GivenDotnetBuildBuildsDcproj : TestBase
    {
        [Fact]
        public void ItPrintsBuildSummary()
        {
            var testAppName = "docker-compose";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName)
                .WithSourceFiles()
                .WithRestoreFiles();

            string expectedBuildSummary = @"Build succeeded.
    0 Warning(s)
    0 Error(s)";

            var cmd = new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragment(expectedBuildSummary);
        }
    }
}