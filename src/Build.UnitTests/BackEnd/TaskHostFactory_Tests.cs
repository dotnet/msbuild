// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.BackEnd;

using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the TaskHostFactory functionality, which manages task host processes
    /// for executing MSBuild tasks in separate processes.
    /// </summary>
    public sealed class TaskHostFactory_Tests
    {
        private static string AssemblyLocation { get; } =
            typeof(TaskHostFactory_Tests).Assembly.Location
            ?? Path.Combine(AppContext.BaseDirectory, "Microsoft.Build.Engine.UnitTests.dll");

        private ITestOutputHelper _output;

        public TaskHostFactory_Tests(ITestOutputHelper testOutputHelper)
        {
            _output = testOutputHelper;
        }

        /// <summary>
        /// Verifies that task host nodes properly terminate after a build completes.
        /// Tests both transient (TaskHostFactory) and sidecar (AssemblyTaskFactory) task hosts
        /// with different configuration combinations.
        /// </summary>
        /// <param name="taskHostFactorySpecified">Whether to use TaskHostFactory (transient) or AssemblyTaskFactory (sidecar)</param>
        /// <param name="envVariableSpecified">Whether to set MSBUILDFORCEALLTASKSOUTOFPROC environment variable</param>
        [Theory]
        [InlineData(true, false)]
        // [InlineData(false, true)] - the process can not be spawned on CI sometimes. A new approach is needed.
        [InlineData(true, true)]
        public void TaskNodesDieAfterBuild(bool taskHostFactorySpecified, bool envVariableSpecified)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                string taskFactory = taskHostFactorySpecified ? "TaskHostFactory" : "AssemblyTaskFactory";
                string pidTaskProject = $@"
<Project>
    <UsingTask TaskName=""ProcessIdTask"" AssemblyFile=""{AssemblyLocation}"" TaskFactory=""{taskFactory}"" />
    <Target Name='AccessPID'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
    </Target>
</Project>";
                TransientTestFile project = env.CreateFile("testProject.csproj", pidTaskProject);

                if (envVariableSpecified)
                {
                    env.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
                }

                // To execute the task in sidecar mode, both node reuse and the environment variable must be set.
                BuildParameters buildParameters = new() { EnableNodeReuse = envVariableSpecified && true /* node reuse enabled */ };

                ProjectInstance projectInstance = new(project.Path);

                BuildManager buildManager = BuildManager.DefaultBuildManager;
                BuildResult buildResult = buildManager.Build(buildParameters, new BuildRequestData(projectInstance, targetsToBuild: ["AccessPID"]));

                buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

                string processId = projectInstance.GetPropertyValue("PID");
                string.IsNullOrEmpty(processId).ShouldBeFalse();
                Int32.TryParse(processId, out int pid).ShouldBeTrue();
                Process.GetCurrentProcess().Id.ShouldNotBe(pid);

                if (taskHostFactorySpecified)
                {
                    try
                    {
                        Process taskHostNode = Process.GetProcessById(pid);

                        // Capture identity up-front so a PID-reuse race (the OS recycled this
                        // pid to an unrelated process between build-end and GetProcessById) is
                        // visible in the failure diagnostic rather than looking like the task
                        // host hung.
                        string capturedName = SafeGetProcessField(() => taskHostNode.ProcessName);
                        string capturedStart = SafeGetProcessField(() => taskHostNode.StartTime.ToString("O", CultureInfo.InvariantCulture));

                        // The task host should exit shortly after the build completes. Use a generous
                        // timeout because slow CI agents have been observed to take up to ~10s for the
                        // child process to drain stdio and exit.
                        // TELEMETRY: elapsedMs is logged so a future iteration can tune this back down
                        // to a tight-but-safe value. If observed elapsed never approaches the timeout,
                        // shrink TaskHostExitTimeoutMs in a follow-up PR.
                        const int TaskHostExitTimeoutMs = 15000;
                        Stopwatch sw = Stopwatch.StartNew();
                        bool exited = taskHostNode.WaitForExit(TaskHostExitTimeoutMs);
                        sw.Stop();
                        _output.WriteLine(
                            $"TaskHostFactory wait: pid={pid} processName={capturedName} startTime={capturedStart} " +
                            $"exited={exited} elapsedMs={sw.ElapsedMilliseconds} timeoutMs={TaskHostExitTimeoutMs}");

                        // Wrap HasExited in SafeGetProcessField — Process.HasExited can throw on
                        // access-denied / transient handle failures, and the message is evaluated
                        // eagerly even when the assertion passes.
                        exited.ShouldBeTrue(
                            $"TaskHost (pid={pid}, name={capturedName}, started={capturedStart}) was still running after {TaskHostExitTimeoutMs}ms. " +
                            $"elapsedMs={sw.ElapsedMilliseconds} HasExited={SafeGetProcessField(() => taskHostNode.HasExited.ToString())}");
                    }

                    // We expect the TaskHostNode to exit quickly. If it exits before Process.GetProcessById, it will throw an ArgumentException.
                    catch (ArgumentException e)
                    {
                        e.Message.ShouldBe($"Process with an Id of {pid} is not running.");
                    }
                }
                else
                {
                    Process taskHostNode = Process.GetProcessById(pid);

                    // This is the sidecar TaskHost case - it should persist after build is done. So we need to clean up and kill it ourselves.
                    // Wait for process to be responsive. The standard 3 secs can be not enough for the child process to start, let's try several times.
                    int attempts = 0;
                    while (attempts < 10)
                    {
                        try
                        {
                            if (taskHostNode.HasExited)
                            {
                                Assert.Fail($"TaskHost exited during startup with code: {taskHostNode.ExitCode}");
                            }

                            // Check if process has loaded its main module
                            if (taskHostNode.Modules.Count > 0)
                            {
                                break;
                            }
                        }
                        catch
                        {
                            // Process not ready yet
                        }

                        Thread.Sleep(2000);
                        attempts++;
                        taskHostNode.Refresh();
                    }

                    // Now wait to ensure it stays alive
                    bool processExited = taskHostNode.WaitForExit(3000);

                    processExited.ShouldBeFalse(
                        processExited
                            ? $"TaskHost should remain alive after build. TaskHost exited with code: {taskHostNode.ExitCode}"
                            : "TaskHost should remain alive after build for task host case.");

                    try
                    {
                        taskHostNode.Kill();
                    }
                    catch
                    {
                        // Ignore exceptions from Kill - the process may have exited between the WaitForExit and Kill calls.
                    }
                }
            }
        }

        // Some Process fields (ProcessName, StartTime) can throw if the process
        // has already exited or access is denied. We capture them best-effort for
        // diagnostic output; never let a diagnostic read fail the test.
        private static string SafeGetProcessField(Func<string> read)
        {
            try
            {
                return read();
            }
            catch (Exception ex)
            {
                return $"<unavailable: {ex.GetType().Name}>";
            }
        }

        /// <summary>
        /// Verifies that transient (TaskHostFactory) and sidecar (AssemblyTaskFactory) task hosts
        /// can coexist in the same build and operate independently.
        /// </summary>
        [Fact]
        public void TransientAndSidecarNodeCanCoexist()
        {
            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                string pidTaskProject = $@"
<Project>
    <UsingTask TaskName=""ProcessIdTask"" AssemblyFile=""{AssemblyLocation}"" TaskFactory=""TaskHostFactory"" />
    <UsingTask TaskName=""ProcessIdTaskSidecar"" AssemblyFile=""{AssemblyLocation}"" TaskFactory=""AssemblyTaskFactory"" />

    <Target Name='AccessPID'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
        <ProcessIdTaskSidecar>
            <Output PropertyName=""PID2"" TaskParameter=""Pid"" />
        </ProcessIdTaskSidecar>
    </Target>
</Project>";

                TransientTestFile project = env.CreateFile("testProject.csproj", pidTaskProject);

                env.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
                ProjectInstance projectInstance = new(project.Path);

                projectInstance.Build().ShouldBeTrue();

                string transientPid = projectInstance.GetPropertyValue("PID");
                string sidecarPid = projectInstance.GetPropertyValue("PID2");
                sidecarPid.ShouldNotBe(transientPid, "Each task should have it's own TaskHost node.");

                string.IsNullOrEmpty(transientPid).ShouldBeFalse();
                Int32.TryParse(transientPid, out int pid).ShouldBeTrue();
                Int32.TryParse(sidecarPid, out int pidSidecar).ShouldBeTrue();

                Process.GetCurrentProcess().Id.ShouldNotBe(pid);

                try
                {
                    Process transientTaskHostNode = Process.GetProcessById(pid);
                    transientTaskHostNode.WaitForExit(3000).ShouldBeTrue("The node should be dead since this is the transient case.");
                }
                catch (ArgumentException e)
                {
                    // We expect the TaskHostNode to exit quickly. If it exits before Process.GetProcessById, it will throw an ArgumentException.
                    e.Message.ShouldBe($"Process with an Id of {pid} is not running.");
                }

                try
                {
                    // This is the sidecar TaskHost case - it should persist after build is done. So we need to clean up and kill it ourselves.
                    Process sidecarTaskHostNode = Process.GetProcessById(pidSidecar);
                    sidecarTaskHostNode.WaitForExit(3000).ShouldBeFalse($"The node should be alive since it is the sidecar node.");
                    sidecarTaskHostNode.Kill();
                }
                catch (Exception e)
                {
                    e.Message.ShouldNotBe($"Process with an Id of {pidSidecar} is not running");
                }
            }
        }

        /// <summary>
        /// Regression test for the out-of-proc task host environment-reuse optimization, which lets the host skip
        /// re-applying the (unchanged) build process environment between consecutive tasks. The load-bearing
        /// assertion is that all three tasks run in the SAME reused task host process (a shared task-host process
        /// id, distinct from this build process), proving the skip path was actually exercised. Each invocation --
        /// including ones whose apply was skipped -- must also still observe a consistent build environment.
        /// (Note: the variable is also inherited by the child process, so the read alone does not isolate the
        /// apply; the dedicated coverage for apply correctness is the serialization and -mt change-propagation
        /// tests.)
        /// </summary>
        [Fact]
        public void TaskHostObservesEnvironmentAcrossConsecutiveTasks()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string variableName = "MSBUILD_ENV_REUSE_TEST_" + Guid.NewGuid().ToString("N");
            const string variableValue = "reuse_value_123";
            env.SetEnvironmentVariable(variableName, variableValue);
            env.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(ReadEnvironmentVariableTask)}"" AssemblyFile=""{typeof(ReadEnvironmentVariableTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name='Build'>
        <{nameof(ReadEnvironmentVariableTask)} VariableName=""{variableName}"">
            <Output PropertyName=""Value1"" TaskParameter=""Value"" />
            <Output PropertyName=""Pid1"" TaskParameter=""Pid"" />
        </{nameof(ReadEnvironmentVariableTask)}>
        <{nameof(ReadEnvironmentVariableTask)} VariableName=""{variableName}"">
            <Output PropertyName=""Value2"" TaskParameter=""Value"" />
            <Output PropertyName=""Pid2"" TaskParameter=""Pid"" />
        </{nameof(ReadEnvironmentVariableTask)}>
        <{nameof(ReadEnvironmentVariableTask)} VariableName=""{variableName}"">
            <Output PropertyName=""Value3"" TaskParameter=""Value"" />
            <Output PropertyName=""Pid3"" TaskParameter=""Pid"" />
        </{nameof(ReadEnvironmentVariableTask)}>
    </Target>
</Project>";

            TransientTestFile project = env.CreateFile("envReuseProject.csproj", projectContents);
            ProjectInstance projectInstance = new(project.Path);

            projectInstance.Build().ShouldBeTrue();

            // Every task invocation -- including ones whose environment apply was skipped because the environment
            // was unchanged from the previous task -- must still observe the build-set variable.
            projectInstance.GetPropertyValue("Value1").ShouldBe(variableValue);
            projectInstance.GetPropertyValue("Value2").ShouldBe(variableValue);
            projectInstance.GetPropertyValue("Value3").ShouldBe(variableValue);

            // All three invocations should have run in the same task host process (so the reuse path was actually
            // exercised) and not in the current build process.
            string pid1 = projectInstance.GetPropertyValue("Pid1");
            pid1.ShouldNotBeNullOrEmpty();
            pid1.ShouldBe(projectInstance.GetPropertyValue("Pid2"));
            pid1.ShouldBe(projectInstance.GetPropertyValue("Pid3"));
            pid1.ShouldNotBe(Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Regression test for the out-of-proc task host forward environment-delta optimization (#14097). To avoid
        /// re-transmitting the (invariant) build process environment in every <see cref="TaskHostConfiguration"/>,
        /// the parent sends it once per connection and marks subsequent unchanged configurations "identical". In
        /// multithreaded mode the configuration's environment is the task environment driver's live backing
        /// dictionary, which is mutated in place between tasks; the baseline used for the "identical" decision must
        /// therefore be a snapshot, not an alias of that live dictionary. If it aliases the live dictionary, the
        /// equivalence check short-circuits on reference equality and a genuinely changed environment is silently
        /// marked "identical", so the task host reconstructs a stale environment. This verifies that when one
        /// task-host task changes an environment variable, a subsequent task-host task observes the new value.
        /// </summary>
        [Fact]
        public void TaskHostObservesEnvironmentChangedByPreviousTaskInMultiThreadedMode()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string variableName = "MSBUILD_ENV_DELTA_TEST_" + Guid.NewGuid().ToString("N");
            const string changedValue = "changed_by_first_task";

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(SetEnvironmentVariableTask)}"" AssemblyFile=""{typeof(SetEnvironmentVariableTask).Assembly.Location}"" />
    <UsingTask TaskName=""{nameof(ReadEnvironmentVariableTask)}"" AssemblyFile=""{typeof(ReadEnvironmentVariableTask).Assembly.Location}"" />
    <Target Name='Build'>
        <{nameof(SetEnvironmentVariableTask)} VariableName=""{variableName}"" Value=""{changedValue}"">
            <Output PropertyName=""SetPid"" TaskParameter=""Pid"" />
        </{nameof(SetEnvironmentVariableTask)}>
        <{nameof(ReadEnvironmentVariableTask)} VariableName=""{variableName}"">
            <Output PropertyName=""ObservedValue"" TaskParameter=""Value"" />
            <Output PropertyName=""ReadPid"" TaskParameter=""Pid"" />
        </{nameof(ReadEnvironmentVariableTask)}>
    </Target>
</Project>";

            TransientTestFile project = env.CreateFile("envDeltaProject.proj", projectContents);

            MockLogger logger = new(_output);
            BuildParameters buildParameters = new()
            {
                MultiThreaded = true,
                DisableInProcNode = false,
                EnableNodeReuse = false,
                Loggers = [logger],
            };

            BuildRequestData buildRequestData = new(
                project.Path,
                new Dictionary<string, string>(),
                null,
                ["Build"],
                null,
                BuildRequestDataFlags.ProvideProjectStateAfterBuild);

            BuildResult result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            // Both tasks must have actually run out of proc, otherwise Environment.SetEnvironmentVariable and
            // Environment.GetEnvironmentVariable would share a single process environment and the test would pass
            // regardless of the delta logic. A non-empty pid that differs from this process, equal across both
            // tasks, proves they ran in the same external task host (so the per-connection delta path was used).
            ProjectInstance state = result.ProjectStateAfterBuild;
            state.ShouldNotBeNull();
            string setPid = state.GetPropertyValue("SetPid");
            string readPid = state.GetPropertyValue("ReadPid");
            setPid.ShouldNotBeNullOrEmpty();
            readPid.ShouldNotBe(Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
            readPid.ShouldBe(setPid);

            // The second task-host task must observe the environment variable set by the first. Before the fix the
            // forward env-delta baseline aliased the live (mutated-in-place) environment dictionary, so this second
            // configuration was falsely marked "identical" and the task host reconstructed a stale environment that
            // did not contain the variable -- ObservedValue would be empty.
            state.GetPropertyValue("ObservedValue").ShouldBe(changedValue);
        }

        /// <summary>
        /// When a task running in the out-of-proc task host mutates the environment (here, by setting a variable),
        /// that changed environment must be sent in <see cref="InvariantPayloadTransferMode.Full"/> mode -- both back
        /// to the parent on task completion and forward to the next task's configuration -- rather than being
        /// deduplicated as "identical". Full mode makes the mutation the new per-connection baseline, so it
        /// propagates to subsequent tasks. This verifies the mutation reaches a later task, and that once it has
        /// become the baseline it also reaches a third task whose configuration is sent as the deduped "identical"
        /// marker (reconstructed from the updated baseline). If the mutated environment had instead been marked
        /// "identical", the readers would reconstruct a stale environment and observe an empty value.
        /// </summary>
        [Fact]
        public void TaskHostSendsFullEnvironmentWhenTaskMutatesItAndLaterTasksObserveChange()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string variableName = "MSBUILD_ENV_MUTATE_TEST_" + Guid.NewGuid().ToString("N");
            const string changedValue = "set_by_first_task";

            // The variable is deliberately absent from the build environment, so the only way a later task can observe
            // it is if the first task's mutation is propagated in full through the parent to the subsequent tasks.
            Environment.GetEnvironmentVariable(variableName).ShouldBeNull();

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(SetEnvironmentVariableTask)}"" AssemblyFile=""{typeof(SetEnvironmentVariableTask).Assembly.Location}"" />
    <UsingTask TaskName=""{nameof(ReadEnvironmentVariableTask)}"" AssemblyFile=""{typeof(ReadEnvironmentVariableTask).Assembly.Location}"" />
    <Target Name='Build'>
        <{nameof(SetEnvironmentVariableTask)} VariableName=""{variableName}"" Value=""{changedValue}"">
            <Output PropertyName=""SetPid"" TaskParameter=""Pid"" />
        </{nameof(SetEnvironmentVariableTask)}>
        <{nameof(ReadEnvironmentVariableTask)} VariableName=""{variableName}"">
            <Output PropertyName=""ObservedValue1"" TaskParameter=""Value"" />
            <Output PropertyName=""ReadPid1"" TaskParameter=""Pid"" />
        </{nameof(ReadEnvironmentVariableTask)}>
        <{nameof(ReadEnvironmentVariableTask)} VariableName=""{variableName}"">
            <Output PropertyName=""ObservedValue2"" TaskParameter=""Value"" />
            <Output PropertyName=""ReadPid2"" TaskParameter=""Pid"" />
        </{nameof(ReadEnvironmentVariableTask)}>
    </Target>
</Project>";

            TransientTestFile project = env.CreateFile("envMutateProject.proj", projectContents);

            MockLogger logger = new(_output);
            BuildParameters buildParameters = new()
            {
                MultiThreaded = true,
                DisableInProcNode = false,
                EnableNodeReuse = false,
                Loggers = [logger],
            };

            BuildRequestData buildRequestData = new(
                project.Path,
                new Dictionary<string, string>(),
                null,
                ["Build"],
                null,
                BuildRequestDataFlags.ProvideProjectStateAfterBuild);

            BuildResult result = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequestData);

            result.OverallResult.ShouldBe(BuildResultCode.Success);

            ProjectInstance state = result.ProjectStateAfterBuild;
            state.ShouldNotBeNull();

            // The tasks must have actually run out of proc (pid differs from this process); otherwise the environment
            // would be shared within a single process and the test would pass regardless of the delta logic.
            string setPid = state.GetPropertyValue("SetPid");
            setPid.ShouldNotBeNullOrEmpty();
            setPid.ShouldNotBe(Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));

            // Both reader tasks must observe the value the first task set: the second because its configuration is sent
            // in full after the change, the third because the change became the connection baseline it reconstructs from.
            state.GetPropertyValue("ObservedValue1").ShouldBe(changedValue);
            state.GetPropertyValue("ObservedValue2").ShouldBe(changedValue);
        }

        /// <summary>
        /// Regression test for the out-of-proc task host forward global-properties delta optimization. To avoid
        /// re-transmitting the (largely invariant) global properties in every <see cref="TaskHostConfiguration"/>,
        /// the parent sends them in full once per connection and marks subsequent unchanged configurations
        /// "identical" (no dictionary on the wire); the task host reconstructs them from the per-connection baseline.
        /// This verifies that every consecutive task -- including ones whose configuration was sent as the deduped
        /// "identical" marker -- still observes the build's global properties via <see cref="IBuildEngine6.GetGlobalProperties"/>,
        /// including the large <c>CurrentSolutionConfigurationContents</c> property whose redundancy motivated the change.
        /// The load-bearing assertion is that all invocations run in the SAME reused task host process (so the dedup
        /// path was actually exercised) yet each still reads the correct values.
        /// </summary>
        [Fact]
        public void TaskHostObservesGlobalPropertyAcrossConsecutiveTasks()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string propertyName = "MSBuildGlobalPropReuseTest" + Guid.NewGuid().ToString("N");
            const string propertyValue = "global_value_123";
            const string solutionConfigValue = "<SolutionConfiguration><ProjectConfiguration>Debug|AnyCPU</ProjectConfiguration></SolutionConfiguration>";
            env.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(ReadGlobalPropertyTask)}"" AssemblyFile=""{typeof(ReadGlobalPropertyTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name='Build'>
        <{nameof(ReadGlobalPropertyTask)} PropertyName=""{propertyName}"">
            <Output PropertyName=""Value1"" TaskParameter=""Value"" />
            <Output PropertyName=""Pid1"" TaskParameter=""Pid"" />
        </{nameof(ReadGlobalPropertyTask)}>
        <{nameof(ReadGlobalPropertyTask)} PropertyName=""{propertyName}"">
            <Output PropertyName=""Value2"" TaskParameter=""Value"" />
            <Output PropertyName=""Pid2"" TaskParameter=""Pid"" />
        </{nameof(ReadGlobalPropertyTask)}>
        <{nameof(ReadGlobalPropertyTask)} PropertyName=""{propertyName}"">
            <Output PropertyName=""Value3"" TaskParameter=""Value"" />
            <Output PropertyName=""Pid3"" TaskParameter=""Pid"" />
        </{nameof(ReadGlobalPropertyTask)}>
        <{nameof(ReadGlobalPropertyTask)} PropertyName=""CurrentSolutionConfigurationContents"">
            <Output PropertyName=""BlobValue"" TaskParameter=""Value"" />
        </{nameof(ReadGlobalPropertyTask)}>
    </Target>
</Project>";

            TransientTestFile project = env.CreateFile("globalPropReuseProject.csproj", projectContents);

            Dictionary<string, string> globalProperties = new()
            {
                [propertyName] = propertyValue,
                ["CurrentSolutionConfigurationContents"] = solutionConfigValue,
            };
            ProjectInstance projectInstance = new(project.Path, globalProperties, toolsVersion: null);

            projectInstance.Build().ShouldBeTrue();

            // Every task invocation -- including the second and third, whose global-properties configuration was sent
            // as the deduped "identical" marker (no dictionary on the wire) -- must still observe the global property,
            // proving the task host reconstructs it from the per-connection baseline.
            projectInstance.GetPropertyValue("Value1").ShouldBe(propertyValue);
            projectInstance.GetPropertyValue("Value2").ShouldBe(propertyValue);
            projectInstance.GetPropertyValue("Value3").ShouldBe(propertyValue);

            // The large CurrentSolutionConfigurationContents property -- the redundancy this optimization targets --
            // must also round-trip through the dedup path.
            projectInstance.GetPropertyValue("BlobValue").ShouldBe(solutionConfigValue);

            // All invocations should have run in the same task host process (so the per-connection dedup path was
            // actually exercised) and not in the current build process.
            string pid1 = projectInstance.GetPropertyValue("Pid1");
            pid1.ShouldNotBeNullOrEmpty();
            pid1.ShouldBe(projectInstance.GetPropertyValue("Pid2"));
            pid1.ShouldBe(projectInstance.GetPropertyValue("Pid3"));
            pid1.ShouldNotBe(Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Verifies that various parameter types can be correctly transmitted to and received from
        /// a task host process, ensuring proper serialization/deserialization of all supported types.
        /// Tests include primitive types, arrays, strings, dates, enums, and custom structures.
        /// </summary>
        [Fact]
        public void VariousParameterTypesCanBeTransmittedToAndReceivedFromTaskHost()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            string boolParam = "True";
            string boolArrayParam = "False;True;False";
            string byteParam = "42";
            string byteArrayParam = "11;22;33";
            string sbyteParam = "-42";
            string sbyteArrayParam = "-11;-22;-33";
            string doubleParam = "3.14";
            string doubleArrayParam = "3.14;2.72";
            string floatParam = "0.5";
            string floatArrayParam = "0.6;0.7;0.8";
            string shortParam = "-100";
            string shortArrayParam = "-200;-300;999";
            string ushortParam = "100";
            string ushortArrayParam = "200;300;999";
            string intParam = "-314";
            string intArrayParam = "42;-67;98";
            string uintParam = "314";
            string uintArrayParam = "4200000;67;98";
            string longParam = "-120000000000";
            string longArrayParam = "-120000000000;0;1";
            string ulongParam = "120000000000";
            string ulongArrayParam = "120000000000;0;1";
            string decimalParam = "0.999999999999";
            string decimalArrayParam = "-0.999999999999";
            string charParam = "A";
            string charArrayParam = "A;b;2";
            string stringParam = "stringParamInput";
            string stringArrayParam = "stringArrayParamInput1;stringArrayParamInput2;stringArrayParamInput3";
            string dateTimeParam = "01/01/2001 10:15:00";
            string dateTimeArrayParam = "01/01/2001 10:15:00;02/02/2002 11:30:00;03/03/2003 12:45:00";

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{nameof(TaskBuilderTestTask)}"" AssemblyFile=""{typeof(TaskBuilderTestTask).Assembly.Location}"" TaskFactory=""TaskHostFactory"" />
    <Target Name='{nameof(VariousParameterTypesCanBeTransmittedToAndReceivedFromTaskHost)}'>
        <{nameof(TaskBuilderTestTask)}
            ExecuteReturnParam=""true""
            BoolParam=""{boolParam}""
            BoolArrayParam=""{boolArrayParam}""
            ByteParam=""{byteParam}""
            ByteArrayParam=""{byteArrayParam}""
            SByteParam=""{sbyteParam}""
            SByteArrayParam=""{sbyteArrayParam}""
            DoubleParam=""{doubleParam}""
            DoubleArrayParam=""{doubleArrayParam}""
            FloatParam=""{floatParam}""
            FloatArrayParam=""{floatArrayParam}""
            ShortParam=""{shortParam}""
            ShortArrayParam=""{shortArrayParam}""
            UShortParam=""{ushortParam}""
            UShortArrayParam=""{ushortArrayParam}""
            IntParam=""{intParam}""
            IntArrayParam=""{intArrayParam}""
            UIntParam=""{uintParam}""
            UIntArrayParam=""{uintArrayParam}""
            LongParam=""{longParam}""
            LongArrayParam=""{longArrayParam}""
            ULongParam=""{ulongParam}""
            ULongArrayParam=""{ulongArrayParam}""
            DecimalParam=""{decimalParam}""
            DecimalArrayParam=""{decimalArrayParam}""
            CharParam=""{charParam}""
            CharArrayParam=""{charArrayParam}""
            StringParam=""{stringParam}""
            StringArrayParam=""{stringArrayParam}""
            DateTimeParam=""{dateTimeParam}""
            DateTimeArrayParam=""{dateTimeArrayParam}"">

            <Output PropertyName=""BoolOutput"" TaskParameter=""BoolOutput"" />
            <Output PropertyName=""BoolArrayOutput"" TaskParameter=""BoolArrayOutput"" />
            <Output PropertyName=""ByteOutput"" TaskParameter=""ByteOutput"" />
            <Output PropertyName=""ByteArrayOutput"" TaskParameter=""ByteArrayOutput"" />
            <Output PropertyName=""SByteOutput"" TaskParameter=""SByteOutput"" />
            <Output PropertyName=""SByteArrayOutput"" TaskParameter=""SByteArrayOutput"" />
            <Output PropertyName=""DoubleOutput"" TaskParameter=""DoubleOutput"" />
            <Output PropertyName=""DoubleArrayOutput"" TaskParameter=""DoubleArrayOutput"" />
            <Output PropertyName=""FloatOutput"" TaskParameter=""FloatOutput"" />
            <Output PropertyName=""FloatArrayOutput"" TaskParameter=""FloatArrayOutput"" />
            <Output PropertyName=""ShortOutput"" TaskParameter=""ShortOutput"" />
            <Output PropertyName=""ShortArrayOutput"" TaskParameter=""ShortArrayOutput"" />
            <Output PropertyName=""UShortOutput"" TaskParameter=""UShortOutput"" />
            <Output PropertyName=""UShortArrayOutput"" TaskParameter=""UShortArrayOutput"" />
            <Output PropertyName=""IntOutput"" TaskParameter=""IntOutput"" />
            <Output PropertyName=""IntArrayOutput"" TaskParameter=""IntArrayOutput"" />
            <Output PropertyName=""UIntOutput"" TaskParameter=""UIntOutput"" />
            <Output PropertyName=""UIntArrayOutput"" TaskParameter=""UIntArrayOutput"" />
            <Output PropertyName=""LongOutput"" TaskParameter=""LongOutput"" />
            <Output PropertyName=""LongArrayOutput"" TaskParameter=""LongArrayOutput"" />
            <Output PropertyName=""ULongOutput"" TaskParameter=""ULongOutput"" />
            <Output PropertyName=""ULongArrayOutput"" TaskParameter=""ULongArrayOutput"" />
            <Output PropertyName=""DecimalOutput"" TaskParameter=""DecimalOutput"" />
            <Output PropertyName=""DecimalArrayOutput"" TaskParameter=""DecimalArrayOutput"" />
            <Output PropertyName=""CharOutput"" TaskParameter=""CharOutput"" />
            <Output PropertyName=""CharArrayOutput"" TaskParameter=""CharArrayOutput"" />
            <Output PropertyName=""StringOutput"" TaskParameter=""StringOutput"" />
            <Output PropertyName=""StringArrayOutput"" TaskParameter=""StringArrayOutput"" />
            <Output PropertyName=""DateTimeOutput"" TaskParameter=""DateTimeOutput"" />
            <Output PropertyName=""DateTimeArrayOutput"" TaskParameter=""DateTimeArrayOutput"" />
            <Output PropertyName=""CustomStructOutput"" TaskParameter=""CustomStructOutput"" />
            <Output PropertyName=""EnumOutput"" TaskParameter=""EnumOutput"" />
        </{nameof(TaskBuilderTestTask)}>
    </Target>
</Project>";
            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles(projectContents);
            ProjectInstance projectInstance = new(project.ProjectFile);
            projectInstance.Build(new[] { new MockLogger(env.Output) }).ShouldBeTrue();

            projectInstance.GetPropertyValue("BoolOutput").ShouldBe(boolParam);
            projectInstance.GetPropertyValue("BoolArrayOutput").ShouldBe(boolArrayParam);
            projectInstance.GetPropertyValue("ByteOutput").ShouldBe(byteParam);
            projectInstance.GetPropertyValue("ByteArrayOutput").ShouldBe(byteArrayParam);
            projectInstance.GetPropertyValue("SByteOutput").ShouldBe(sbyteParam);
            projectInstance.GetPropertyValue("SByteArrayOutput").ShouldBe(sbyteArrayParam);
            projectInstance.GetPropertyValue("DoubleOutput").ShouldBe(doubleParam);
            projectInstance.GetPropertyValue("DoubleArrayOutput").ShouldBe(doubleArrayParam);
            projectInstance.GetPropertyValue("FloatOutput").ShouldBe(floatParam);
            projectInstance.GetPropertyValue("FloatArrayOutput").ShouldBe(floatArrayParam);
            projectInstance.GetPropertyValue("ShortOutput").ShouldBe(shortParam);
            projectInstance.GetPropertyValue("ShortArrayOutput").ShouldBe(shortArrayParam);
            projectInstance.GetPropertyValue("UShortOutput").ShouldBe(ushortParam);
            projectInstance.GetPropertyValue("UShortArrayOutput").ShouldBe(ushortArrayParam);
            projectInstance.GetPropertyValue("IntOutput").ShouldBe(intParam);
            projectInstance.GetPropertyValue("IntArrayOutput").ShouldBe(intArrayParam);
            projectInstance.GetPropertyValue("UIntOutput").ShouldBe(uintParam);
            projectInstance.GetPropertyValue("UIntArrayOutput").ShouldBe(uintArrayParam);
            projectInstance.GetPropertyValue("LongOutput").ShouldBe(longParam);
            projectInstance.GetPropertyValue("LongArrayOutput").ShouldBe(longArrayParam);
            projectInstance.GetPropertyValue("ULongOutput").ShouldBe(ulongParam);
            projectInstance.GetPropertyValue("ULongArrayOutput").ShouldBe(ulongArrayParam);
            projectInstance.GetPropertyValue("DecimalOutput").ShouldBe(decimalParam);
            projectInstance.GetPropertyValue("DecimalArrayOutput").ShouldBe(decimalArrayParam);
            projectInstance.GetPropertyValue("CharOutput").ShouldBe(charParam);
            projectInstance.GetPropertyValue("CharArrayOutput").ShouldBe(charArrayParam);
            projectInstance.GetPropertyValue("StringOutput").ShouldBe(stringParam);
            projectInstance.GetPropertyValue("StringArrayOutput").ShouldBe(stringArrayParam);
            projectInstance.GetPropertyValue("DateTimeOutput").ShouldBe(dateTimeParam);
            projectInstance.GetPropertyValue("DateTimeArrayOutput").ShouldBe(dateTimeArrayParam);
            projectInstance.GetPropertyValue("CustomStructOutput").ShouldBe(TaskBuilderTestTask.s_customStruct.ToString(CultureInfo.InvariantCulture));
            projectInstance.GetPropertyValue("EnumOutput").ShouldBe(TargetBuiltReason.BeforeTargets.ToString());
        }

        /// <summary>
        /// Verifies that a task returning a string[] with null elements does not crash
        /// when executed via TaskHostFactory. This is a regression test for
        /// https://github.com/dotnet/msbuild/issues/13174
        /// </summary>
        [Fact]
        public void StringArrayWithNullsDoesNotCrashTaskHost()
        {
            using TestEnvironment env = TestEnvironment.Create();

            string projectContents = $@"
<Project>
    <UsingTask TaskName=""{typeof(StringArrayWithNullsTask).FullName}"" AssemblyFile=""{AssemblyLocation}"" TaskFactory=""TaskHostFactory"" />
    <Target Name=""TestTarget"">
        <{typeof(StringArrayWithNullsTask).Name}>
            <Output ItemName=""OutputItems"" TaskParameter=""OutputArray"" />
            <Output PropertyName=""TaskPid"" TaskParameter=""Pid"" />
        </{typeof(StringArrayWithNullsTask).Name}>
    </Target>
</Project>";

            TransientTestFile project = env.CreateFile("testProject.csproj", projectContents);
            ProjectInstance projectInstance = new(project.Path);
            BuildManager buildManager = BuildManager.DefaultBuildManager;
            BuildResult buildResult = buildManager.Build(new BuildParameters(), new BuildRequestData(projectInstance, targetsToBuild: new[] { "TestTarget" }));

            // The build should succeed - nulls should be filtered, not cause a crash
            buildResult.OverallResult.ShouldBe(BuildResultCode.Success);

            // Verify task ran out-of-process (TaskHostFactory should force this)
            string taskPidStr = projectInstance.GetPropertyValue("TaskPid");
            taskPidStr.ShouldNotBeNullOrEmpty();
            int.TryParse(taskPidStr, out int taskPid).ShouldBeTrue();
            Process.GetCurrentProcess().Id.ShouldNotBe(taskPid, "Task should have run in a separate TaskHost process");

            // Verify output items - nulls should be filtered out, leaving 3 items
            var outputItems = projectInstance.GetItems("OutputItems");
            outputItems.Count.ShouldBe(3, "Null elements should be filtered from the string array");
        }
    }
}
