// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class VSTestForwardDotnetRootEnvironmentVariables : SdkTest
    {
        private const string TestAppName = "VSTestForwardDotnetRootEnvironmentVariables";

        public VSTestForwardDotnetRootEnvironmentVariables(ITestOutputHelper log) : base(log)
        {
        }

        private readonly string[] ConsoleLoggerOutputDetailed = new[] { "--logger", "console;verbosity=detailed" };

        [Fact]
        public void ShouldForwardDotnetRootEnvironmentVariablesIfNotProvided()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(TestAppName)
                .WithSource()
                .WithVersionVariables();

            var command = new DotnetTestCommand(Log).WithWorkingDirectory(testAsset.Path);
            command.EnvironmentToRemove.Add("DOTNET_ROOT");
            command.EnvironmentToRemove.Add("DOTNET_ROOT(x86)");
            var result = command.Execute(ConsoleLoggerOutputDetailed);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 1")
                    .And.Contain("Passed: 1")
                    .And.Contain("Passed TestForwardDotnetRootEnvironmentVariables")
                    .And.Contain("VSTEST_WINAPPHOST_");
            }

            result.ExitCode.Should().Be(0);
        }
    }
}
