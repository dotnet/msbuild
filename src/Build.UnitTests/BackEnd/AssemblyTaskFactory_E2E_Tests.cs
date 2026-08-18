// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    public class AssemblyTaskFactory_E2E_Tests
    {
        private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(NetTaskHost_E2E_Tests).Assembly.Location) ?? AppContext.BaseDirectory);

        private static string TestAssetsRootPath { get; } = Path.Combine(AssemblyLocation, "TestAssets");

        private readonly ITestOutputHelper _output;

        public AssemblyTaskFactory_E2E_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsFullFrameworkOnlyFact]
        public void FrameworkTaskArchitectureMismatchHandledSuccessfullyTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleFrameworkTask", "TestTask", "TestTask.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();

            testTaskOutput.ShouldContain("The task is executed in process: MSBuild");
            testTaskOutput.ShouldContain("PlatformTarget: x86");
            testTaskOutput.ShouldContain("PlatformTarget: x64");
        }

        [WindowsFullFrameworkOnlyFact]
        public void FrameworkTaskArchitectureMismatchReportsWarningTest()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string testProjectPath = Path.Combine(TestAssetsRootPath, "ExampleFrameworkTask", "TestTask", "TestTask.csproj");

            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{testProjectPath} -restore -v:n", out bool successTestTask);

            if (!successTestTask)
            {
                _output.WriteLine(testTaskOutput);
            }

            successTestTask.ShouldBeTrue();
            testTaskOutput.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("AssemblyLoad_Warning", "ExampleTaskX64"));
        }
    }
}
