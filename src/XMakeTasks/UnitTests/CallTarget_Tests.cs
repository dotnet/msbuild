// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class CallTarget_Tests
    {
        [TestInitialize]
        public void SetUp()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        [TestCleanup]
        public void TearDown()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Simple test of the CallTarget task.
        /// </summary>
        [TestMethod]
        public void Simple()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                <Project DefaultTargets=`a` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`a` >
	                    <CallTarget Targets=`b` />
                    </Target>
                    <Target Name=`b` >
	                    <Message Text=`Inside B` />
                    </Target>
                </Project>
                ");

            logger.AssertLogContains("Inside B");
        }

        /// <summary>
        /// Simple test of the CallTarget task, where one of the middle targets invoked fails.
        /// </summary>
        [TestMethod]
        public void FailedTargets()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(@"

                <Project DefaultTargets=`build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`build` >
	                    <CallTarget Targets=`a; b; c` />
                    </Target>
                    <Target Name=`a` >
	                    <Message Text=`Inside A` />
                    </Target>
                    <Target Name=`b` >
	                    <Error Text=`Inside B` />
                    </Target>
                    <Target Name=`c` >
	                    <Message Text=`Inside C` />
                    </Target>
                </Project>
                ");

            logger.AssertLogContains("Inside A");
            logger.AssertLogContains("Inside B");

            // Target C should not have been run.
            logger.AssertLogDoesntContain("Inside C");
        }

        /// <summary>
        /// Test the CallTarget task, where one of the middle targets invoked fails, but we
        /// specified RunEachTargetSeparately, so all the targets should have been run anyway.
        /// </summary>
        [TestMethod]
        public void FailedTargetsRunSeparately()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(@"

                <Project DefaultTargets=`build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`build` >
	                    <CallTarget Targets=`a; b; c` RunEachTargetSeparately=`true` />
                    </Target>
                    <Target Name=`a` >
	                    <Message Text=`Inside A` />
                    </Target>
                    <Target Name=`b` >
	                    <Error Text=`Inside B` />
                    </Target>
                    <Target Name=`c` >
	                    <Message Text=`Inside C` />
                    </Target>
                </Project>
                ");

            // All three targets should have been run.
            logger.AssertLogContains("Inside A");
            logger.AssertLogContains("Inside B");
            logger.AssertLogContains("Inside C");
        }

        /// <summary>
        /// Test the CallTarget task, where we don't pass in any targets.  This is expected
        /// to succeed, so that callers of the task don't have to add a Condition to ensure
        /// that the list of targets is non-empty.
        /// </summary>
        [TestMethod]
        public void NoTargets()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                <Project DefaultTargets=`build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`build` >
	                    <CallTarget Targets=` @(empty) ` />
                    </Target>
                </Project>
                ");
        }

        /// <summary>
        /// Test the CallTarget task and capture the outputs of the invoked targets.
        /// </summary>
        [TestMethod]
        public void CaptureTargetOutputs()
        {
            Project project = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project DefaultTargets=`build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <Target Name=`build` >

                        <CallTarget Targets=` a; b; c ` >
                            <Output ItemName=`myfancytargetoutputs` TaskParameter=`TargetOutputs`/>
                        </CallTarget>

                    </Target>
                    <!-- include some nice characters that need escaping -->
                    <Target Name=`a` Outputs=`a.t!@#$%^xt`>
	                    <Message Text=`Inside A` />
                    </Target>
                    <Target Name=`b` Outputs=`b.txt`>
	                    <Message Text=`Inside B` />
                    </Target>
                    <Target Name=`c` Outputs=`c.txt`>
	                    <Message Text=`Inside C` />
                    </Target>
                </Project>

                ");

            ProjectInstance instance = project.CreateProjectInstance();
            bool success = instance.Build();
            Assert.IsTrue(success, "Build failed.  See Standard Out tab for details");

            IEnumerable<ProjectItemInstance> targetOutputs = instance.GetItems("myfancytargetoutputs");

            // Convert to a list of TaskItems for easier verification.
            List<ITaskItem> targetOutputsTaskItems = new List<ITaskItem>();
            foreach (ProjectItemInstance item in targetOutputs)
            {
                targetOutputsTaskItems.Add(new TaskItem(item.EvaluatedInclude));
            }

            // Order independent verification of the right set of items.
            ObjectModelHelpers.AssertItemsMatch(@"
                c.txt
                b.txt
                a.t!@#$%^xt
                ",
                targetOutputsTaskItems.ToArray(), false /* ignore the order */);
        }

        [TestMethod]
        public void CaptureTargetOutputsRunningEachTargetSeparately()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                <Project DefaultTargets = `CallT` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
	                <Target Name = `a` Outputs = `a.txt`/>
	                <Target Name = `b` Outputs = `b.txt`/>
	                <Target Name = `c` Outputs = `c.txt`/>
	                <Target Name = `CallT`>
		                <CallTarget
			                Targets = `a;b;c`
			                RunEachTargetSeparately = `true`>
			                <Output TaskParameter= `TargetOutputs` ItemName = `TaskOut`/>
		                </CallTarget>
		                <Message Text = `CallTarget Outputs: @(TaskOut)`/>
	                </Target>
                </Project>
                ");

            // All three targets should have been run.
            logger.AssertLogContains("CallTarget Outputs: a.txt;b.txt;c.txt");
        }
    }
}
