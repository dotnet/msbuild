// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Tests for Target
    /// </summary>
    [TestFixture]
    public class Target_Tests
    {
        #region Common Helpers
        /// <summary>
        /// Basic project content with 1 Target
        /// </summary>
        private const string ProjectContentsOneTarget = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' />
                                </Project>
                            ";

        /// <summary>
        /// Basic project content with 1 Target that contains an Input and an Output
        /// </summary>
        private const string ProjectContentOneTargetWithInputsOutputs = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' 
                                            Outputs='out'
                                            Inputs='in' />
                                </Project>
                            ";

        /// <summary>
        /// Basic project content with 1 Target that contains a Message Task
        /// </summary>
        private const string ProjectContentOneTargetWithTask = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1'>
                                        <Message Text='t1.task1' />
                                    </Target>
                                </Project>
                            ";

        /// <summary>
        /// Basic project content with 3 Targets
        /// </summary>
        private const string ProjectContentsSeveralTargets = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' />
                                    <Target Name='t2' />
                                    <Target Name='t3' />
                                </Project>
                            ";

        /// <summary>
        /// Basic project content with 3 Targets that contain a Message Task
        /// </summary>
        private const string ProjectContentsSeveralTargetsWithTask = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1'>
                                        <Message Text='t1.task1' />
                                    </Target>
                                    <Target Name='t2'>
                                        <Message Text='t2.task1' />
                                    </Target>
                                    <Target Name='t3'>
                                        <Message Text='t3.task1' />
                                    </Target>
                                </Project>
                            ";

        /// <summary>
        /// Basic project content with 3 targets, each that contain an Input, Output, and Condition
        /// </summary>
        private const string ProjectContentSeveralTargetsWithInputsOutputsConditions = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' Condition=""'A' == 'A'"" Outputs='t1.out' Inputs='t1.in' />
                                    <Target Name='t2' Condition=""'true' == 'true'"" Outputs='t2.out' Inputs='t2.in' />
                                    <Target Name='t3' Condition=""'true' != 'false'"" Outputs='t3.out' Inputs='t3.in' />
                                </Project>
                            ";

        /// <summary>
        /// Basic project content with several targets, where depends on targets is used
        /// </summary>
        private const string ProjectContentSeveralTargetsWithDependsOnTargets = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' DependsOnTargets='t2'/>
                                    <Target Name='t2' DependsOnTargets='t3'>
                                        <Message Text='t2.task' />
                                    </Target>
                                    <Target Name='t3' DependsOnTargets='t4'>
                                        <Message Text='t3.task' />
                                    </Target>
                                    <Target Name='t4'>
                                        <Message Text='t4.task' />
                                    </Target>
                                    <Target Name='t5'>
                                        <Message Text='t5.task' />
                                    </Target>
                                </Project>
                            ";

        /// <summary>
        /// Engine that is used through out test class
        /// </summary>
        private Engine engine;

        /// <summary>
        /// Project that is used through out test class
        /// </summary>
        private Project project;

        /// <summary>
        /// MockLogger that is used through out test class
        /// </summary>
        private MockLogger logger;

        /// <summary>
        /// Creates the engine and parent object. Also registers the mock logger.
        /// </summary>
        [SetUp()]
        public void Initialize()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            engine = new Engine();
            engine.DefaultToolsVersion = "4.0";
            project = new Project(engine);
            logger = new MockLogger();
            project.ParentEngine.RegisterLogger(logger);
        }

        /// <summary>
        /// Unloads projects and un-registers logger.
        /// </summary>
        [TearDown()]
        public void Cleanup()
        {
            engine.UnloadProject(project);
            engine.UnloadAllProjects();
            engine.UnregisterAllLoggers();

            ObjectModelHelpers.DeleteTempProjectDirectory();
        }
        #endregion

        #region Inputs/Outputs Tests
        /// <summary>
        /// Tests Target.Inputs/Outputs Get Inputs/Outputs when they've not been set
        /// </summary>
        [Test]
        public void InputsOutputsGetWhenUnset()
        {
            Target t = project.Targets.AddNewTarget("t1");

            Assertion.AssertEquals(String.Empty, t.Inputs);
            Assertion.AssertEquals(String.Empty, t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Get Inputs/Outputs when they've been set to an empty string
        /// </summary>
        [Test]
        public void InputsOutputsGetWhenSetEmptyString()
        {
            Target t = project.Targets.AddNewTarget("t1");
            t.Inputs = String.Empty;
            t.Outputs = String.Empty;

            Assertion.AssertEquals(String.Empty, t.Inputs);
            Assertion.AssertEquals(String.Empty, t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Get Inputs/Outputs when they've been set to null
        /// </summary>
        [Test]
        public void InputsOutputsGetWhenSetNull()
        {
            Target t = project.Targets.AddNewTarget("t1");
            t.Inputs = null;
            t.Outputs = null;

            Assertion.AssertEquals(String.Empty, t.Inputs);
            Assertion.AssertEquals(String.Empty, t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Get Inputs/Outputs when they're set to simple valid string
        /// </summary>
        [Test]
        public void InputsOutputsGetWhenSetToValidString()
        {
            Target t = project.Targets.AddNewTarget("t1");
            t.Inputs = "input";
            t.Outputs = "output";

            Assertion.AssertEquals("input", t.Inputs);
            Assertion.AssertEquals("output", t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Set Inputs/Outputs to string that contains special characters
        /// </summary>
        [Test]
        public void InputsOutputsSetWithSpecialCharacters()
        {
            Target t = project.Targets.AddNewTarget("t1");
            t.Inputs = "%24%40%3b%5c%25";
            t.Outputs = "%24%40%3b%5c%25";

            Assertion.AssertEquals("%24%40%3b%5c%25", t.Inputs);
            Assertion.AssertEquals("%24%40%3b%5c%25", t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Get Inputs/Outputs from a pre-existing Target
        /// </summary>
        [Test]
        public void InputsOutputsGetOfExistingTarget()
        {
            project.LoadXml(ProjectContentOneTargetWithInputsOutputs);
            Target t = GetSpecificTargetFromProject(project, "t1");

            Assertion.AssertEquals("in", t.Inputs);
            Assertion.AssertEquals("out", t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Get Inputs/Outputs from a pre-existing Target that has no Inputs/Outputs
        /// </summary>
        [Test]
        public void InputsOutputsGetOfExistingTargetThatDoesntContainThem()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");

            Assertion.AssertEquals("", t.Inputs);
            Assertion.AssertEquals("", t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Set Inputs/Outputs on a pre-existing Target that has no Inputs/Outputs
        /// </summary>
        [Test]
        public void InputsOutputsSetOnExistingTargetThatDoesntContainThem()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Inputs = "in";
            t.Outputs = "out";

            Assertion.AssertEquals("in", t.Inputs);
            Assertion.AssertEquals("out", t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Set (change) Inputs/Outputs on a pre-existing Target
        /// </summary>
        [Test]
        public void InputsOutputsSetOfExistingTargetThatAlreadyContainThem()
        {
            project.LoadXml(ProjectContentOneTargetWithInputsOutputs);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Inputs = "newin";
            t.Outputs = "newout";

            Assertion.AssertEquals("newin", t.Inputs);
            Assertion.AssertEquals("newout", t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Get from an imported Target
        /// </summary>
        [Test]
        public void InputsOutputsGetFromAnImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "t1");

            Assertion.AssertEquals("in", t.Inputs);
            Assertion.AssertEquals("out", t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Set Inputs on an imported Target
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void InputsOutputsSetInputOnAnImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "t1");
            t.Inputs = "newin";
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Set Outputs on an imported Target
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void InputsOutputsSetOutputOnAnImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "t1");
            t.Outputs = "newout";
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Set, then save to disk and verify
        /// </summary>
        [Test]
        public void InputsOutputsSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentOneTargetWithInputsOutputs);

            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Inputs = "newin";
            t.Outputs = "newout";

            string expectedProjectContents = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' Outputs='newout' Inputs='newin' />
                                </Project>
                            ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Set on an already existing Target that has been Built
        /// </summary>
        [Test]
        public void InputsOutputsSetOnAnAlreadyBuiltTarget()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1' Inputs='in' Outputs='out'>
                            <Exec Command='echo foo'/>
                        </Target>
                    </Project>
                ";

            project.LoadXml(projectContents);
            project.Build("t1");

            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Inputs = "newin";
            t.Outputs = "newout";

            Assertion.AssertEquals("newin", t.Inputs);
            Assertion.AssertEquals("newout", t.Outputs);
        }

        /// <summary>
        /// Tests Target.Inputs/Outputs Set to String where string contains metadata
        /// </summary>
        [Test]
        public void InputsOutputsSetToStringWithMetadata()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <SomeItem Include='a'>
                                <metadata>1</metadata>
                            </SomeItem>
                            <SomeItem Include='b'>
                                <metadata>2</metadata>
                            </SomeItem>
                        </ItemGroup>
                        <Target Name='t1'>
                            <Exec Command='echo @(SomeItem)foo'/>
                        </Target>
                    </Project>
                ";

            project.LoadXml(projectContents);
            project.Build("t1");

            ResetLogger();

            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Inputs = "%(SomeItem.metadata)";
            t.Outputs = @"@(SomeItem->'%(metadata)')";

            ITaskItem[] outputItems = BuildAndGatherOutputs("t1");

            Assertion.AssertEquals(true, logger.FullLog.Contains("afoo"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("bfoo"));
            Assertion.AssertEquals("1", outputItems[0].ToString());
            Assertion.AssertEquals("2", outputItems[1].ToString());
        }
        #endregion

        #region Name Tests
        /// <summary>
        /// Tests Target.Name after Adding a new Target of that newly added Target
        /// </summary>
        [Test]
        public void NameAfterAddingNewTarget()
        {
            Target t = project.Targets.AddNewTarget("t1");

            Assertion.AssertEquals("t1", t.Name);
        }

        /// <summary>
        /// Tests Target.Name of an existing Target
        /// </summary>
        [Test]
        public void NameOfExistingTargetWhenOnlyOneExists()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");

            Assertion.AssertEquals("t1", t.Name);
        }

        /// <summary>
        /// Tests Target.Name of an imported target
        /// </summary>
        [Test]
        public void NameOfAnImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "t1");

            Assertion.AssertEquals("t1", t.Name);
        }

        /// <summary>
        /// Tests Target.Name of a newly added target when existing targets exist
        /// </summary>
        [Test]
        public void NameAfterAddingNewTargetWhenOtherExistingTargetsExist()
        {
            project.LoadXml(ProjectContentsOneTarget);
            project.Targets.AddNewTarget("new");
            Target t = GetSpecificTargetFromProject(project, "new");

            Assertion.AssertEquals("new", t.Name);
        }

        /// <summary>
        /// Tests Target.Name of 1 of many existing Targets
        /// </summary>
        [Test]
        public void NameOfExistingTargetWhenManyExists()
        {
            project.LoadXml(ProjectContentsSeveralTargets);
            Target t = GetSpecificTargetFromProject(project, "t2");

            Assertion.AssertEquals("t2", t.Name);
        }

        /// <summary>
        /// Tests Target.Name Set, then save to disk and verify
        /// </summary>
        [Test]
        public void NameSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentOneTargetWithInputsOutputs);
            Target t = project.Targets.AddNewTarget("tnew");

            string expectedProjectContents = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' Outputs='out' Inputs='in' />
                                    <Target Name='tnew' />
                                </Project>
                            ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }
        #endregion

        #region Condition Tests
        /// <summary>
        /// Tests Target.Condition Get from an existing project target
        /// </summary>
        [Test]
        public void ConditionGetFromExistingProjectTarget()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithInputsOutputsConditions);
            Target t = GetSpecificTargetFromProject(project, "t1");

            Assertion.AssertEquals("'A' == 'A'", t.Condition);
        }

        /// <summary>
        /// Tests Target.Condition Set on an existing project target
        /// </summary>
        [Test]
        public void ConditionSetOnExistingProjectTarget()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithInputsOutputsConditions);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Condition = "'A' == 'B'";

            Assertion.AssertEquals("'A' == 'B'", t.Condition);
        }

        /// <summary>
        /// Tests Target.Condition Set on a newly added target
        /// </summary>
        [Test]
        public void ConditionSetOnNewlyAddedTarget()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithInputsOutputsConditions);
            Target t = project.Targets.AddNewTarget("t4");
            t.Condition = "'true' == 'true'";

            Assertion.AssertEquals("'true' == 'true'", t.Condition);
        }

        /// <summary>
        /// Tests Target.Condition Set, then save to disk and verify
        /// </summary>
        [Test]
        public void ConditionSetSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentOneTargetWithInputsOutputs);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Condition = "'A' == 'B'";

            string expectedProjectContents = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' Outputs='out' Inputs='in' Condition=""'A' == 'B'"" />
                                </Project>
                            ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests Target.Condition Set with special characters
        /// </summary>
        [Test]
        public void ConditionSetWithSpecialCharacters()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithInputsOutputsConditions);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Condition = "'%24%40%3b%5c%25' == '%24%40%3b%5c%25'";

            Assertion.AssertEquals("'%24%40%3b%5c%25' == '%24%40%3b%5c%25'", t.Condition);
        }

        /// <summary>
        /// Tests Target.Condition Set to an empty string
        /// </summary>
        [Test]
        public void ConditionSetToEmptyString()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Condition = String.Empty;

            Assertion.AssertEquals(String.Empty, t.Condition);
        }

        /// <summary>
        /// Tests Target.Condition Set to null
        /// </summary>
        [Test]
        public void ConditionSetToNull()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.Condition = null;

            Assertion.AssertEquals(String.Empty, t.Condition);
        }

        /// <summary>
        /// Tests Target.Condition Get from an imported Target
        /// </summary>
        [Test]
        public void ConditionGetFromAnImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "t1");

            Assertion.AssertEquals("'true' == 'true'", t.Condition);
        }

        /// <summary>
        /// Tests Target.Condition Set on an imported Target
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ConditionSetOnAnImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "t1");
            t.Condition = "'B' == 'B'";
        }
        #endregion

        #region AddNewTask Tests
        /// <summary>
        /// Tests Target.AddNewTask by adding a new task with a simple string name
        /// </summary>
        [Test]
        public void AddNewTaskSimpleTaskNameString()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");

            BuildTask task = t.AddNewTask("Message");
            task.SetParameterValue("Text", "t1.task");

            Assertion.AssertEquals(true, project.Build("t1"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t1.task"));
        }

        /// <summary>
        /// Tests Target.AddNewTask by adding a new task, then save to disk and verify
        /// </summary>
        [Test]
        public void AddNewTaskSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");

            BuildTask task = t.AddNewTask("Message");
            task.SetParameterValue("Text", "t1.task");

            string expectedProjectContents = @"
                                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                                <Target Name='t1'>
                                                    <Message Text='t1.task'/>
                                                </Target>
                                            </Project>
                                        ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests Target.AddNewTask by attempting to add a new task with an Empty String name
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void AddNewTaskWithEmptyTaskname()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");

            t.AddNewTask(String.Empty);
        }

        /// <summary>
        /// Tests Target.AddNewTask by attempting to add a new task with a null name
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddNewTaskWithNullTaskname()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");

            t.AddNewTask(null);
        }

        /// <summary>
        /// Tests Target.AddNewTask by attempting to add a new task to an imported target
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AddNewTaskAttemptToAddToImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "t1");

            t.AddNewTask("Message");
        }

        /// <summary>
        /// Tests Target.AddNewTask by attempting to add a new task with name that contains special characters
        /// </summary>
        [Test]
        [ExpectedException(typeof(XmlException))]
        public void AddNewTaskWithSpecialCharactersInTaskname()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");

            t.AddNewTask("%24%40%3b%5c%25");
        }

        /// <summary>
        /// Tests Target.AddNewTask by adding several new tasks to the same target
        /// </summary>
        [Test]
        public void AddNewTaskSeveralToOneTarget()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");

            BuildTask[] task = new BuildTask[3];
            task[0] = t.AddNewTask("Message");
            task[0].SetParameterValue("Text", "t1.task1");
            task[1] = t.AddNewTask("Message");
            task[1].SetParameterValue("Text", "t1.task2");
            task[2] = t.AddNewTask("Message");
            task[2].SetParameterValue("Text", "t1.task3");

            Assertion.AssertEquals(true, project.Build("t1"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t1.task1"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t1.task2"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t1.task3"));
        }

        /// <summary>
        /// Tests Target.AddNewTask by adding a new task to several different targets
        /// </summary>
        [Test]
        public void AddNewTaskSeveralToDifferentTargets()
        {
            project.LoadXml(ProjectContentsSeveralTargets);

            BuildTask[] task = new BuildTask[3];
            Target[] t = new Target[3];
            t[0] = GetSpecificTargetFromProject(project, "t1");
            task[0] = t[0].AddNewTask("Message");
            task[0].SetParameterValue("Text", "t1.task1");

            t[1] = GetSpecificTargetFromProject(project, "t2");
            task[1] = t[1].AddNewTask("Message");
            task[1].SetParameterValue("Text", "t2.task1");

            t[2] = GetSpecificTargetFromProject(project, "t3");
            task[2] = t[2].AddNewTask("Message");
            task[2].SetParameterValue("Text", "t3.task1");

            Assertion.AssertEquals(true, project.Build(new string[] { "t1", "t2", "t3" }));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t1.task1"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t2.task1"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t3.task1"));
        }

        /// <summary>
        /// Tests Target.AddNewTask by adding a new task to a target that already contains a task
        /// </summary>
        [Test]
        public void AddNewTaskToATargetThatContainsAPreExistingTask()
        {
            project.LoadXml(ProjectContentOneTargetWithTask);
            Target t = GetSpecificTargetFromProject(project, "t1");
            BuildTask task = t.AddNewTask("Message");
            task.SetParameterValue("Text", "t1.task2");

            Assertion.AssertEquals(true, project.Build("t1"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t1.task1"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t1.task2"));
        }
        #endregion

        #region RemoveTask Tests
        /// <summary>
        /// Tests Target.RemoveTask of an existing Task
        /// </summary>
        [Test]
        public void RemoveTaskRemoveExistingTask()
        {
            project.LoadXml(ProjectContentOneTargetWithTask);
            Target t = GetSpecificTargetFromProject(project, "t1");
            BuildTask task = GetSpecificTaskWithinTarget(t, "Message");

            t.RemoveTask(task);

            Assertion.AssertEquals(true, project.Build("t1"));
            Assertion.AssertEquals(false, logger.FullLog.Contains("t1.task1"));
        }

        /// <summary>
        /// Tests Target.RemoveTask of a newly added Task
        /// </summary>
        [Test]
        public void RemoveTaskRemoveNewlyAddedTask()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");
            BuildTask task = t.AddNewTask("Message");
            task.SetParameterValue("Text", "t1.task1");

            t.RemoveTask(task);

            Assertion.AssertEquals(true, project.Build("t1"));
            Assertion.AssertEquals(false, logger.FullLog.Contains("t1.task1"));
        }

        /// <summary>
        /// Tests Target.RemoveTask by attemptying to remove a null task
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RemoveTaskWhereTaskIsNull()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");
            BuildTask task = null;

            t.RemoveTask(task);
        }

        /// <summary>
        /// Tests Target.RemoveTask by attempting to remove a task from another target
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveTaskAttemptWithTaskThatsInAnotherTarget()
        {
            project.LoadXml(ProjectContentsSeveralTargetsWithTask);
            Target t1 = GetSpecificTargetFromProject(project, "t1");
            BuildTask task1 = GetSpecificTaskWithinTarget(t1, "Message");
            Target t2 = GetSpecificTargetFromProject(project, "t2");

            t2.RemoveTask(task1);
        }

        /// <summary>
        /// Tests Target.RemoveTask by attempting to remove a task from an imported target
        /// </summary>
        [Test]  
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveTaskAttemptRemovalOfAnImportedTask()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "t1");
            BuildTask task = GetSpecificTaskWithinTarget(t, "Message");

            t.RemoveTask(task);
        }
        #endregion

        #region DependsOnTargets Tests
        /// <summary>
        /// Tests Target.DependsOnTargets Get from existing target
        /// </summary>
        [Test]
        public void DependsOnTargetGetExisting()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = GetSpecificTargetFromProject(project, "t3");

            Assertion.AssertEquals("t4", t.DependsOnTargets);
        }

        /// <summary>
        /// Tests Target.DependsOnTargets Set from existing target
        /// </summary>
        [Test]
        public void DependsOnTargetSetExisting()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = GetSpecificTargetFromProject(project, "t3");

            t.DependsOnTargets = "t1";

            Assertion.AssertEquals("t1", t.DependsOnTargets);
        }

        /// <summary>
        /// Tests Target.DependsOnTargets Saving to Disk after setting
        /// </summary>
        [Test]
        public void DependsOnTargetSaveAfterSet()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = GetSpecificTargetFromProject(project, "t3");
            t.DependsOnTargets = "t1";

            string expectedProjectContents = @"
                                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                    <Target Name='t1' DependsOnTargets='t2'/>
                                    <Target Name='t2' DependsOnTargets='t3'>
                                        <Message Text='t2.task' />
                                    </Target>
                                    <Target Name='t3' DependsOnTargets='t1'>
                                        <Message Text='t3.task' />
                                    </Target>
                                    <Target Name='t4'>
                                        <Message Text='t4.task' />
                                    </Target>
                                    <Target Name='t5'>
                                        <Message Text='t5.task' />
                                    </Target>
                                </Project>
                            ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests Target.DependsOnTargets Building after Set
        /// </summary>
        [Test]
        public void DependsOnTargetSetThenBuild()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = GetSpecificTargetFromProject(project, "t2");
            t.DependsOnTargets = "t4";

            Assertion.AssertEquals(true, project.Build("t1"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t2.task"));
            Assertion.AssertEquals(false, logger.FullLog.Contains("t3.task"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t4.task"));
            Assertion.AssertEquals(false, logger.FullLog.Contains("t5.task"));
        }

        /// <summary>
        /// Tests Target.DependsOnTargets Setting to a non-existing Target and then Build
        /// </summary>
        [Test]
        public void DependsOnTargetSetToNonExistingTargetBuild()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.DependsOnTargets = "tNot";

            Assertion.AssertEquals(false, project.Build("t1"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("MSB4057"));
        }

        /// <summary>
        /// Tests Target.DependsOnTargets Get of a newly created target
        /// </summary>
        [Test]
        public void DependsOnTargetGetNewlyCreated()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = project.Targets.AddNewTarget("tNew");

            Assertion.AssertEquals(String.Empty, t.DependsOnTargets);
        }

        /// <summary>
        /// Tests Target.DependsOnTargets Set of a newly created target
        /// </summary>
        [Test]
        public void DependsOnTargetSetNewlyCreated()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = project.Targets.AddNewTarget("tNew");
            t.DependsOnTargets = "t5";

            Assertion.AssertEquals("t5", t.DependsOnTargets);
            Assertion.AssertEquals(true, project.Build("tNew"));
            Assertion.AssertEquals(true, logger.FullLog.Contains("t5.task"));
        }

        /// <summary>
        /// Tests Target.DependsOnTarget Set to an empty string
        /// </summary>
        [Test]
        public void DependsOnTargetSetToEmptyString()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.DependsOnTargets = String.Empty;

            Assertion.AssertEquals(String.Empty, t.DependsOnTargets);
        }

        /// <summary>
        /// Tests Target.DependsOnTarget Set to null
        /// </summary>
        [Test]
        public void DependsOnTargetSetToNull()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.DependsOnTargets = null;

            Assertion.AssertEquals(String.Empty, t.DependsOnTargets);
        }

        /// <summary>
        /// Tests Target.DependsOnTarget Set to special characters
        /// </summary>
        [Test]
        public void DependsOnTargetSetToSpecialCharacters()
        {
            project.LoadXml(ProjectContentSeveralTargetsWithDependsOnTargets);
            Target t = GetSpecificTargetFromProject(project, "t1");
            t.DependsOnTargets = "%24%40%3b%5c%25";

            Assertion.AssertEquals("%24%40%3b%5c%25", t.DependsOnTargets);
        }

        /// <summary>
        /// Tests Target.DependsOnTarget Get of an imported Target
        /// </summary>
        [Test]
        public void DependsOnTargetGetFromImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "tImported");

            Assertion.AssertEquals("t1", t.DependsOnTargets);
        }

        /// <summary>
        /// Tests Target.DependsOnTarget Set on an imported Target
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void DependsOnTargetSetFromImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "tImported");

            t.DependsOnTargets = "t";
        }
        #endregion

        #region IsImported Tests
        /// <summary>
        /// Tests Target.IsImported of an imported target
        /// </summary>
        [Test]
        public void IsImportedOfAnImportedTarget()
        {
            Project p = GetProjectWithOneImportProject();
            Target t = GetSpecificTargetFromProject(p, "tImported");

            Assertion.AssertEquals(true, t.IsImported);
        }

        /// <summary>
        /// Tests Target.IsImported of an target that is not imported
        /// </summary>
        [Test]
        public void IsImportedOfANonImportedTarget()
        {
            project.LoadXml(ProjectContentsOneTarget);
            Target t = GetSpecificTargetFromProject(project, "t1");

            Assertion.AssertEquals(false, t.IsImported);
        }

        /// <summary>
        /// Tests Target.IsImported where the Main project and Imported project both
        ///     contain target t1, but the imported target t1 comes after the main
        ///     target t1.
        /// </summary>
        [Test]
        public void IsImportedTargetOfSameNameInBothWhereImportedComesLast()
        {
            string importProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1'>
                            <Message Text='imported.t1.task' />
                        </Target>
                    </Project>
                ";

            string projectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1'>
                            <Message Text='main.t1.task' />
                        </Target>
                        <Import Project='import.proj' />
                    </Project>
                ";

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", importProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", projectContents);
            Project p = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);

            Target t = GetSpecificTargetFromProject(p, "t1", true);

            Assertion.AssertEquals(true, t.IsImported);
        }

        /// <summary>
        /// Tests Target.IsImported where the Main project and Imported project both
        ///     contain target t1, but the imported target t1 comes before the main
        ///     target t1.
        /// </summary>
        [Test]
        public void IsImportedTargetOfSameNameInBothWhereImportedComesFirst()
        {
            string importProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1'>
                            <Message Text='imported.t1.task' />
                        </Target>
                    </Project>
                ";

            string projectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Import Project='import.proj' />
                        <Target Name='t1'>
                            <Message Text='main.t1.task' />
                        </Target>
                    </Project>
                ";

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", importProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", projectContents);
            Project p = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);

            Target t = GetSpecificTargetFromProject(p, "t1", true);

            Assertion.AssertEquals(false, t.IsImported);
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Gets you a specific BuildTask from a specific Target
        /// </summary>
        /// <param name="t">Target that contains your BuildTask</param>
        /// <param name="taskNameYouWant">The name of the BuildTask you want</param>
        /// <returns>The BuildTask you requested</returns>
        private static BuildTask GetSpecificTaskWithinTarget(Target t, string taskNameYouWant)
        {
            foreach (BuildTask task in t)
            {
                if (String.Equals(task.Name, taskNameYouWant, StringComparison.OrdinalIgnoreCase))
                {
                    return task;
                }
            }

            return null;
        }

        /// <summary>
        /// Saves a given Project to disk and compares what's saved to disk with expected contents.  Assertion handled within
        ///     ObjectModelHelpers.CompareProjectContents.
        /// </summary>
        /// <param name="p">Project to save</param>
        /// <param name="expectedProjectContents">The Project content that you expect</param>
        private static void SaveProjectToDiskAndCompareAgainstExpectedContents(Project p, string expectedProjectContents)
        {
            string savePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "p.proj");
            p.Save(savePath);

            Engine e = new Engine();
            Project savedProject = new Project(e);
            savedProject.Load(savePath);

            ObjectModelHelpers.CompareProjectContents(savedProject, expectedProjectContents);
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Helper Method to create a Main project that imports 1 other project.  Each project contains a Target that
        ///     also contains a BuildTask.
        /// </summary>
        /// <returns>Project</returns>
        private static Project GetProjectWithOneImportProject()
        {
            string importProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Target Name='t1' Condition=""'true' == 'true'"" Inputs='in' Outputs='out'>
                            <Message Text='imported.t1.task1' />
                        </Target>
                        <Target Name='tImported' DependsOnTargets='t1'/>
                    </Project>
                ";

            string projectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Import Project='import1.proj' />
                        <Target Name='Build'>
                            <Message Text='Build.task' />
                        </Target>
                    </Project>
                ";

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.proj", importProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", projectContents);
            Project p = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);

            return p;
        }

        /// <summary>
        /// Gets a specified Target from a Project
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="nameOfTarget">Target name of the Target you want</param>
        /// <returns>Target requested.  null if specific target isn't found</returns>
        private Target GetSpecificTargetFromProject(Project p, string nameOfTarget)
        {
            return GetSpecificTargetFromProject(p, nameOfTarget, false);
        }

        /// <summary>
        /// Gets a specified Target from a Project
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="nameOfTarget">Target name of the Target you want</param>
        /// <param name="lastInstance">If you want the last instance of a target set to true</param>
        /// <returns>Target requested.  null if specific target isn't found</returns>
        private Target GetSpecificTargetFromProject(Project p, string nameOfTarget, bool lastInstance)
        {
            Target target = null;
            foreach (Target t in p.Targets)
            {
                if (String.Equals(t.Name, nameOfTarget, StringComparison.OrdinalIgnoreCase))
                {
                    if (!lastInstance)
                    {
                        return t;
                    }
                    else
                    {
                        target = t;
                    }
                }
            }

            return target;
        }

        /// <summary>
        /// Builds the specified target and return the outputs
        /// </summary>
        /// <param name="targetToBuild">The Target that you want to build, like 'Build', 'Rebuild', etc</param>
        /// <returns>outputs</returns>
        private ITaskItem[] BuildAndGatherOutputs(string targetToBuild)
        {
            Dictionary<object, object> outputs = new Dictionary<object, object>();
            project.Build(new string[] { targetToBuild }, outputs);
            ITaskItem[] outputItems = (ITaskItem[])outputs[targetToBuild];
            return outputItems;
        }

        /// <summary>
        /// Un-registers the existing logger and registers a new copy.
        /// We will use this when we do multiple builds so that we can safely 
        /// assert on log messages for that particular build.
        /// </summary>
        private void ResetLogger()
        {
            engine.UnregisterAllLoggers();
            logger = new MockLogger();
            project.ParentEngine.RegisterLogger(logger);
        }
        #endregion
    }
}
