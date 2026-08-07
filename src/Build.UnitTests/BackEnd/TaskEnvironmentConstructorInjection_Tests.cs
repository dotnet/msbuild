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

#nullable enable

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

            // In multi-threaded mode the attributed task runs in-process and the engine injects a real
            // per-request environment (not the shared TaskEnvironment.Fallback singleton). In single-threaded
            // mode the request environment is Fallback. This proves a concrete, usable environment reached the
            // constructor and distinguishes the real environment from the Fallback singleton.
            if (multiThreaded)
            {
                logger.FullLog.ShouldContain("CtorIsFallback=False");
            }
            else
            {
                logger.FullLog.ShouldContain("CtorIsFallback=True");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TaskWithBothConstructors_PrefersTaskEnvironmentConstructor(bool multiThreaded)
        {
            MockLogger logger = BuildTaskProject("TaskEnvBothCtorsTask", multiThreaded);

            logger.FullLog.ShouldContain("UsedTaskEnvironmentConstructor=True");
        }

        /// <summary>
        /// When a task that only declares a <see cref="TaskEnvironment"/> constructor runs in the out-of-proc
        /// task host (via TaskHostFactory), the task host must still instantiate it through that constructor.
        /// The task host has no per-request environment, so it injects <see cref="TaskEnvironment.Fallback"/>.
        /// </summary>
        [Fact]
        public void TaskWithOnlyTaskEnvironmentConstructor_InTaskHost_InjectsFallbackEnvironment()
        {
            MockLogger logger = BuildTaskProject("TaskEnvCtorOnlyTask", multiThreaded: false, useTaskHost: true);

            // The task actually ran in the external task host process.
            TaskRouterTestHelper.AssertTaskUsedTaskHost(logger, "TaskEnvCtorOnlyTask");

            // The constructor was invoked (there is no parameterless constructor to fall back to) with the
            // task host's Fallback environment.
            logger.FullLog.ShouldContain("CtorEnvironmentWasNull=False");
            logger.FullLog.ShouldContain("CtorIsFallback=True");
        }

        private MockLogger BuildTaskProject(string taskName, bool multiThreaded, bool useTaskHost = false)
        {
            string taskFactoryAttribute = useTaskHost ? @" TaskFactory=""TaskHostFactory""" : string.Empty;
            string projectContent = $@"
<Project>
    <UsingTask TaskName=""{taskName}"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}""{taskFactoryAttribute} />

    <Target Name=""TestTarget"">
        <{taskName} />
    </Target>
</Project>";

            string projectFile = Path.Combine(_testProjectsDir, $"{taskName}_{multiThreaded}_{useTaskHost}.proj");
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
                new Dictionary<string, string?>(),
                null,
                new[] { "TestTarget" },
                null);

            BuildResult result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);

            _output.WriteLine(logger.FullLog);

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            return logger;
        }
    }

    /// <summary>
    /// Task that only declares a constructor accepting a <see cref="TaskEnvironment"/>. It cannot be
    /// instantiated at all unless the engine supports constructor injection.
    /// </summary>
    /// <remarks>
    /// Marked with <c>[MSBuildMultiThreadableTask]</c> so that in multi-threaded mode it runs in-process
    /// (via <c>TaskExecutionHost</c>) and therefore receives the real per-request <see cref="TaskEnvironment"/>
    /// rather than the shared <see cref="TaskEnvironment.Fallback"/> singleton used by the out-of-proc task host.
    /// The attribute resolves to the public test copy defined in <c>TaskRouter_IntegrationTests.cs</c>, which
    /// intentionally shadows the internal Framework version for name-based routing detection.
    /// </remarks>
#pragma warning disable CS0436 // Type conflicts with imported type - intentional for testing
    [MSBuildMultiThreadableTask]
#pragma warning restore CS0436
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
            Log.LogMessage(MessageImportance.High, $"CtorIsFallback={ReferenceEquals(_ctorEnvironment, TaskEnvironment.Fallback)}");
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

        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, $"UsedTaskEnvironmentConstructor={_usedTaskEnvironmentConstructor}");
            return true;
        }
    }
}
