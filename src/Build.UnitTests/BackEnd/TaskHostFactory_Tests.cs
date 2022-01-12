// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public sealed class TaskHostFactory_Tests
    {
        [Fact]
        [Trait("Category", "mono-osx-failing")]
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
    <Target Name='AccessPID2' AfterTargets='AccessPID'>
        <ProcessIdTask>
            <Output PropertyName=""PID2"" TaskParameter=""Pid"" />
        </ProcessIdTask>
    </Target>
</Project>";
                TransientTestFile project = env.CreateFile("testProject.csproj", pidTaskProject);
                ProjectInstance projectInstance = new(project.Path);
                projectInstance.Build().ShouldBeTrue();
                string processId = projectInstance.GetPropertyValue("PID");
                string.IsNullOrEmpty(processId).ShouldBeFalse();
                Int32.TryParse(processId, out int pid).ShouldBeTrue();
                string processId2 = projectInstance.GetPropertyValue("PID2");
                string.IsNullOrEmpty(processId2).ShouldBeFalse();
                Int32.TryParse(processId2, out int pid2).ShouldBeTrue();
                pid2.ShouldNotBe(pid);
                Process.GetCurrentProcess().Id.ShouldNotBe<int>(pid);
                try
                {
                    Process taskHostNode = Process.GetProcessById(pid2);
                    taskHostNode.WaitForExit(2000).ShouldBeTrue();
                }
                // We expect the TaskHostNode to exit quickly. If it exits before Process.GetProcessById, it will throw an ArgumentException.
                catch (ArgumentException e)
                {
                    e.Message.ShouldBe($"Process with an Id of {pid2} is not running.");
                }
            }
        }
    }
}
