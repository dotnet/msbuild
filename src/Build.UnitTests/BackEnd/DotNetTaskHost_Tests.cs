// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// End-to-end MSBuild CLI tests for .NET Runtime task error handling.
    /// These tests verify that MSBuild.exe CLI gives a clear error when attempting to use Runtime="NET" tasks.
    /// </summary>
    public class DotNetTaskHost_Tests
    {
        private readonly ITestOutputHelper _output;

        public DotNetTaskHost_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Test that MSBuild.exe gives clear error (MSB4233) when building a project
        /// that uses a task with Runtime="NET".
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void CustomTask_WithNetRuntime_ShowsClearError()
        {
#if NETFRAMEWORK
            using (var env = TestEnvironment.Create(_output))
            {
                string taskAssemblyPath = Path.Combine(
                    Path.GetDirectoryName(typeof(DotNetTaskHost_Tests).Assembly.Location) ?? AppContext.BaseDirectory,
                    "Microsoft.Build.Engine.UnitTests.dll");

                string projectContent = $@"
<Project>
    <UsingTask TaskName=""ProcessIdTask"" AssemblyFile=""{taskAssemblyPath}"" Runtime=""NET"" />
    <Target Name='TestTask'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
    </Target>
</Project>";
                var logger = new MockLogger(_output);
                TransientTestFile project = env.CreateFile("test.csproj", projectContent);
                ProjectInstance projectInstance = new(project.Path);
                projectInstance.Build(new[] { logger })
                    .ShouldBeFalse();

                logger.ErrorCount.ShouldBeGreaterThan(0);
                logger.Errors[0].Code.ShouldBe("MSB4233");

                string? errorMessage = logger.Errors[0].Message;
                errorMessage.ShouldNotBeNull();
                errorMessage.ShouldContain("To run .NET tasks, MSBuild 18.0 or Visual Studio 2026 or higher must be used.");
            }
#endif
        }
    }
}
