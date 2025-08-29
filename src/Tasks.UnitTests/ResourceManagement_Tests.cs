﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    public class ResourceManagement_Tests
    {
        [Fact]
        public void SingleCoreRequest()
        {
            var messages = AssertBuildSucceededAndGetMessages(@"
                {
                    int grantedCores = BuildEngine9.RequestCores(1337);
                    Log.LogMessage(""Number of cores acquired: "" + grantedCores);
                    BuildEngine9.ReleaseCores(grantedCores);
                }", "<UseCores />");

            var filteredMessages = messages.Where(m => m.Message.StartsWith("Number of cores acquired: ")).ToArray();
            filteredMessages.Length.ShouldBe(1);
            GetTrailingIntegerFromMessage(filteredMessages[0]).ShouldBeGreaterThan(0);
        }

        [Fact]
        public void SingleCoreRequestWithNoRelease()
        {
            var messages = AssertBuildSucceededAndGetMessages(@"
                {
                    int grantedCores = BuildEngine9.RequestCores(1337);
                    Log.LogMessage(""Number of cores acquired: "" + grantedCores);
                    // Note that we're missing a call to ReleaseCores() so we rely on cores being released after the task is finished.
                }", "<UseCores /> <UseCores />");

            var filteredMessages = messages.Where(m => m.Message.StartsWith("Number of cores acquired: ")).ToArray();
            filteredMessages.Length.ShouldBe(2);

            int grantedCores1 = GetTrailingIntegerFromMessage(filteredMessages[0]);
            int grantedCores2 = GetTrailingIntegerFromMessage(filteredMessages[1]);

            // Both tasks were able to get the same number of cores because cores were auto-released.
            grantedCores1.ShouldBeGreaterThan(0);
            grantedCores2.ShouldBe(grantedCores1);
        }

        [Fact]
        public void SingleCoreRequestWithReacquire()
        {
            var messages = AssertBuildSucceededAndGetMessages(@"
                {
                    int grantedCores1 = BuildEngine9.RequestCores(1337);
                    Log.LogMessage(""Number of cores acquired: "" + grantedCores1);

                    BuildEngine9.Yield();
                    // Reacquire releases all cores.
                    BuildEngine9.Reacquire();

                    int grantedCores2 = BuildEngine9.RequestCores(1337);
                    Log.LogMessage(""Number of cores acquired: "" + grantedCores2);
                }", "<UseCores />");

            var filteredMessages = messages.Where(m => m.Message.StartsWith("Number of cores acquired: ")).ToArray();
            filteredMessages.Length.ShouldBe(2);

            int grantedCores1 = GetTrailingIntegerFromMessage(filteredMessages[0]);
            int grantedCores2 = GetTrailingIntegerFromMessage(filteredMessages[1]);

            // Both tasks were able to get the same number of cores because cores were auto-released.
            grantedCores1.ShouldBeGreaterThan(0);
            grantedCores2.ShouldBe(grantedCores1);
        }

        [Fact]
        public void MultipleCoreRequests()
        {
            // Exercise concurrent RequestCores() and ReleaseCores() calls.
            AssertBuildSucceededAndGetMessages(@"
                {
                    const int coresToAcquire = 1337;
                    int acquiredCores = 0;
                    int done = 0;
                    System.Threading.Thread requestThread = new System.Threading.Thread(() =>
                    {
                        for (int i = 0; i &lt; coresToAcquire; i++)
                        {
                            BuildEngine9.RequestCores(1);
                            System.Threading.Interlocked.Increment(ref acquiredCores);
                        }
                        System.Threading.Thread.VolatileWrite(ref done, 1);
                    });
                    System.Threading.Thread releaseThread = new System.Threading.Thread(() =>
                    {
                            while (System.Threading.Thread.VolatileRead(ref done) == 0 || System.Threading.Thread.VolatileRead(ref acquiredCores) > 0)
                            {
                                if (System.Threading.Thread.VolatileRead(ref acquiredCores) > 0)
                                {
                                    BuildEngine9.ReleaseCores(1);
                                    System.Threading.Interlocked.Decrement(ref acquiredCores);
                                }
                                else
                                {
                                    System.Threading.Thread.Yield();
                                }
                            }
                    });

                    // One thread is acquiring cores, the other is releasing them. The releasing thread is running with a lower
                    // priority to increase the chances of contention where all cores are allocated and RequestCores() blocks.
                    requestThread.Start();
                    releaseThread.Priority = System.Threading.ThreadPriority.BelowNormal;
                    releaseThread.Start();

                    requestThread.Join();
                    releaseThread.Join();
                }", "<UseCores />");
        }

        private List<BuildMessageEventArgs> AssertBuildSucceededAndGetMessages(string taskCode, string targetContent)
        {
            string text = $@"
<Project>
  <UsingTask
    TaskName=""UseCores""
    TaskFactory=""RoslynCodeTaskFactory""
    AssemblyFile=""$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll"" >
    <Task>
      <Reference Include=""{typeof(Enumerable).Assembly.Location}"" />
      <Code Type=""Fragment"" Language=""cs"">
        {taskCode}
      </Code>
    </Task>
  </UsingTask>

  <Target Name=""Build"">
        {targetContent}
  </Target>
</Project>";
            using var env = TestEnvironment.Create();

            var projectFile = env.CreateTestProjectWithFiles("test.proj", text);
            var logger = projectFile.BuildProjectExpectSuccess();
            return logger.BuildMessageEvents;
        }

        private int GetTrailingIntegerFromMessage(BuildMessageEventArgs msg)
        {
            string[] messageComponents = msg.Message.Split(' ');
            int.TryParse(messageComponents.Last(), out int trailingInteger).ShouldBeTrue();
            return trailingInteger;
        }
    }
}
