// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.BackEnd;

using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the TaskHostFactory functionality, which manages task host processes
    /// for executing MSBuild tasks in separate processes.
    /// </summary>
    public sealed class TaskHostFactory_Tests
    {
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
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void TaskNodesDieAfterBuild(bool taskHostFactorySpecified, bool envVariableSpecified)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                using ProcessTracker processTracker = new();
                string taskFactory = taskHostFactorySpecified ? "TaskHostFactory" : "AssemblyTaskFactory";
                string pidTaskProject = $@"
<Project>
    <UsingTask TaskName=""ProcessIdTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests"" TaskFactory=""{taskFactory}"" />
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

                ProjectInstance projectInstance = new(project.Path);

                projectInstance.Build().ShouldBeTrue();
                string processId = projectInstance.GetPropertyValue("PID");
                string.IsNullOrEmpty(processId).ShouldBeFalse();
                Int32.TryParse(processId, out int pid).ShouldBeTrue();
                Process.GetCurrentProcess().Id.ShouldNotBe(pid);

                if (taskHostFactorySpecified)
                {
                    try
                    {
                        Process taskHostNode = Process.GetProcessById(pid);
                        taskHostNode.WaitForExit(3000).ShouldBeTrue("The process with taskHostNode is still running.");
                    }

                    // We expect the TaskHostNode to exit quickly. If it exits before Process.GetProcessById, it will throw an ArgumentException.
                    catch (ArgumentException e)
                    {
                        e.Message.ShouldBe($"Process with an Id of {pid} is not running.");
                    }
                }
                else
                {
                    try
                    {
                        // This is the sidecar TaskHost case - it should persist after build is done. So we need to clean up and kill it ourselves.
                        Process taskHostNode = Process.GetProcessById(pid);
                        using var taskHostNodeTracker = processTracker.AttachToProcess(pid, "Sidecar", _output);
                        bool processExited = taskHostNode.WaitForExit(3000);
                        if (processExited)
                        {
                            processTracker.PrintSummary(_output);
                        }

                        processExited.ShouldBeFalse();
                        taskHostNode.Kill();
                    }
                    catch
                    {
                        processTracker.PrintSummary(_output);
                    }
                }
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
                using ProcessTracker processTracker = new();
                {
                    string pidTaskProject = $@"
<Project>
<UsingTask TaskName=""ProcessIdTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests"" TaskFactory=""TaskHostFactory"" />
<UsingTask TaskName=""ProcessIdTaskSidecar"" AssemblyName=""Microsoft.Build.Engine.UnitTests"" TaskFactory=""AssemblyTaskFactory"" />

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

                    using var sidecarTracker = processTracker.AttachToProcess(int.Parse(sidecarPid), "Sidecar", _output);

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
                        processTracker.PrintSummary(_output);
                        e.Message.ShouldNotBe($"Process with an Id of {pidSidecar} is not running");
                    }
                }
            }
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
        /// Helper class for tracking external processes during tests.
        /// Monitors process lifecycle and provides diagnostic information for debugging.
        /// </summary>
        internal sealed class ProcessTracker : IDisposable
        {
            private readonly List<TrackedProcess> _trackedProcesses = new();

            /// <summary>
            /// Attaches to an existing process for monitoring.
            /// </summary>
            /// <param name="pid">Process ID to attach to</param>
            /// <param name="name">Friendly name for the process</param>
            /// <param name="output">Test output helper for logging</param>
            /// <returns>TrackedProcess instance for the attached process</returns>
            public TrackedProcess AttachToProcess(int pid, string name, ITestOutputHelper output)
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    var tracked = new TrackedProcess(process, name);

                    // Enable event notifications
                    process.EnableRaisingEvents = true;

                    // Subscribe to exit event
                    process.Exited += (sender, e) =>
                    {
                        var proc = sender as Process;
                        tracked.ExitTime = DateTime.Now;
                        tracked.ExitCode = proc?.ExitCode ?? -999;
                        tracked.HasExited = true;

                        output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {tracked.Name} (PID {tracked.ProcessId}) EXITED with code {tracked.ExitCode}");
                    };

                    _trackedProcesses.Add(tracked);
                    output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Attached to {name} (PID {pid})");

                    return tracked;
                }
                catch (ArgumentException)
                {
                    output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Could not attach to {name} (PID {pid}) - process not found");
                    return new TrackedProcess(null, name) { ProcessId = pid, NotFound = true };
                }
            }

            /// <summary>
            /// Prints a summary of all tracked processes for diagnostic purposes.
            /// </summary>
            /// <param name="output">Test output helper for logging</param>
            public void PrintSummary(ITestOutputHelper output)
            {
                output.WriteLine("\n=== PROCESS TRACKING SUMMARY ===");
                foreach (var tracked in _trackedProcesses)
                {
                    tracked.PrintStatus(output);
                }
            }

            public void Dispose()
            {
                foreach (var tracked in _trackedProcesses)
                {
                    tracked.Dispose();
                }
            }
        }

        /// <summary>
        /// Represents a tracked process with lifecycle monitoring capabilities.
        /// </summary>
        internal sealed class TrackedProcess : IDisposable
        {
            /// <summary>
            /// The underlying Process object being tracked.
            /// </summary>
            public Process Process { get; }

            /// <summary>
            /// Friendly name for the tracked process.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Process ID of the tracked process.
            /// </summary>
            public int ProcessId { get; set; }

            /// <summary>
            /// Time when tracking began for this process.
            /// </summary>
            public DateTime AttachTime { get; }

            /// <summary>
            /// Time when the process exited, if applicable.
            /// </summary>
            public DateTime? ExitTime { get; set; }

            /// <summary>
            /// Exit code of the process, if it has exited.
            /// </summary>
            public int? ExitCode { get; set; }

            /// <summary>
            /// Whether the process has exited.
            /// </summary>
            public bool HasExited { get; set; }

            /// <summary>
            /// Whether the process was not found when attempting to attach.
            /// </summary>
            public bool NotFound { get; set; }

            public TrackedProcess(Process process, string name)
            {
                Process = process;
                Name = name;
                ProcessId = process?.Id ?? -1;
                AttachTime = DateTime.Now;
            }

            /// <summary>
            /// Prints detailed status information about the tracked process.
            /// </summary>
            /// <param name="output">Test output helper for logging</param>
            public void PrintStatus(ITestOutputHelper output)
            {
                output.WriteLine($"\n{Name} (PID {ProcessId}):");
                output.WriteLine($"  Attached at: {AttachTime:HH:mm:ss.fff}");

                if (NotFound)
                {
                    output.WriteLine("  Status: Not found when trying to attach");
                }
                else if (HasExited)
                {
                    var duration = (ExitTime.Value - AttachTime).TotalMilliseconds;
                    output.WriteLine($"  Status: Exited with code {ExitCode}");
                    output.WriteLine($"  Exit time: {ExitTime:HH:mm:ss.fff}");
                    output.WriteLine($"  Duration: {duration:F0}ms");
                }
                else
                {
                    try
                    {
                        if (Process != null && !Process.HasExited)
                        {
                            output.WriteLine("  Status: Still running");
                            output.WriteLine($"  Start time: {Process.StartTime:HH:mm:ss.fff}");
                            output.WriteLine($"  CPU time: {Process.TotalProcessorTime.TotalMilliseconds:F0}ms");
                        }
                        else
                        {
                            output.WriteLine("  Status: Exited (detected during status check)");
                            if (Process != null)
                            {
                                output.WriteLine($"  Exit code: {Process.ExitCode}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        output.WriteLine($"  Status: Error checking process - {ex.Message}");
                    }
                }
            }

            public void Dispose() => Process?.Dispose();
        }
    }
}
