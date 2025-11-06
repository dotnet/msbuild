// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// End-to-end tests for .NET Runtime task error handling.
    /// These tests verify that MSBuild 17.14 gives a clear error when attempting to use Runtime="NET" tasks.
    /// </summary>
    public class DotNetRuntimeTask_EndToEnd_Tests
    {
        private readonly ITestOutputHelper _output;

        public DotNetRuntimeTask_EndToEnd_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Test that BuildManager API gives clear error (MSB4233) when attempting to build a project
        /// that uses a task with Runtime="NET".
        /// </summary>
        [WindowsFullFrameworkOnlyFact]
        public void BuildManager_WithDotNetRuntimeTask_ShowsClearError()
        {
            // This test uses a real task but specifies Runtime="NET" which is not supported on .NET Framework builds.
            // We expect the build to fail with MSB4233 error that clearly explains the issue.
#if NETFRAMEWORK
            string projectContent = @"
<Project>
    <UsingTask TaskName=""ProcessIdTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests"" Runtime=""NET"" />
    <Target Name='TestTask'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
    </Target>
</Project>";

            using (var env = TestEnvironment.Create(_output))
            {
                var testProject = env.CreateTestProjectWithFiles(projectContent);
                var logger = new MockLogger(_output);

                var parameters = new BuildParameters
                {
                    Loggers = new[] { logger }
                };

                var result = Helpers.BuildProjectFileUsingBuildManager(testProject.ProjectFile, logger, parameters, targetsToBuild: new[] { "TestTask" });

                // Build should fail
                result.OverallResult.ShouldBe(BuildResultCode.Failure);

                // Should log MSB4233 error
                logger.ErrorCount.ShouldBeGreaterThan(0);
                logger.Errors[0].Code.ShouldBe("MSB4233");

                // Error message should contain the task name and indicate .NET runtime is not supported
                string errorMessage = logger.Errors[0].Message;
                errorMessage.ShouldContain("ProcessIdTask");
                errorMessage.ShouldContain(".NET runtime");
                errorMessage.ShouldContain("MSBuild 18.0");
            }
#endif
        }
    }
}
