// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Verifies that when a task type declares a constructor accepting a <see cref="TaskEnvironment"/>,
    /// the engine instantiates the task through that constructor (injecting the current environment) rather
    /// than requiring a parameterless constructor.
    /// </summary>
    public class TaskEnvironmentConstructorInjection_Tests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private readonly string _testProjectsDir;

        public TaskEnvironmentConstructorInjection_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
            _testProjectsDir = _env.CreateFolder().Path;
        }

        public void Dispose() => _env.Dispose();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TaskWithOnlyTaskEnvironmentConstructor_IsInstantiatedAndInjected(bool multiThreaded)
        {
            MockLogger logger = BuildTaskProject("TaskEnvCtorOnlyTask", multiThreaded);

            // The constructor was invoked with a non-null environment (there is no parameterless constructor to fall back to).
            logger.FullLog.ShouldContain("CtorEnvironmentWasNull=False");

            // The engine still sets the TaskEnvironment property, and it is the same instance passed to the constructor.
            logger.FullLog.ShouldContain("CtorMatchesProperty=True");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TaskWithBothConstructors_PrefersTaskEnvironmentConstructor(bool multiThreaded)
        {
            MockLogger logger = BuildTaskProject("TaskEnvBothCtorsTask", multiThreaded);

            logger.FullLog.ShouldContain("UsedTaskEnvironmentConstructor=True");
        }

        private MockLogger BuildTaskProject(string taskName, bool multiThreaded)
        {
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""{taskName}"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />

    <Target Name=""TestTarget"">
        <{taskName} />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, $"{taskName}_{multiThreaded}.proj");
            File.WriteAllText(projectFile, projectContent);

            MockLogger logger = new(_output);
            BuildParameters buildParameters = new()
            {
                MultiThreaded = multiThreaded,
                Loggers = new[] { logger },
                DisableInProcNode = false,
                EnableNodeReuse = false,
            };

            BuildRequestData buildRequestData = new(
                projectFile,
                new Dictionary<string, string>(),
                null,
                new[] { "TestTarget" },
                null);

            BuildResult result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);
            result.OverallResult.ShouldBe(BuildResultCode.Success);

            return logger;
        }
    }

    /// <summary>
    /// Task that only declares a constructor accepting a <see cref="TaskEnvironment"/>. It cannot be
    /// instantiated at all unless the engine supports constructor injection.
    /// </summary>
    public class TaskEnvCtorOnlyTask : Task, IMultiThreadableTask
    {
        private readonly TaskEnvironment _ctorEnvironment;

        public TaskEnvCtorOnlyTask(TaskEnvironment taskEnvironment)
        {
            _ctorEnvironment = taskEnvironment;
            TaskEnvironment = taskEnvironment;
        }

        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"CtorEnvironmentWasNull={_ctorEnvironment is null}");
            Log.LogMessage(MessageImportance.High, $"CtorMatchesProperty={ReferenceEquals(_ctorEnvironment, TaskEnvironment)}");
            return true;
        }
    }

    /// <summary>
    /// Task that declares both a parameterless constructor and a <see cref="TaskEnvironment"/> constructor.
    /// The engine must prefer the <see cref="TaskEnvironment"/> constructor.
    /// </summary>
    public class TaskEnvBothCtorsTask : Task, IMultiThreadableTask
    {
        private readonly bool _usedTaskEnvironmentConstructor;

        public TaskEnvBothCtorsTask()
        {
            _usedTaskEnvironmentConstructor = false;
        }

        public TaskEnvBothCtorsTask(TaskEnvironment taskEnvironment)
        {
            _usedTaskEnvironmentConstructor = true;
            TaskEnvironment = taskEnvironment;
        }

        public TaskEnvironment TaskEnvironment { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"UsedTaskEnvironmentConstructor={_usedTaskEnvironmentConstructor}");
            return true;
        }
    }
}
