// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestContainsMSBuildParameters : SdkTest
    {
        private const string TestAppName = "VSTestMSBuildParameters";
        private const string MSBuildParameter = "/p:Version=1.2.3";

        public GivenDotnetTestContainsMSBuildParameters(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData($"{TestAppName}.csproj")]
        [InlineData(null)]
        [Theory]
        public void ItPassesEnvironmentVariablesFromCommandLineParametersWhenRunningViaCsproj(string projectName)
        {
            var testAsset = _testAssetsManager.CopyTestAsset(TestAppName)
                .WithSource()
                .WithVersionVariables();

            var testRoot = testAsset.Path;

            CommandResult result = (projectName is null ? new DotnetTestCommand(Log) : new DotnetTestCommand(Log, projectName))
                                    .WithWorkingDirectory(testRoot)
                                    .Execute("--logger", "console;verbosity=detailed", MSBuildParameter);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 1")
                    .And.Contain("Passed: 1")
                    .And.Contain("Passed TestMSBuildParameters");
            }

            result.ExitCode.Should().Be(0);
        }
    }
}
