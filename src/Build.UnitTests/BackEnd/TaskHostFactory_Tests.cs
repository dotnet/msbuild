// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.BackEnd;

using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public sealed class TaskHostFactory_Tests
    {
        private ITestOutputHelper _output;

        public TaskHostFactory_Tests(ITestOutputHelper testOutputHelper)
        {
            _output = testOutputHelper;
        }

        [Fact]
        public void TaskNodesDieAfterBuild()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                string pidTaskProject = $@"
<Project>
    <UsingTask TaskName=""ProcessIdTask"" AssemblyName=""Microsoft.Build.Engine.UnitTests"" TaskFactory=""TaskHostFactory"" />
    <Target Name='AccessPID'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
    </Target>
</Project>";
                TransientTestFile project = env.CreateFile("testProject.csproj", pidTaskProject);
                ProjectInstance projectInstance = new(project.Path);
                projectInstance.Build().ShouldBeTrue();
                string processId = projectInstance.GetPropertyValue("PID");
                string.IsNullOrEmpty(processId).ShouldBeFalse();
                Int32.TryParse(processId, out int pid).ShouldBeTrue();
                Process.GetCurrentProcess().Id.ShouldNotBe<int>(pid);
                try
                {
                    Process taskHostNode = Process.GetProcessById(pid);
                    taskHostNode.WaitForExit(2000).ShouldBeTrue();
                }
                // We expect the TaskHostNode to exit quickly. If it exits before Process.GetProcessById, it will throw an ArgumentException.
                catch (ArgumentException e)
                {
                    e.Message.ShouldBe($"Process with an Id of {pid} is not running.");
                }
            }
        }

        [DotNetOnlyTheory]
        [InlineData(false)]
        // [InlineData(true)] <-- explicitly opting out on core will lead to node crash
        public void VariousParameterTypesCanBeTransmittedToAndReceivedFromTaskHost_NetCore(
            bool testLegacyImplementation)
            => VariousParameterTypesCanBeTransmittedToAndReceivedFromTaskHost(testLegacyImplementation);

        [WindowsFullFrameworkOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void VariousParameterTypesCanBeTransmittedToAndReceivedFromTaskHost_Framework(
            bool testLegacyImplementation)
            => VariousParameterTypesCanBeTransmittedToAndReceivedFromTaskHost(testLegacyImplementation);

        private void VariousParameterTypesCanBeTransmittedToAndReceivedFromTaskHost(bool testLegacyImplementation)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            ChangeWaves.ResetStateForTests();
            if (testLegacyImplementation)
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave17_6.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }

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
