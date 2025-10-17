// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// End-to-end MSBuild CLI tests for .NET Runtime task error handling.
    /// These tests verify that MSBuild.exe CLI gives a clear error when attempting to use Runtime="NET" tasks.
    /// </summary>
    public class DotNetRuntimeTask_CLI_Tests
    {
        private readonly ITestOutputHelper _output;

        public DotNetRuntimeTask_CLI_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Test that MSBuild.exe CLI gives clear error (MSB4233) when building a project
        /// that uses a task with Runtime="NET".
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void MSBuildCLI_WithDotNetRuntimeTask_ShowsClearError()
        {
            // This test verifies that running MSBuild.exe from command line with a project that uses Runtime="NET"
            // produces a clear error message rather than a confusing "MSBuild.dll not found" error.
#if NETFRAMEWORK
            using (var env = TestEnvironment.Create(_output))
            {
                // Use the same ProcessIdTask from Microsoft.Build.Engine.UnitTests that is built during the repo build
                // Get the path to the Microsoft.Build.Engine.UnitTests assembly
                string taskAssemblyPath = typeof(Microsoft.Build.UnitTests.ProcessIdTask).Assembly.Location;
                
                string projectContent = $@"
<Project>
    <UsingTask TaskName=""ProcessIdTask"" AssemblyFile=""{taskAssemblyPath}"" Runtime=""NET"" />
    <Target Name='TestTask'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
    </Target>
</Project>";

                var projectFile = env.CreateFile("test.proj", projectContent).Path;

                // Execute MSBuild on the project - should fail with MSB4233
                string output = RunnerUtilities.ExecMSBuild($"\"{projectFile}\" /t:TestTask", out bool success, _output);

                // Build should fail
                success.ShouldBeFalse();

                // Output should contain MSB4233 error
                output.ShouldContain("MSB4233");

                // Output should contain clear error message about .NET runtime not being supported
                output.ShouldContain("ProcessIdTask");
                output.ShouldContain(".NET runtime");
                output.ShouldContain("MSBuild 18.0");
            }
#endif
        }
    }
}
