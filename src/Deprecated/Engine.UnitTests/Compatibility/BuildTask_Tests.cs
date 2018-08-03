// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Test Fixture Class for the v9 Object Model Public Interface Compatibility Tests for the BuildTask Class.
    /// </summary>
    [TestFixture]
    public sealed class BuildTask_Tests
    {
        #region Common Helpers
        /// <summary>
        /// Basic Project Contents with a Target 't' that contains 1 BuildTask 'Task' where
        ///     the BuildTask contains no parameters.
        /// </summary>
        private string ProjectContentsWithOneTask = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task />
                    </Target>
                </Project>
                ");

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

        #region Condition Tests
        /// <summary>
        /// Tests BuildTask.Condition Get simple/basic case
        /// </summary>
        [Test]
        public void ConditionGetSimple()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task Condition=""'a' == 'b'"" >
                        </Task>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            Assertion.AssertEquals("'a' == 'b'", task.Condition);
        }

        /// <summary>
        /// Tests BuildTask.Condition Get when Condition is an empty string
        /// </summary>
        [Test]
        public void ConditionGetEmptyString()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task Condition="""" >
                        </Task>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            Assertion.AssertEquals(String.Empty, task.Condition);
        }

        /// <summary>
        /// Tests BuildTask.Condition Get when condition contains special characters
        /// </summary>
        [Test]
        public void ConditionGetSpecialCharacters()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task Condition=""%24%40%3b%5c%25"" >
                        </Task>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            Assertion.AssertEquals("%24%40%3b%5c%25", task.Condition);
        }

        /// <summary>
        /// Tests BuildTask.Condition Get from an imported project
        /// </summary>
        [Test]
        public void ConditionGetFromImportedProject()
        {
            string importProjectContents = ObjectModelHelpers.CleanupFileContents(@" 
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t2' >
                        <t2.Task Condition=""'a' == 'b'"" >
                        </t2.Task>
                    </Target>
                    </Project>
                ");

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, null);
            BuildTask task = GetSpecificBuildTask(p, "t2", "t2.Task");

            Assertion.AssertEquals("'a' == 'b'", task.Condition);
        }

        /// <summary>
        /// Tests BuildTask.Condition Set when no previous exists
        /// </summary>
        [Test]
        public void ConditionSetWhenNoPreviousConditionExists()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.Condition = "'t' == 'f'";

            Assertion.AssertEquals("'t' == 'f'", task.Condition);
        }

        /// <summary>
        /// Tests BuildTask.Condition Set when an existing condition exists, changing the condition
        /// </summary>
        [Test]
        public void ConditionSetOverExistingCondition()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task Condition=""'a' == 'b'"" >
                        </Task>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.Condition = "'c' == 'd'";

            Assertion.AssertEquals("'c' == 'd'", task.Condition);
        }

        /// <summary>
        /// Tests BuildTask.Condition Set to an empty string
        /// </summary>
        [Test]
        public void ConditionSetToEmtpyString()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.Condition = String.Empty;

            Assertion.AssertEquals(String.Empty, task.Condition);
        }

        /// <summary>
        /// Tests BuildTask.Condition Set to null
        /// </summary>
        [Test]
        public void ConditionSetToNull()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.Condition = null;

            Assertion.AssertEquals(String.Empty, task.Condition);
        }

        /// <summary>
        /// Tests BuildTask.Condition Set to Special Characters
        /// </summary>
        [Test]
        public void ConditionSetToSpecialCharacters()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.Condition = "%24%40%3b%5c%25";

            Assertion.AssertEquals("%24%40%3b%5c%25", task.Condition);
        }

        /// <summary>
        /// Tests BuildTask.Condition Set on an Imported Project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ConditionSetOnImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildTask task = GetSpecificBuildTask(p, "t3", "t3.Task3");

            task.Condition = "true";
        }

        /// <summary>
        /// Tests BuildTask.Condition Set, save to disk and verify
        /// </summary>
        [Test]
        public void ConditionSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.Condition = "'a' == 'b'";

            string expectedProjectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Task Condition=""'a' == 'b'"" />
                        </Target>
                    </Project>
                    ");

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }
        #endregion

        #region ContinueOnError Tests
        /// <summary>
        /// Tests BuildTask.ContinueOnError Get for all cases that would return true
        /// </summary>
        [Test]
        public void ContinueOnErrorGetTrue()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task1 ContinueOnError='true' />
                        <Task2 ContinueOnError='True' />
                        <Task3 ContinueOnError='TRUE' />
                        <Task4 ContinueOnError='on' />
                        <Task5 ContinueOnError='yes' />
                        <Task6 ContinueOnError='!false' />
                        <Task7 ContinueOnError='!off' />
                        <Task8 ContinueOnError='!no' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            List<bool> continueOnErrorResults = GetListOfContinueOnErrorResultsFromSpecificProject(project);

            Assertion.AssertEquals(true, continueOnErrorResults.TrueForAll(delegate(bool b) { return b; }));
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Get for all cases that would return false
        /// </summary>
        [Test]
        public void ContinueOnErrorGetFalse()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task1 ContinueOnError='false' />
                        <Task2 ContinueOnError='False' />
                        <Task3 ContinueOnError='FALSE' />
                        <Task4 ContinueOnError='off' />
                        <Task5 ContinueOnError='no' />
                        <Task6 ContinueOnError='!true' />
                        <Task7 ContinueOnError='!on' />
                        <Task8 ContinueOnError='!yes' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            List<bool> continueOnErrorResults = GetListOfContinueOnErrorResultsFromSpecificProject(project);

            Assertion.AssertEquals(true, continueOnErrorResults.TrueForAll(delegate(bool b) { return !b; }));
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Get when value is an empty string
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ContinueOnErrorGetEmptyString()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task ContinueOnError='' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.ContinueOnError.ToString();
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Get when value is something that won't return true or false - invalid
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ContinueOnErrorGetInvalid()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task ContinueOnError='a' />
                    </Target>
                </Project>
                "));

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.ContinueOnError.ToString();
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Get of a BuildTask from an imported Project
        /// </summary>
        [Test]
        public void ContinueOnErrorGetFromImportedProject()
        {
            string importProjectContents = ObjectModelHelpers.CleanupFileContents(@" 
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t2' >
                            <t2.Task ContinueOnError='true' />
                        </Target>
                    </Project>
                ");

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, null);
            BuildTask task = GetSpecificBuildTask(p, "t2", "t2.Task");

            Assertion.AssertEquals(true, task.ContinueOnError);
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Set when no previous ContinueOnError value exists
        /// </summary>
        [Test]
        public void ContinueOnErrorSetWhenNoContinueOnErrorExists()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");
            task.ContinueOnError = true;

            Assertion.AssertEquals(true, task.ContinueOnError);
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Set when a ContinueOnError value exists (basically changing from true to false)
        /// </summary>
        [Test]
        public void ContinueOnErrorSetWhenContinueOnErrorExists()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task ContinueOnError='true' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");
            task.ContinueOnError = false;

            Assertion.AssertEquals(false, task.ContinueOnError);
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Set to true
        /// </summary>
        [Test]
        public void ContinueOnErrorSetToTrue()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");
            task.ContinueOnError = true;

            Assertion.AssertEquals(true, task.ContinueOnError);
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Set to false
        /// </summary>
        [Test]
        public void ContinueOnErrorSetToFalse()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");
            task.ContinueOnError = false;

            Assertion.AssertEquals(false, task.ContinueOnError);
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Set on an imported project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ContinueOnErrorSetOnImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildTask task = GetSpecificBuildTask(p, "t2", "t2.Task2");

            task.ContinueOnError = true;
        }

        /// <summary>
        /// Tests BuildTask.ContinueOnError Set, then save to disk and verify
        /// </summary>
        [Test]
        public void ContinueOnErrorSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.ContinueOnError = true;

            string expectedProjectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Task ContinueOnError='true'/>
                        </Target>
                    </Project>
                    ");

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }
        #endregion

        #region HostObject Tests
        /// <summary>
        /// Tests BuildTask.HostObject simple set and get with only one BuildTask
        /// </summary>
        [Test]
        public void HostObjectSetGetOneTask()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                         <Target Name='t'>
                             <Message Text='t.message' />
                         </Target>
                    </Project>
                    ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Message");

            MockHostObject hostObject = new MockHostObject();
            task.HostObject = hostObject;

            Assertion.AssertSame(task.HostObject.ToString(), hostObject.ToString());
        }

        /// <summary>
        /// Tests BuildTask.HostObject Set/Get with several BuildTasks
        /// </summary>
        [Test]
        public void HostObjectSetGetMultipleTasks()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                         <Target Name='t'>
                             <Message Text='t.message' />
                             <MyTask />
                         </Target>
                    </Project>
                    ");

            project.LoadXml(projectContents);
            BuildTask task1 = GetSpecificBuildTask(project, "t", "Message");
            BuildTask task2 = GetSpecificBuildTask(project, "t", "MyTask");

            MockHostObject hostObject1 = new MockHostObject();
            MockHostObject hostObject2 = new MockHostObject();

            task1.HostObject = hostObject1;
            task2.HostObject = hostObject2;

            Assertion.AssertSame(task1.HostObject.ToString(), hostObject1.ToString());
            Assertion.AssertSame(task2.HostObject.ToString(), hostObject2.ToString());
        }

        /// <summary>
        /// Tests BuildTask.HostObject Get before the Object Reference is set to an instance of an object
        /// </summary>
        [Test]
        [ExpectedException(typeof(NullReferenceException))]
        public void HostObjectGetBeforeObjectReferenceSet()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                         <Target Name='t'>
                             <Message Text='t.message' />
                         </Target>
                    </Project>
                    ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Message");

            task.HostObject.ToString();
        }
        #endregion

        #region Name Tests
        /// <summary>
        /// Tests BuildTask.Name get when only one BuildTask exists
        /// </summary>
        [Test]
        public void NameWithOneBuildTask()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task parameter='value' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            Assertion.AssertEquals("Task", task.Name);
        }

        /// <summary>
        /// Tests BuildTask.Name get when several BuildTasks exist within the same target
        /// </summary>
        [Test]
        public void NameWithSeveralBuildTasksInSameTarget()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task1 parameter='value' />
                        <Task2 parameter='value' />
                        <Task3 parameter='value' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);

            Assertion.AssertEquals("Task1", GetSpecificBuildTask(project, "t", "Task1").Name);
            Assertion.AssertEquals("Task2", GetSpecificBuildTask(project, "t", "Task2").Name);
            Assertion.AssertEquals("Task3", GetSpecificBuildTask(project, "t", "Task3").Name);
        }

        /// <summary>
        /// Tests BuildTask.Name get when several BuildTasks exist within different targets
        /// </summary>
        [Test]
        public void NameWithSeveralBuildTasksInDifferentTargets()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t1' >
                        <t1.Task1 parameter='value' />
                    </Target>
                    <Target Name='t2' >
                        <t2.Task1 parameter='value' />
                    </Target>
                    <Target Name='t3' >
                        <t3.Task2 parameter='value' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);

            Assertion.AssertEquals("t1.Task1", GetSpecificBuildTask(project, "t1", "t1.Task1").Name);
            Assertion.AssertEquals("t2.Task1", GetSpecificBuildTask(project, "t2", "t2.Task1").Name);
            Assertion.AssertEquals("t3.Task2", GetSpecificBuildTask(project, "t3", "t3.Task2").Name);
        }

        /// <summary>
        /// Tests BuildTask.Name get when BuildTask comes from an imported Project
        /// </summary>
        [Test]
        public void NameOfBuildTaskFromImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);

            Assertion.AssertEquals("t3.Task3", GetSpecificBuildTask(p, "t3", "t3.Task3").Name);
        }

        /// <summary>
        /// Tests BuildTask.Name get when BuildTask was just created
        /// </summary>
        [Test]
        public void NameOfBuildTaskOfANewlyCreatedBuildTask()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                                <Project xmlns='msbuildnamespace'>
                                    <Target Name='t1' />
                                </Project>
                            ");
            project.LoadXml(projectContents);
            Target t = GetSpecificTargetFromProject(project, "t1", false);
            BuildTask task = t.AddNewTask("Task");
            task.SetParameterValue("parameter", "value");

            Assertion.AssertEquals("Task", GetSpecificBuildTask(project, "t1", "Task").Name);
        }
        #endregion

        #region Type Tests
        /// <summary>
        /// Tests BuildTask.Type when Task class exists (Message)
        /// </summary>
        [Test]
        public void TypeTaskExists()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                         <Target Name='t'>
                             <Message Text='t.message' />
                         </Target>
                    </Project>
                    ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Message");

            Assertion.AssertEquals(0, String.Compare(task.Type.ToString(), "Microsoft.Build.Tasks.Message"));
        }

        /// <summary>
        /// Tests BuildTask.Type when Task class doesn't exists (MyFooTask)
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TypeTaskNotExists()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                         <Target Name='t'>
                             <MyFooTask />
                         </Target>
                    </Project>
                    ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "MyFooTask");

            Type t = task.Type;
        }

        /// <summary>
        /// Tests BuildTask.Type when BuildTask is null
        /// </summary>
        [Test]
        [ExpectedException(typeof(NullReferenceException))]
        public void TypeTaskIsNull()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = null;

            Type t = task.Type;
        }
        #endregion

        #region AddOutputItem/AddOutputProperty Tests
        /// <summary>
        /// Tests BuildTask.AddOutputItem/AddOutputProperty by adding a new OutputItem/OutputProperty when none exist
        /// </summary>
        [Test]
        public void AddOutputItemPropertyWhenNoOutputItemsPropertyExist()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.AddOutputItem("ip", "in");
            task.AddOutputProperty("pp", "pn");

            string expectedProjectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Task>
                                <Output TaskParameter='ip' ItemName='in' />
                                <Output TaskParameter='pp' PropertyName='pn' />
                            </Task>
                        </Target>
                    </Project>
                    ");

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildTask.AddOutputItem/AddOutputProperty by adding a new OutputItem/OutputProperty when several exist
        /// </summary>
        [Test]
        public void AddOutputItemPropertyWhenOutputItemsPropertiesExist()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task>
                            <Output TaskParameter='ip1' ItemName='in1' />
                            <Output TaskParameter='ip2' ItemName='in2' />
                            <Output TaskParameter='ip3' ItemName='in3' />
                            <Output TaskParameter='pp1' PropertyName='pn1' />
                            <Output TaskParameter='pp2' PropertyName='pn2' />
                            <Output TaskParameter='pp3' PropertyName='pn3' />
                        </Task>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.AddOutputItem("ip4", "in4");
            task.AddOutputProperty("pp4", "pn4");

            string expectedProjectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Task>
                                <Output TaskParameter='ip1' ItemName='in1' />
                                <Output TaskParameter='ip2' ItemName='in2' />
                                <Output TaskParameter='ip3' ItemName='in3' />
                                <Output TaskParameter='pp1' PropertyName='pn1' />
                                <Output TaskParameter='pp2' PropertyName='pn2' />
                                <Output TaskParameter='pp3' PropertyName='pn3' />
                                <Output TaskParameter='ip4' ItemName='in4' />
                                <Output TaskParameter='pp4' PropertyName='pn4' />
                            </Task>
                        </Target>
                    </Project>
                    ");

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildTask.AddOutputItem/AddOutputProperty by adding a new OutputItem/OutputProperty
        ///     with the same values as an existing OutputItem/OutputProperty
        /// </summary>
        [Test]
        public void AddOutputItemPropertyWithSameValuesAsExistingOutputItemProperty()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task>
                            <Output TaskParameter='ip' ItemName='in' />
                            <Output TaskParameter='pp' PropertyName='pn' />
                        </Task>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.AddOutputItem("ip", "in");
            task.AddOutputProperty("pp", "pn");

            string expectedProjectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Task>
                            <Output TaskParameter='ip' ItemName='in' />
                            <Output TaskParameter='pp' PropertyName='pn' />
                            <Output TaskParameter='ip' ItemName='in' />
                            <Output TaskParameter='pp' PropertyName='pn' />
                            </Task>
                        </Target>
                    </Project>
                    ");

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildTask.AddOutputItem/AddOutputProperty by adding Empty Strings for the taskParameter and itemName/propertyName
        /// </summary>
        [Test]
        public void AddOutputItemPropertyWithEmptyStrings()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.AddOutputItem(String.Empty, String.Empty);
            task.AddOutputProperty(String.Empty, String.Empty);

            string expectedProjectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Task>
                                <Output TaskParameter='' ItemName='' />
                                <Output TaskParameter='' PropertyName='' />
                            </Task>
                        </Target>
                    </Project>
                    ");

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildTask.AddOutputItem/AddOutputProperty by adding nulls for the taskParameter and itemName/propertyName
        /// </summary>
        [Test]
        public void AddOutputItemPropertyWithNulls()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.AddOutputItem(null, null);
            task.AddOutputProperty(null, null);

            string expectedProjectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Task>
                                <Output TaskParameter='' ItemName='' />
                                <Output TaskParameter='' PropertyName='' />
                            </Task>
                        </Target>
                    </Project>
                    ");

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildTask.AddOutputItem/AddOutputProperty by passing in Special characters into the taskParameter and itemName/propertyName
        /// </summary>
        [Test]
        public void AddOutputItemPropertyWithSpecialCharacters()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.AddOutputItem("%24%40%3b%5c%25", "%24%40%3b%5c%25");
            task.AddOutputProperty("%24%40%3b%5c%25", "%24%40%3b%5c%25");

            string expectedProjectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Task>
                                <Output TaskParameter='%24%40%3b%5c%25' ItemName='%24%40%3b%5c%25' />
                                <Output TaskParameter='%24%40%3b%5c%25' PropertyName='%24%40%3b%5c%25' />
                            </Task>
                        </Target>
                    </Project>
                    ");

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildTask.AddOutputItem by attempting to add an OutputItem to an imported Project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AddOutputItemToImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildTask task = GetSpecificBuildTask(p, "t2", "t2.Task2");

            task.AddOutputItem("p", "n");
        }

        /// <summary>
        /// Tests BuildTask.AddOutputProperty by attempting to add an OutputProperty to an imported Project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AddOutputPropertyToImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildTask task = GetSpecificBuildTask(p, "t2", "t2.Task2");

            task.AddOutputProperty("p", "n");
        }
        #endregion

        #region Execute Tests
        /// <summary>
        /// Tests BuildTask.Execute basic case where expected to be true
        /// </summary>
        [Test]
        public void ExecuteExpectedTrue()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                         <Target Name='t1'>
                             <Message Text='t1.message' />
                         </Target>
                         <Target Name='t2'>
                             <Message Text='t2.message' />
                         </Target>
                    </Project>
                    ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t2", "Message");

            Assertion.AssertEquals(0, String.Compare(task.Type.ToString(), "Microsoft.Build.Tasks.Message", StringComparison.OrdinalIgnoreCase));

            Assertion.AssertEquals(true, task.Execute());
            Assertion.AssertEquals(true, logger.FullLog.Contains("t2.message"));
            Assertion.AssertEquals(false, logger.FullLog.Contains("t1.message"));
        }

        /// <summary>
        /// Tests BuildTask.Execute where the BuildTask is null
        /// </summary>
        [Test]
        [ExpectedException(typeof(NullReferenceException))]
        public void ExecuteOnNullBuildTask()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = null;

            task.Execute();
        }

        /// <summary>
        /// Tests BuildTask.Execute where the BuildTask doesn't do anything
        /// </summary>
        [Test]
        public void ExecuteOnTaskThatDoesNothing()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            Assertion.AssertEquals(false, task.Execute());
            Assertion.AssertEquals(true, logger.FullLog.Contains("MSB4036"));
        }

        /// <summary>
        /// Tests BuildTask.Execute where Task comes from an Imported Project
        /// </summary>
        [Test]
        public void ExecuteFromImportedProject()
        {
            string importProjectContents = ObjectModelHelpers.CleanupFileContents(@" 
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Message Text='t.message' />
                        </Target>
                    </Project>
                ");

            string parentProjectContents = ObjectModelHelpers.CleanupFileContents(@" 
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                        <Import Project='import.proj' />
                    </Project>
                ");

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, parentProjectContents);
            BuildTask task = GetSpecificBuildTask(p, "t", "Message");

            Assertion.AssertEquals(true, task.Execute());
        }
        #endregion
        
        #region GetParameterNames Tests
        /// <summary>
        /// Tests BuildTask.GetParameterNames when only one parameter exists on the BuildTask
        /// </summary>
        [Test]
        public void GetParameterNamesOnlyOneParameterExists()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task parameter='value'/>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");
            string[] parameters = task.GetParameterNames();

            Assertion.AssertEquals(1, parameters.Length);
            Assertion.AssertEquals("parameter", parameters[0]);
        }

        /// <summary>
        /// Tests BuildTask.GetParameterNames when no parameters exist on the BuildTask
        /// </summary>
        [Test]
        public void GetParameterNamesNoParametersExist()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task/>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");
            string[] parameters = task.GetParameterNames();

            Assertion.AssertEquals(0, parameters.Length);
        }

        /// <summary>
        /// Tests BuildTask.GetParameterNames when several parameters exist on the BuildTask
        /// </summary>
        [Test]
        public void GetParameterNamesLotsOfParametersExist()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task p1='v1' p2='v2' p3='v3' p4='v4' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");
            string[] parameters = task.GetParameterNames();

            Assertion.AssertEquals(4, parameters.Length);
            Assertion.AssertEquals("p1", parameters[0]);
            Assertion.AssertEquals("p2", parameters[1]);
            Assertion.AssertEquals("p3", parameters[2]);
            Assertion.AssertEquals("p4", parameters[3]);
        }
        #endregion

        #region GetParameterValue Tests
        /// <summary>
        /// Tests BuildTask.GetParameterValue on one BuildTask
        /// </summary>
        [Test]
        public void GetParameterValueOneBuildTaskWithOneParameter()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task parameter='value'/>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            Assertion.AssertEquals("value", task.GetParameterValue("parameter"));
        }

        /// <summary>
        /// Tests BuildTask.GetParameterValue when parameter value is special characters
        /// </summary>
        [Test]
        public void GetParameterValueWhenSpecialCharacters()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task parameter='%24%40%3b%5c%25'/>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            Assertion.AssertEquals("%24%40%3b%5c%25", task.GetParameterValue("parameter"));
        }

        /// <summary>
        /// Tests BuildTask.GetParameterValue when parameter value is an empty string
        /// </summary>
        [Test]
        public void GetParameterValueWhenEmtpyString()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task parameter='' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            Assertion.AssertEquals(String.Empty, task.GetParameterValue("parameter"));
        }

        /// <summary>
        /// Tests BuildTask.GetParameterValue when parameter value is the Special Task Attribute 'Condition'
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void GetParameterValueWhenSpecialTaskAttributeCondition()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task Condition='A' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.GetParameterValue("Condition");
        }

        /// <summary>
        /// Tests BuildTask.GetParameterValue when parameter value is the Special Task Attribute 'ContinueOnError'
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void GetParameterValueWhenSpecialTaskAttributeContinueOnError()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task ContinueOnError='true' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.GetParameterValue("ContinueOnError");
        }

        /// <summary>
        /// Tests BuildTask.GetParameterValue when parameter value is the Special Task Attribute 'xmlns'
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void GetParameterValueWhenSpecialTaskAttributexmlns()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task xmlns='msbuildnamespace' />
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.GetParameterValue("xmlns");
        }

        /// <summary>
        /// Tests BuildTask.GetParameterValue when parameter value comes from an imported Project
        /// </summary>
        [Test]
        public void GetParameterValueFromImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildTask task = GetSpecificBuildTask(p, "t3", "t3.Task3");

            Assertion.AssertEquals("value", task.GetParameterValue("parameter"));
        }
        #endregion

        #region SetParameterValue Tests
        /// <summary>
        /// Tests BuildTask.SetParameterValue on one BuildTask, simple case
        /// </summary>
        [Test]
        public void SetParameterValueSimple()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("p", "v");

            Assertion.AssertEquals("v", task.GetParameterValue("p"));
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue to null
        /// </summary>
        [Test]
        public void SetParameterValueToNull()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("p", null);
            Assertion.AssertEquals(String.Empty, task.GetParameterValue("p"));
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue to an Empty String
        /// </summary>
        [Test]
        public void SetParameterValueToEmptyString()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("p", String.Empty);

            Assertion.AssertEquals(String.Empty, task.GetParameterValue("p"));
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue to special characters
        /// </summary>
        [Test]
        public void SetParameterValueToSpecialCharacters()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("p", "%24%40%3b%5c%25");
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue to Special Task Attribute Condition
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetParameterValueToSpecialTaskAttributeCondition()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("Condition", "v");
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue to Special Task Attribute ContinueOnError
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetParameterValueToSpecialTaskAttributeContinueOnError()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("ContinueOnError", "v");
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue to Special Task Attribute xmlns
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetParameterValueToSpecialTaskAttributexmlns()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("xmlns", "v");
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue on a BuildTask from an Imported Project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetParameterValueOnBuildTaskFromImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildTask task = GetSpecificBuildTask(p, "t3", "t3.Task3");

            task.SetParameterValue("p", "v");
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue on a BuildTask parameter that already exists
        /// </summary>
        [Test]
        public void SetParameterValueOnAnExistingParameter()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project xmlns='msbuildnamespace'>
                    <Target Name='t' >
                        <Task parameter='value'/>
                    </Target>
                </Project>
                ");

            project.LoadXml(projectContents);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("parameter", "new");

            Assertion.AssertEquals("new", task.GetParameterValue("parameter"));
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue, then save to disk and verify
        /// </summary>
        [Test]
        public void SetParameterValueSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("parameter", "value");

            string expectedProjectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <Target Name='t' >
                            <Task parameter='value'/>
                        </Target>
                    </Project>
                    ");

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue by setting the parameter name to an empty string
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetParameterValueWithParameterNameSetToEmptyString()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue(String.Empty, "v");
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue by setting the parameter name to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(NullReferenceException))]
        public void SetParameterValueWithParameterNameSetToNull()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue(null, "v");
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue by setting the parameter name to special characters
        /// </summary>
        [Test]
        [ExpectedException(typeof(XmlException))]
        public void SetParameterValueWithParameterNameSetToSpecialCharacters()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("%24%40%3b%5c%25", "v");
        }

        /// <summary>
        /// Tests BuildTask.SetParameterValue with the Treat Parameter Value As Literal set to true/false
        /// </summary>
        [Test]
        public void SetParameterValueTreatParameterValueAsLiteral()
        {
            project.LoadXml(ProjectContentsWithOneTask);
            BuildTask task = GetSpecificBuildTask(project, "t", "Task");

            task.SetParameterValue("p1", @"%*?@$();\", true);
            task.SetParameterValue("p2", @"%*?@$();\", false);
            Assertion.AssertEquals(@"%25%2a%3f%40%24%28%29%3b\", task.GetParameterValue("p1"));
            Assertion.AssertEquals(@"%*?@$();\", task.GetParameterValue("p2"));
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Gets a list of all ContinueOnError results within your project
        /// </summary>
        /// <param name="p">Project</param>
        /// <returns>List of bool results for all ContinueOnError BuildTasks within your project</returns>
        private List<bool> GetListOfContinueOnErrorResultsFromSpecificProject(Project p)
        {
            List<bool> continueOnErrorResults = new List<bool>();
            foreach (Target t in project.Targets)
            {
                foreach (BuildTask task in t)
                {
                    continueOnErrorResults.Add(task.ContinueOnError);
                }
            }

            return continueOnErrorResults;
        }

        /// <summary>
        /// Gets the specified BuildTask from your specified Project and Target
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="targetNameThatContainsBuildTask">Target that contains the BuildTask that you want</param>
        /// <param name="buildTaskName">BuildTask name that you want</param>
        /// <returns>The specified BuildTask</returns>
        private BuildTask GetSpecificBuildTask(Project p, string targetNameThatContainsBuildTask, string buildTaskName)
        {
            foreach (Target t in p.Targets)
            {
                if (String.Equals(t.Name, targetNameThatContainsBuildTask, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (BuildTask task in t)
                    {
                        if (String.Equals(task.Name, buildTaskName, StringComparison.OrdinalIgnoreCase))
                        {
                            return task;
                        }
                    }
                }
            }

            return null;
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
        /// Saves a given Project to disk and compares what's saved to disk with expected contents.  Assertion handled within
        ///     ObjectModelHelpers.CompareProjectContents.
        /// </summary>
        /// <param name="p">Project to save</param>
        /// <param name="expectedProjectContents">The Project content that you expect</param>
        private void SaveProjectToDiskAndCompareAgainstExpectedContents(Project p, string expectedProjectContents)
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
        /// Gets a Project that imports another Project
        /// </summary>
        /// <param name="importProjectContents">Project Contents of the imported Project, to get default content, pass in an empty string</param>
        /// <param name="parentProjectContents">Project Contents of the Parent Project, to get default content, pass in an empty string</param>
        /// <returns>Project</returns>
        private Project GetProjectThatImportsAnotherProject(string importProjectContents, string parentProjectContents)
        {
            if (String.IsNullOrEmpty(importProjectContents))
            {
                importProjectContents = ObjectModelHelpers.CleanupFileContents(@" 
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                        <Target Name='t2' >
                            <t2.Task2 parameter='value' />
                        </Target>
                        <Target Name='t3' >
                            <t3.Task3 parameter='value' />
                        </Target>
                    </Project>
                ");
            }

            if (String.IsNullOrEmpty(parentProjectContents))
            {
                parentProjectContents = ObjectModelHelpers.CleanupFileContents(@" 
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                        <Target Name='t1'>
                            <t1.Task1 parameter='value' />
                        </Target>
                        <Import Project='import.proj' />
                    </Project>
                ");
            }

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", importProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", parentProjectContents);
            return ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);
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

        /// <summary>
        /// MyHostObject class for testing BuildTask HostObject
        /// </summary>
        internal class MockHostObject : ITaskHost
        {
        }
        #endregion
    }
}
