// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Xunit;

#nullable enable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Integration coverage for the host task-class registration API
    /// (<see cref="Task.RegisterTask{T}(string)"/> and the
    /// <see cref="System.Func{ITask}"/> overload) exercised through a real in-process build on the default
    /// (JIT) engine, where reflective task execution is enabled. The Native AOT (reflective-off) counterparts
    /// live in the aot-validation harness, which is not part of the CI test run; these run in CI.
    /// </summary>
    /// <remarks>
    /// The task registry is process-global and has no unregister API, so each test uses a unique, distinctive
    /// task name that no real task or other test uses; the leftover registrations are harmless (a registered
    /// name only affects a build that invokes a task of exactly that name).
    /// </remarks>
    public sealed class RegisteredTaskExecution_Tests
    {
        private readonly ITestOutputHelper _output;

        public RegisteredTaskExecution_Tests(ITestOutputHelper output) => _output = output;

        /// <summary>
        /// A task registered through the generic overload resolves from the registry (consulted before the
        /// project's UsingTask table) and executes under the default JIT engine, and its <c>[Output]</c> binds
        /// back to a property - exercising reflective parameter binding over the registered, trim-rooted type.
        /// </summary>
        [Fact]
        public void RegisterTaskGeneric_ResolvesAndExecutesAndBindsOutput()
        {
            Task.RegisterTask<RegisteredEchoTestTask>("RegTaskTest_GenericEcho");

            string project = """
                <Project DefaultTargets="Build">
                  <Target Name="Build">
                    <RegTaskTest_GenericEcho Input="hello">
                      <Output TaskParameter="Result" PropertyName="EchoResult" />
                    </RegTaskTest_GenericEcho>
                    <Message Text="Bound: $(EchoResult)" Importance="high" />
                  </Target>
                </Project>
                """;

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(project, _output);

            logger.AssertLogContains("RegisteredEchoTestTask ran: hello!");
            logger.AssertLogContains("Bound: hello!");
        }

        /// <summary>
        /// A task registered through the <see cref="System.Func{ITask}"/> overload (whose task type is not
        /// statically known at registration) resolves and executes, exercising the lazily-built
        /// <c>LoadedType</c> and the parameter binding that depends on it.
        /// </summary>
        [Fact]
        public void RegisterTaskFactory_ResolvesAndExecutesAndBindsOutput()
        {
            Task.RegisterTask("RegTaskTest_FactoryEcho", static () => new RegisteredEchoTestTask());

            string project = """
                <Project DefaultTargets="Build">
                  <Target Name="Build">
                    <RegTaskTest_FactoryEcho Input="world">
                      <Output TaskParameter="Result" PropertyName="EchoResult" />
                    </RegTaskTest_FactoryEcho>
                    <Message Text="Bound: $(EchoResult)" Importance="high" />
                  </Target>
                </Project>
                """;

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(project, _output);

            logger.AssertLogContains("RegisteredEchoTestTask ran: world!");
            logger.AssertLogContains("Bound: world!");
        }

        /// <summary>
        /// Registering a name a second time replaces the previous registration; the most recently registered
        /// task is the one the engine constructs.
        /// </summary>
        [Fact]
        public void RegisterTask_RegisteringSameNameAgain_ReplacesPreviousRegistration()
        {
            Task.RegisterTask<RegisteredEchoTestTask>("RegTaskTest_Replace");

            // Re-register the same name with a different task type; the latest registration must win.
            Task.RegisterTask<RegisteredMarkerTestTask>("RegTaskTest_Replace");

            string project = """
                <Project DefaultTargets="Build">
                  <Target Name="Build">
                    <RegTaskTest_Replace />
                  </Target>
                </Project>
                """;

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(project, _output);

            logger.AssertLogContains("RegisteredMarkerTestTask ran");
            logger.AssertLogDoesntContain("RegisteredEchoTestTask ran");
        }

        /// <summary>
        /// A registered task followed by an intrinsic task on the same (reused) <c>TaskExecutionHost</c> both
        /// execute correctly: the registered-task factory does not leak across tasks. If it did, the intrinsic
        /// <c>CallTarget</c> would be mis-constructed as the registered task and its target would not run.
        /// </summary>
        [Fact]
        public void RegisteredTaskFollowedByIntrinsicTask_BothExecute_NoStateLeak()
        {
            Task.RegisterTask<RegisteredEchoTestTask>("RegTaskTest_ResetEcho");

            string project = """
                <Project DefaultTargets="Build">
                  <Target Name="Build">
                    <RegTaskTest_ResetEcho Input="x" />
                    <CallTarget Targets="Side" />
                  </Target>
                  <Target Name="Side">
                    <Message Text="SideTargetRan" Importance="high" />
                  </Target>
                </Project>
                """;

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(project, _output);

            logger.AssertLogContains("RegisteredEchoTestTask ran: x!");
            logger.AssertLogContains("SideTargetRan");
        }

        /// <summary>
        /// A task registered through the generic overload whose type declares both a parameterless and a
        /// <see cref="TaskEnvironment"/> constructor is constructed through the <see cref="TaskEnvironment"/>
        /// constructor (injecting the current environment), mirroring the reflective task-load path.
        /// </summary>
        [Fact]
        public void RegisterTaskGeneric_TaskWithTaskEnvironmentConstructor_InjectsEnvironment()
        {
            Task.RegisterTask<RegisteredEnvCtorTestTask>("RegTaskTest_GenericEnvCtor");

            string project = """
                <Project DefaultTargets="Build">
                  <Target Name="Build">
                    <RegTaskTest_GenericEnvCtor />
                  </Target>
                </Project>
                """;

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(project, _output);

            logger.AssertLogContains("RegisteredEnvCtorTestTask used env ctor: True");
            logger.AssertLogContains("RegisteredEnvCtorTestTask ctor env was null: False");
        }

        /// <summary>
        /// A task registered through the <see cref="System.Func{TaskEnvironment, ITask}"/> overload receives
        /// the current <see cref="TaskEnvironment"/> at construction, so a task whose only constructor takes a
        /// <see cref="TaskEnvironment"/> (and thus cannot use the generic <c>new()</c> overload) can still be
        /// registered and run reflection-free.
        /// </summary>
        [Fact]
        public void RegisterTaskEnvironmentFactory_InjectsEnvironment()
        {
            Task.RegisterTask("RegTaskTest_EnvFactory", static env => new RegisteredEnvOnlyCtorTestTask(env));

            string project = """
                <Project DefaultTargets="Build">
                  <Target Name="Build">
                    <RegTaskTest_EnvFactory />
                  </Target>
                </Project>
                """;

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(project, _output);

            logger.AssertLogContains("RegisteredEnvOnlyCtorTestTask ctor env was null: False");
        }
    }

    /// <summary>
    /// A simple host-registered task: echoes its <see cref="Input"/> with a "!" suffix into an
    /// <c>[Output]</c> property and logs a marker so a build can assert it executed and that its output bound.
    /// </summary>
    public sealed class RegisteredEchoTestTask : Task
    {
        public string? Input { get; set; }

        [Output]
        public string? Result { get; set; }

        public override bool Execute()
        {
            Result = Input + "!";
            Log.LogMessage(MessageImportance.High, "RegisteredEchoTestTask ran: " + Result);
            return true;
        }
    }

    /// <summary>
    /// A second host-registered task that logs a distinct marker, used to prove that re-registering a name
    /// replaces the previous registration.
    /// </summary>
    public sealed class RegisteredMarkerTestTask : Task
    {
        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "RegisteredMarkerTestTask ran");
            return true;
        }
    }

    /// <summary>
    /// A host-registered task that declares both a parameterless and a <see cref="TaskEnvironment"/>
    /// constructor, used to prove the registered-task path prefers the environment-injecting constructor.
    /// </summary>
    public sealed class RegisteredEnvCtorTestTask : Task, IMultiThreadableTask
    {
        private readonly bool _usedEnvironmentConstructor;
        private readonly bool _ctorEnvironmentWasNull;

        public RegisteredEnvCtorTestTask()
        {
            _usedEnvironmentConstructor = false;
            _ctorEnvironmentWasNull = true;
        }

        public RegisteredEnvCtorTestTask(TaskEnvironment taskEnvironment)
        {
            _usedEnvironmentConstructor = true;
            _ctorEnvironmentWasNull = taskEnvironment is null;
            TaskEnvironment = taskEnvironment!;
        }

        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "RegisteredEnvCtorTestTask used env ctor: " + _usedEnvironmentConstructor);
            Log.LogMessage(MessageImportance.High, "RegisteredEnvCtorTestTask ctor env was null: " + _ctorEnvironmentWasNull);
            return true;
        }
    }

    /// <summary>
    /// A host-registered task whose only constructor takes a <see cref="TaskEnvironment"/>. It cannot be
    /// registered through the generic <c>new()</c> overload, so it is registered via the
    /// <see cref="System.Func{TaskEnvironment, ITask}"/> factory overload that supplies the environment.
    /// </summary>
    public sealed class RegisteredEnvOnlyCtorTestTask : Task, IMultiThreadableTask
    {
        private readonly bool _ctorEnvironmentWasNull;

        public RegisteredEnvOnlyCtorTestTask(TaskEnvironment taskEnvironment)
        {
            _ctorEnvironmentWasNull = taskEnvironment is null;
            TaskEnvironment = taskEnvironment!;
        }

        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.High, "RegisteredEnvOnlyCtorTestTask ctor env was null: " + _ctorEnvironmentWasNull);
            return true;
        }
    }
}
