// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(TaskHostFactory_Tests).Assembly.Location) ?? AppContext.BaseDirectory, "Microsoft.Build.Engine.UnitTests.dll");

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
    }
}
