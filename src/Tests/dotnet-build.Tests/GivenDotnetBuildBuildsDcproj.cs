// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
