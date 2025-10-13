// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    public class NetTaskHost_E2E_Tests
    {
        private const string LatestDotNetCoreForMSBuild = "net10.0";

        private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(NetTaskHost_E2E_Tests).Assembly.Location) ?? AppContext.BaseDirectory);

        private static string TestAssetsRootPath { get; } = Path.Combine(AssemblyLocation, "TestAssets");

        private readonly ITestOutputHelper _output;

        public NetTaskHost_E2E_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHostTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output, setupDotnetEnvVars: true);
            var bootstrapCorePath = Path.Combine(RunnerUtilities.BootstrapRootPath, "core", Constants.DotnetProcessName);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestNetTask", "TestNetTask.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain($"The task is executed in process: dotnet");
            testTaskOutput.ShouldContain($"Process path: {bootstrapCorePath}", customMessage: testTaskOutput);

            var customTaskAssemblyLocation = Path.GetFullPath(Path.Combine(AssemblyLocation, "..", LatestDotNetCoreForMSBuild, "ExampleTask.dll"));
            var resource = ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
               "TaskAssemblyLocationMismatch",

               // Microsoft.Build.dll represents TaskHostTask wrapper for the custom task here.
               Path.Combine(RunnerUtilities.BootstrapRootPath, "net472", "MSBuild", "Current", "Bin", "Microsoft.Build.dll"),
               customTaskAssemblyLocation);
            testTaskOutput.ShouldNotContain(resource);
        }

        [WindowsFullFrameworkOnlyFact]
        public void MSBuildTaskInNetHostTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output, setupDotnetEnvVars: true);
            var bootstrapCorePath = Path.Combine(RunnerUtilities.BootstrapRootPath, "core", Constants.DotnetProcessName);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleNetTask", "TestMSBuildTaskInNet", "TestMSBuildTaskInNet.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore  -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain($"Hello TEST");
        }
    }
}
