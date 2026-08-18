// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests that IMultiThreadableTask implementations always have a usable TaskEnvironment,
    /// even when explicitly instantiated or run in the out-of-proc task host.
    /// </summary>
    public class TaskHost_MultiThreadableTask_Tests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private readonly string _testProjectsDir;

        public TaskHost_MultiThreadableTask_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
            _testProjectsDir = _env.CreateFolder().Path;
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        /// <summary>
        /// A subclass of a built-in IMultiThreadableTask (MakeDir) should inherit the
        /// non-null default TaskEnvironment. This covers the scenario where someone
        /// derives from a built-in task and explicitly instantiates it.
        /// </summary>
        [Fact]
        public void ExplicitlyInstantiated_InheritedTask_HasNonNullTaskEnvironment()
        {
            var task = new DerivedMakeDirTask();
            IMultiThreadableTask multiThreadable = task;

            multiThreadable.TaskEnvironment.ShouldNotBeNull();
        }

        /// <summary>
        /// When a task that inherits from a built-in IMultiThreadableTask (MakeDir) runs in
        /// the out-of-proc task host (via TaskHostFactory), the inherited default TaskEnvironment
        /// should be usable. The task accesses TaskEnvironment.ProjectDirectory in Execute() —
        /// without the default it would NRE.
        /// </summary>
        [Fact]
        public void InheritedTask_InTaskHost_HasUsableTaskEnvironment()
        {
            string projectContent = $"""
                <Project>
                    <UsingTask TaskName="DerivedMakeDirTask"
                               AssemblyFile="{Assembly.GetExecutingAssembly().Location}"
                               TaskFactory="TaskHostFactory" />

                    <Target Name="TestTarget">
                        <DerivedMakeDirTask Directories="does-not-matter" />
                    </Target>
                </Project>
                """;

            string projectFile = Path.Combine(_testProjectsDir, "TaskEnvTest.proj");
            File.WriteAllText(projectFile, projectContent);

            var logger = new MockLogger(_output);
            var buildParameters = new BuildParameters
            {
                Loggers = [logger],
                DisableInProcNode = false,
                EnableNodeReuse = false,
            };

            var buildRequestData = new BuildRequestData(
                projectFile,
                new Dictionary<string, string?>(),
                null,
                ["TestTarget"],
                null);

            var result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);

            _output.WriteLine(logger.FullLog);

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify the task actually ran in the task host
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "DerivedMakeDirTask");

            // Verify the task was able to read ProjectDirectory without NRE
            logger.FullLog.ShouldContain("TaskEnvironment.ProjectDirectory=");
        }
    }

    /// <summary>
    /// Task that inherits from the built-in MakeDir (which implements IMultiThreadableTask).
    /// Does NOT declare its own TaskEnvironment — it relies on the inherited default from MakeDir.
    /// Overrides Execute() to log TaskEnvironment.ProjectDirectory, proving the default works.
    /// </summary>
    public class DerivedMakeDirTask : MakeDir
    {
        public override bool Execute()
        {
            string projectDir = TaskEnvironment.ProjectDirectory;
            Log.LogMessage(MessageImportance.High, $"TaskEnvironment.ProjectDirectory={projectDir}");
            return true;
        }
    }
}
