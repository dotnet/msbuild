// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
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
                // Create a simple .NET task DLL content for testing
                string taskAssemblyContent = @"
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace TestTasks
{
    public class SimpleTask : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, ""SimpleTask executed"");
            return true;
        }
    }
}";

                // Create the task assembly project
                string taskProjectContent = @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>net472</TargetFramework>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include=""Microsoft.Build.Framework"" Version=""17.0.0"" />
        <PackageReference Include=""Microsoft.Build.Utilities.Core"" Version=""17.0.0"" />
    </ItemGroup>
</Project>";

                var taskProjectFile = env.CreateFile("SimpleTask.csproj", taskProjectContent).Path;
                var taskSourceFile = env.CreateFile("SimpleTask.cs", taskAssemblyContent).Path;

                // Build the task assembly (this should succeed)
                string buildTaskOutput = RunnerUtilities.ExecMSBuild($"\"{taskProjectFile}\" /t:Build /v:m", out bool taskBuildSuccess);
                
                // If task build fails, skip the test as it's an environment issue, not what we're testing
                if (!taskBuildSuccess)
                {
                    _output.WriteLine("Warning: Could not build test task assembly. Skipping test.");
                    _output.WriteLine(buildTaskOutput);
                    return;
                }

                string taskAssemblyPath = Path.Combine(Path.GetDirectoryName(taskProjectFile), "bin", "Debug", "net472", "SimpleTask.dll");

                // Now create a project that uses the task with Runtime="NET"
                string projectContent = $@"
<Project>
    <UsingTask TaskName=""TestTasks.SimpleTask"" AssemblyFile=""{taskAssemblyPath}"" Runtime=""NET"" Architecture=""x64"" />
    <Target Name='TestTarget'>
        <SimpleTask />
    </Target>
</Project>";

                var projectFile = env.CreateFile("test.proj", projectContent).Path;

                // Execute MSBuild on the project - should fail with MSB4233
                string output = RunnerUtilities.ExecMSBuild($"\"{projectFile}\" /t:TestTarget", out bool success, _output);

                // Build should fail
                success.ShouldBeFalse();

                // Output should contain MSB4233 error
                output.ShouldContain("MSB4233");

                // Output should contain clear error message about .NET runtime not being supported
                output.ShouldContain("SimpleTask");
                output.ShouldContain(".NET runtime");
                output.ShouldContain("MSBuild 18.0");
            }
#endif
        }

        /// <summary>
        /// Test that MSBuild.exe can successfully build a project with a task without Runtime="NET".
        /// This verifies we didn't break normal CLI execution.
        /// </summary>
        [WindowsOnlyFact]
        public void MSBuildCLI_WithoutDotNetRuntime_Succeeds()
        {
            using (var env = TestEnvironment.Create(_output))
            {
                // Create a simple project that uses a built-in task
                string projectContent = @"
<Project>
    <Target Name='TestTarget'>
        <Message Text='Hello from MSBuild' Importance='High' />
    </Target>
</Project>";

                var projectFile = env.CreateFile("test.proj", projectContent).Path;

                // Execute MSBuild on the project - should succeed
                string output = RunnerUtilities.ExecMSBuild($"\"{projectFile}\" /t:TestTarget", out bool success, _output);

                // Build should succeed
                success.ShouldBeTrue();

                // Output should not contain MSB4233 error
                output.ShouldNotContain("MSB4233");

                // Should see our message
                output.ShouldContain("Hello from MSBuild");
            }
        }
    }
}
