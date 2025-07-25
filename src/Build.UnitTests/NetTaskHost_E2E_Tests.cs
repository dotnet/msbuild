// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Internal;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests
{
    public class NetTaskHost_E2E_Tests
    {
        private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(NetTaskHost_E2E_Tests).Assembly.Location) ?? AppContext.BaseDirectory);

        private static string TestAssetsRootPath { get; } = Path.Combine(AssemblyLocation, "TestAssets");

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHostTest()
        {
            using TestEnvironment env = TestEnvironment.Create();
            var bootstrapCoreFolder = Path.Combine(RunnerUtilities.BootstrapRootPath, "core");
            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild(
                $"{testProjectPath} -restore -v:n",
                out bool successTestTask);
            successTestTask.ShouldBeTrue();

            testTaskOutput.ShouldContain($"The task is executed in process: dotnet");
            testTaskOutput.ShouldContain($"Process path: {Path.Combine(bootstrapCoreFolder, Constants.DotnetProcessName)}", customMessage: testTaskOutput);
        }
    }
}
