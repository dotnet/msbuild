// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Build.Tests
{
    public class GivenDotnetBuildBuildsDcproj : SdkTest
    {
        public GivenDotnetBuildBuildsDcproj(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItPrintsBuildSummary()
        {
            var testAppName = "docker-compose";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .Restore(Log);

            string expectedBuildSummary = @"Build succeeded.
    0 Warning(s)
    0 Error(s)";

            var cmd = new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragment(expectedBuildSummary);
        }
    }
}
