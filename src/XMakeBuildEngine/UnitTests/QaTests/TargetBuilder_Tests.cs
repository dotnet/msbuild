// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Collections;
using Microsoft.Build.Unittest;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// Steps to write the tests
    /// 1) Create a TestProjectDefinition object for each project file or a build request that you will submit
    /// 2) Call Build() on the object to submit the build request
    /// 3) Call ValidateResults() on the object to wait till the build completes and the results sent were what we expected
    /// </summary>
    [TestClass]
    [Ignore] // "QA tests are double-initializing some components such as BuildRequestEngine."
    public class TargetBuilder_Tests
    {
        #region Data members

        private Common_Tests _commonTests;
        private ResultsCache _resultsCache;
        private string _tempPath;

        #endregion

        #region Constructor

        /// <summary>
        /// Setup the object
        /// </summary>
        public TargetBuilder_Tests()
        {
            _commonTests = new Common_Tests(this.GetComponent, true);
            _tempPath = System.IO.Path.GetTempPath();
            _resultsCache = null;
        }

        #endregion

        #region Common

        /// <summary>
        /// Delegate to common test setup
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _resultsCache = new ResultsCache();
            _commonTests.Setup();
        }

        /// <summary>
        /// Delegate to common test teardown
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            _commonTests.TearDown();
            _resultsCache = null;
        }

        #endregion

        #region GetComponent delegate

        /// <summary>
        /// Provides the components required by the tests
        /// </summary>
        internal IBuildComponent GetComponent(BuildComponentType type)
        {
            switch (type)
            {
                case BuildComponentType.RequestBuilder:
                    RequestBuilder requestBuilder = new RequestBuilder();
                    return (IBuildComponent)requestBuilder;

                case BuildComponentType.TaskBuilder:
                    QAMockTaskBuilder taskBuilder = new QAMockTaskBuilder();
                    return (IBuildComponent)taskBuilder;

                case BuildComponentType.TargetBuilder:
                    TargetBuilder targetBuilder = new TargetBuilder();
                    return (IBuildComponent)targetBuilder;

                case BuildComponentType.ResultsCache:
                    return (IBuildComponent)_resultsCache;

                default:
                    throw new ArgumentException("Unexpected type requested. Type = " + type.ToString());
            }
        }

        #endregion

        #region Simple building targets

        /// <summary>
        /// Build 1 project containing a single target with a single task
        /// </summary>
        [TestMethod]
        public void Build1ProjectWith1TargetAnd1Task()
        {
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition project1 = test1.ProjectDefinition;
            TargetDefinition target1 = new TargetDefinition(RequestDefinition.defaultTargetName, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            target1.AddTask(task1);
            project1.AddTarget(target1);

            test1.SubmitBuildRequest();

            test1.WaitForResults();
            test1.ValidateTargetBuilt(RequestDefinition.defaultTargetName);
            test1.ValidateTargetEndResult(RequestDefinition.defaultTargetName, TargetResultCode.Success, null);
        }

        /// <summary>
        /// Build 1 project containing a single target with a 2 task
        /// </summary>
        [TestMethod]
        public void Build1ProjectWith1TargetAnd2Task()
        {
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition project1 = test1.ProjectDefinition;
            TargetDefinition target1 = new TargetDefinition(RequestDefinition.defaultTargetName, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition("task1", null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition("task2", null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            target1.AddTask(task1);
            target1.AddTask(task2);
            project1.AddTarget(target1);

            test1.SubmitBuildRequest();

            task1.WaitForTaskToComplete();
            task2.WaitForTaskToComplete();
            test1.WaitForResults();
            test1.ValidateTargetBuilt(RequestDefinition.defaultTargetName);
            test1.ValidateTargetEndResult(RequestDefinition.defaultTargetName, TargetResultCode.Success, null);
        }

        /// <summary>
        /// Build 1 project containing a 2 target with a 1 task
        /// </summary>
        [TestMethod]
        public void Build1ProjectWith2TargetAnd1TaskEach()
        {
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[2] { "Target1", "Target2" }, null, 100, null, (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition project1 = test1.ProjectDefinition;
            TargetDefinition target1 = new TargetDefinition("Target1", project1.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("Target2", project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition("Task1", null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition("Task2", null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            target1.AddTask(task1);
            target1.AddTask(task2);
            project1.AddTarget(target1);
            project1.AddTarget(target2);

            test1.SubmitBuildRequest();

            task1.WaitForTaskToComplete();
            task2.WaitForTaskToComplete();
            test1.WaitForResults();
            test1.ValidateTargetBuilt("Target1");
            test1.ValidateTargetBuilt("Target2");
            test1.ValidateTargetEndResult("Target1", TargetResultCode.Success, null);
            test1.ValidateTargetEndResult("Target2", TargetResultCode.Success, null);
        }

        #endregion

        #region Dependencies

        /// <summary>
        /// Build a project with 1 target which depends on another target. Validation makes sure that the targets are executed in the order
        /// </summary>
        [TestMethod]
        public void BuildProjectWith1TargetWhichDependsOn1Target()
        {
            ProjectDefinition project1 = new ProjectDefinition(FullProjectPath("1.proj"));
            TargetDefinition target1 = new TargetDefinition("Target1", null, "Target2", project1.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("Target2", null, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            target1.AddTask(task1);
            target2.AddTask(task2);
            project1.AddTarget(target1);
            project1.AddTarget(target2);
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "Target1" }, null, 100, null, (IBuildComponentHost)_commonTests.Host);
            test1.ProjectDefinition = project1;

            test1.SubmitBuildRequest();

            task2.WaitForTaskToComplete();
            task1.WaitForTaskToComplete();
            test1.WaitForResults();

            test1.ValidateTargetEndResult("Target1", TargetResultCode.Success, null);
            test1.ValidateNonPrimaryTargetEndResult("Target2", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Build a project with 1 target which depends on another target. The dependent target has a true condition Validation makes sure that the targets are executed in the order
        /// </summary>
        [TestMethod]
        public void BuildDependentTargetWithTrueCondition()
        {
            ProjectDefinition project1 = new ProjectDefinition(FullProjectPath("1.proj"));
            TargetDefinition target1 = new TargetDefinition("Target1", null, "Target2", project1.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("Target2", @"'1' == '1'", project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            target1.AddTask(task1);
            target2.AddTask(task2);
            project1.AddTarget(target1);
            project1.AddTarget(target2);
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "Target1" }, null, 100, null, (IBuildComponentHost)_commonTests.Host);
            test1.ProjectDefinition = project1;

            test1.SubmitBuildRequest();

            task2.WaitForTaskToComplete();
            task1.WaitForTaskToComplete();
            test1.WaitForResults();

            test1.ValidateTargetEndResult("Target1", TargetResultCode.Success, null);
            test1.ValidateNonPrimaryTargetEndResult("Target2", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Build a project with 1 target which depends on another target. The dependent target has a false condition Validation makes sure that the targets are executed in the order
        /// </summary>
        [TestMethod]
        public void BuildDependentTargetWithFalseCondition()
        {
            ProjectDefinition project1 = new ProjectDefinition(FullProjectPath("1.proj"));
            TargetDefinition target1 = new TargetDefinition("Target1", null, "Target2", project1.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("Target2", @"'1' == '2'", project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            target1.AddTask(task1);
            target2.AddTask(task2);
            project1.AddTarget(target1);
            project1.AddTarget(target2);
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "Target1" }, null, 100, null, (IBuildComponentHost)_commonTests.Host);
            test1.ProjectDefinition = project1;

            test1.SubmitBuildRequest();

            task1.WaitForTaskToComplete();
            test1.WaitForResults();
            test1.ValidateTargetBuilt("Target1");
            test1.ValidateNonPrimaryTargetEndResult("Target2", TargetResultCode.Skipped, null);
            test1.ValidateTargetEndResult("Target1", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Dependency chain: Target1 depends on Target2 and Target3 and Target2 depends on Target4
        /// </summary>
        [TestMethod]
        public void TargetDependencyChain()
        {
            ProjectDefinition project1 = new ProjectDefinition(FullProjectPath("1.proj"));
            TargetDefinition target1 = new TargetDefinition("Target1", null, "Target2;Target3", project1.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("Target2", null, "Target4", project1.ProjectXmlDocument);
            TargetDefinition target3 = new TargetDefinition("Target3", null, null, project1.ProjectXmlDocument);
            TargetDefinition target4 = new TargetDefinition("Target4", null, null, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task3 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task4 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "Target1" }, null, 100, null, (IBuildComponentHost)_commonTests.Host);
            target1.AddTask(task1);
            target2.AddTask(task2);
            target3.AddTask(task3);
            target4.AddTask(task4);
            project1.AddTarget(target1);
            project1.AddTarget(target2);
            project1.AddTarget(target3);
            project1.AddTarget(target4);
            test1.ProjectDefinition = project1;

            test1.SubmitBuildRequest();

            task4.WaitForTaskToComplete();
            task2.WaitForTaskToComplete();
            task3.WaitForTaskToComplete();
            task1.WaitForTaskToComplete();
            test1.WaitForResults();

            test1.ValidateTargetEndResult("Target1", TargetResultCode.Success, null);
            test1.ValidateNonPrimaryTargetEndResult("Target2", TargetResultCode.Success, null);
            test1.ValidateNonPrimaryTargetEndResult("Target3", TargetResultCode.Success, null);
            test1.ValidateNonPrimaryTargetEndResult("Target4", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Dependency chain: Target1 depends on Target2 and Target3 and Target3 depends on Target4
        /// </summary>
        [TestMethod]
        public void TargetDependencyChain2()
        {
            ProjectDefinition project1 = new ProjectDefinition(FullProjectPath("1.proj"));
            TargetDefinition target1 = new TargetDefinition("Target1", null, "Target2;Target3", project1.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("Target2", null, null, project1.ProjectXmlDocument);
            TargetDefinition target3 = new TargetDefinition("Target3", null, "Target4", project1.ProjectXmlDocument);
            TargetDefinition target4 = new TargetDefinition("Target4", null, null, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task3 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task4 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "Target1" }, null, 100, null, (IBuildComponentHost)_commonTests.Host);
            target1.AddTask(task1);
            target2.AddTask(task2);
            target3.AddTask(task3);
            target4.AddTask(task4);
            project1.AddTarget(target1);
            project1.AddTarget(target2);
            project1.AddTarget(target3);
            project1.AddTarget(target4);
            test1.ProjectDefinition = project1;

            test1.SubmitBuildRequest();

            task2.WaitForTaskToComplete();
            task4.WaitForTaskToComplete();
            task3.WaitForTaskToComplete();
            task1.WaitForTaskToComplete();
            test1.WaitForResults();

            test1.ValidateTargetEndResult("Target1", TargetResultCode.Success, null);
            test1.ValidateNonPrimaryTargetEndResult("Target2", TargetResultCode.Success, null);
            test1.ValidateNonPrimaryTargetEndResult("Target3", TargetResultCode.Success, null);
            test1.ValidateNonPrimaryTargetEndResult("Target4", TargetResultCode.Success, null);
        }

        #endregion

        #region Circular Dependency

        /// <summary>
        /// Target1 depends on target2 and target2 depends on target1
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CircularDependencyWithParentTarget()
        {
            ProjectDefinition project = new ProjectDefinition(FullProjectPath("1.proj"));
            TargetDefinition target1 = new TargetDefinition("target1", null, "target2", project.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("target2", null, "target1", project.ProjectXmlDocument);
            RequestDefinition test = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "target1" }, null, 0, null, (IBuildComponentHost)_commonTests.Host);
            project.AddTarget(target1);
            project.AddTarget(target2);
            test.ProjectDefinition = project;

            test.SubmitBuildRequest();

            test.WaitForResultsThrowException();
        }


        /// <summary>
        /// Project has a set of initial target -  target1 and target2. target2 depends on target1
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CircularDependencyInvolvingTargetsWithinInitialTargets()
        {
            ProjectDefinition project = new ProjectDefinition(FullProjectPath("1.proj"), "target1", null, "2.0", true);
            TargetDefinition target1 = new TargetDefinition("target1", null, "target2", project.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("target2", null, "target1", project.ProjectXmlDocument);

            project.AddTarget(target1);
            project.AddTarget(target2);

            RequestDefinition request = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "target1" }, null, 0, null, (IBuildComponentHost)_commonTests.Host);
            request.ProjectDefinition = project;

            request.SubmitBuildRequest();

            request.WaitForResultsThrowException();
        }

        /// <summary>
        /// Project has a set of default target -  target1 and target2. target2 depends on target1
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CircularDependencyInvolvingTargetsWithinDefaultTargets()
        {
            ProjectDefinition project = new ProjectDefinition(FullProjectPath("1.proj"), null, "target1", "2.0", true);
            TargetDefinition target1 = new TargetDefinition("target1", null, "target2", project.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("target2", null, "target1", project.ProjectXmlDocument);

            project.AddTarget(target1);
            project.AddTarget(target2);

            RequestDefinition request = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "target1" }, null, 0, null, (IBuildComponentHost)_commonTests.Host);
            request.ProjectDefinition = project;

            request.SubmitBuildRequest();

            request.WaitForResultsThrowException();
        }

        #endregion

        #region Target Rebuild

        /// <summary>
        /// Build request builds target1, target2 and target1
        /// </summary>
        [TestMethod]
        public void BuildRequestContainsTheSameTargetTwiceWHichPreviouslyPassed()
        {
            string[] targetsToBuild = new string[3] { "target1", "target2", "target1" };

            RequestDefinition request = new RequestDefinition(FullProjectPath("1.proj"), "2.0", targetsToBuild, null, 0, null, (IBuildComponentHost)_commonTests.Host);

            TargetDefinition target1 = new TargetDefinition("target1", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("target2", request.ProjectDefinition.ProjectXmlDocument);

            request.ProjectDefinition.AddTarget(target1);
            request.ProjectDefinition.AddTarget(target2);

            request.SubmitBuildRequest();

            request.WaitForResults();

            request.ValidateTargetEndResult("target1", TargetResultCode.Success, null);
            request.ValidateTargetEndResult("target2", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 depends on target2 and target3. Target2 depends on target4. Target3 depends on target4
        /// </summary>
        [TestMethod]
        public void DependentTargetsDependOnTheSameTarget1()
        {
            string[] targetsToBuild = new string[1] { "target1" };

            RequestDefinition request = new RequestDefinition(FullProjectPath("1.proj"), "2.0", targetsToBuild, null, 0, null, (IBuildComponentHost)_commonTests.Host);

            TargetDefinition target1 = new TargetDefinition("target1", null, "target2;target3", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("target2", null, "target4", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target3 = new TargetDefinition("target3", null, "target4", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target4 = new TargetDefinition("target4", request.ProjectDefinition.ProjectXmlDocument);

            request.ProjectDefinition.AddTarget(target1);
            request.ProjectDefinition.AddTarget(target2);
            request.ProjectDefinition.AddTarget(target3);
            request.ProjectDefinition.AddTarget(target4);

            request.SubmitBuildRequest();

            request.WaitForResults();

            request.ValidateTargetEndResult("target1", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target2", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target3", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target4", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 depends on target2 and target3. Target2 depends on target4. Target4 depends on target5. Target3 depends on target5
        /// </summary>
        [TestMethod]
        public void DependentTargetsDependOnTheSameTarget2()
        {
            string[] targetsToBuild = new string[1] { "target1" };

            RequestDefinition request = new RequestDefinition(FullProjectPath("1.proj"), "2.0", targetsToBuild, null, 0, null, (IBuildComponentHost)_commonTests.Host);

            TargetDefinition target1 = new TargetDefinition("target1", null, "target2;target3", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("target2", null, "target4", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target3 = new TargetDefinition("target3", null, "target5", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target4 = new TargetDefinition("target4", null, "target5", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target5 = new TargetDefinition("target5", request.ProjectDefinition.ProjectXmlDocument);

            request.ProjectDefinition.AddTarget(target1);
            request.ProjectDefinition.AddTarget(target2);
            request.ProjectDefinition.AddTarget(target3);
            request.ProjectDefinition.AddTarget(target4);
            request.ProjectDefinition.AddTarget(target5);

            request.SubmitBuildRequest();

            request.WaitForResults();

            request.ValidateTargetEndResult("target1", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target2", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target3", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target4", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target5", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 depends on target2 and target3. Target2 depends on target4. Target4 depends on target5. Target3 depends on target2
        /// </summary>
        [TestMethod]
        public void DependentTargetsDependOnTheSameTarget3()
        {
            string[] targetsToBuild = new string[1] { "target1" };

            RequestDefinition request = new RequestDefinition(FullProjectPath("1.proj"), "2.0", targetsToBuild, null, 0, null, (IBuildComponentHost)_commonTests.Host);

            TargetDefinition target1 = new TargetDefinition("target1", null, "target2;target3", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("target2", null, "target4", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target3 = new TargetDefinition("target3", null, "target2", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target4 = new TargetDefinition("target4", null, "target5", request.ProjectDefinition.ProjectXmlDocument);
            TargetDefinition target5 = new TargetDefinition("target5", request.ProjectDefinition.ProjectXmlDocument);

            request.ProjectDefinition.AddTarget(target1);
            request.ProjectDefinition.AddTarget(target2);
            request.ProjectDefinition.AddTarget(target3);
            request.ProjectDefinition.AddTarget(target4);
            request.ProjectDefinition.AddTarget(target5);

            request.SubmitBuildRequest();

            request.WaitForResults();

            request.ValidateTargetEndResult("target1", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target2", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target3", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target4", TargetResultCode.Success, null);
            request.ValidateNonPrimaryTargetEndResult("target5", TargetResultCode.Success, null);
        }

        #endregion

        #region Condition on target

        /// <summary>
        /// Build 1 project containing a single target with condition evaluating to true
        /// </summary>
        [TestMethod]
        public void Build1ProjectWith1TargetWhereConditionIsTrue()
        {
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition project1 = test1.ProjectDefinition;
            TargetDefinition target1 = new TargetDefinition(RequestDefinition.defaultTargetName, @"'1' == '1'", project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            target1.AddTask(task1);
            project1.AddTarget(target1);

            test1.SubmitBuildRequest();

            test1.WaitForResults();
            test1.ValidateTargetBuilt(RequestDefinition.defaultTargetName);
            test1.ValidateTargetEndResult(RequestDefinition.defaultTargetName, TargetResultCode.Success, null);
        }

        /// <summary>
        /// Build 1 project containing a single target with condition evaluating to false
        /// </summary>
        [TestMethod]
        public void Build1ProjectWith1TargetWhereConditionIsFasle()
        {
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition project1 = test1.ProjectDefinition;
            TargetDefinition target1 = new TargetDefinition(RequestDefinition.defaultTargetName, @"'1' == '2'", project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            target1.AddTask(task1);
            project1.AddTarget(target1);

            test1.SubmitBuildRequest();

            test1.WaitForResults();
            test1.ValidateTargetEndResult(RequestDefinition.defaultTargetName, TargetResultCode.Skipped, null);
        }

        #endregion

        #region OnError

        /// <summary>
        /// Target1 executes task1. Task1 has an error with Stop on error. Target1 has OnError to execute target2
        /// </summary>
        [TestMethod]
        public void OnErrorTargetIsBuilt()
        {
            RequestDefinition r1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "tar1" }, null, 0, null, (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition p1 = r1.ProjectDefinition;

            TargetDefinition tar1 = new TargetDefinition("tar1", p1.ProjectXmlDocument);
            TargetDefinition tarOnError = new TargetDefinition("error", p1.ProjectXmlDocument);
            TaskDefinition tas1 = new TaskDefinition("tas1", p1.ProjectXmlDocument, TestUtilities.GetStopWithErrorResult());

            tar1.AddTask(tas1);
            tar1.AddOnError("error", null);
            p1.AddTarget(tar1);
            p1.AddTarget(tarOnError);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("tar1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("error", TargetResultCode.Success, null);
        }

        #endregion

        #region continue with error

        /// <summary>
        /// Build 1 project containing a single target and a task where the task fails but continues on Error
        /// </summary>
        [TestMethod]
        public void Build1ProjectWith1TargetWhereTheTaskFailButContinuesOnError()
        {
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition project1 = test1.ProjectDefinition;
            TargetDefinition target1 = new TargetDefinition(RequestDefinition.defaultTargetName, null, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetContinueWithErrorResult());
            target1.AddTask(task1);
            project1.AddTarget(target1);

            test1.SubmitBuildRequest();

            test1.WaitForResults();
            test1.ValidateTargetBuilt(RequestDefinition.defaultTargetName);
            test1.ValidateTargetEndResult(RequestDefinition.defaultTargetName, TargetResultCode.Success, null);
        }

        /// <summary>
        /// Build a project with 1 target which depends on another target. The task in the dependent target fails with continue on error.
        /// </summary>
        [TestMethod]
        public void BuildProjectWith1TargetWhichDependsOn1TargetWhichErrorsWithContinue()
        {
            ProjectDefinition project1 = new ProjectDefinition(FullProjectPath("1.proj"));
            TargetDefinition target1 = new TargetDefinition("Target1", null, "Target2", project1.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("Target2", null, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetContinueWithErrorResult());
            target1.AddTask(task1);
            target2.AddTask(task2);
            project1.AddTarget(target1);
            project1.AddTarget(target2);
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "Target1" }, null, 100, null, (IBuildComponentHost)_commonTests.Host);
            test1.ProjectDefinition = project1;

            test1.SubmitBuildRequest();

            task2.WaitForTaskToComplete();
            task1.WaitForTaskToComplete();
            test1.WaitForResults();

            test1.ValidateNonPrimaryTargetEndResult("Target2", TargetResultCode.Success, null);
            test1.ValidateTargetEndResult("Target1", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Build a project with 1 target which depends on another target. The task in the dependent target fails with continue on error.
        /// </summary>
        [TestMethod]
        public void TasksContinueToExecuteAfterContinueOnError()
        {
            ProjectDefinition project1 = new ProjectDefinition(FullProjectPath("1.proj"));
            TargetDefinition target1 = new TargetDefinition("Target1", null, "Target2", project1.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("Target2", null, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition("t1-1", null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition("t2-1", null, false, project1.ProjectXmlDocument, TestUtilities.GetContinueWithErrorResult());
            TaskDefinition task3 = new TaskDefinition("t2-2", null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            target1.AddTask(task1);
            target2.AddTask(task2);
            target2.AddTask(task3);

            project1.AddTarget(target1);
            project1.AddTarget(target2);
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "Target1" }, null, 100, null, (IBuildComponentHost)_commonTests.Host);
            test1.ProjectDefinition = project1;

            test1.SubmitBuildRequest();

            task2.WaitForTaskToComplete();
            task1.WaitForTaskToComplete();
            test1.WaitForResults();

            test1.ValidateNonPrimaryTargetEndResult("Target2", TargetResultCode.Success, null);
            test1.ValidateTargetEndResult("Target1", TargetResultCode.Success, null);
        }

        #endregion

        #region Stop on Error

        /// <summary>
        /// Build a project with 1 target which depends on another target. The task in the dependent target fails. Task 1 should not execute
        /// </summary>
        [TestMethod]
        public void BuildProjectWith1TargetWhichDependsOn1TargetWhichErrors()
        {
            ProjectDefinition project1 = new ProjectDefinition(FullProjectPath("1.proj"));
            TargetDefinition target1 = new TargetDefinition("Target1", null, "Target2", project1.ProjectXmlDocument);
            TargetDefinition target2 = new TargetDefinition("Target2", null, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetSuccessResult());
            TaskDefinition task2 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetStopWithErrorResult());
            target1.AddTask(task1);
            target2.AddTask(task2);
            project1.AddTarget(target1);
            project1.AddTarget(target2);
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), "2.0", new string[1] { "Target1" }, null, 100, null, (IBuildComponentHost)_commonTests.Host);
            test1.ProjectDefinition = project1;

            test1.SubmitBuildRequest();

            task2.WaitForTaskToComplete();
            test1.WaitForResults();

            test1.ValidateNonPrimaryTargetEndResult("Target2", TargetResultCode.Failure, null);
            test1.ValidateTargetEndResult("Target1", TargetResultCode.Failure, null);
        }

        /// <summary>
        /// Build 1 project containing a single target and a task where the task fails
        /// </summary>
        [TestMethod]
        public void Build1ProjectWith1TargetWhereTheTaskFail()
        {
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition project1 = test1.ProjectDefinition;
            TargetDefinition target1 = new TargetDefinition(RequestDefinition.defaultTargetName, null, project1.ProjectXmlDocument);
            TaskDefinition task1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, project1.ProjectXmlDocument, TestUtilities.GetStopWithErrorResult());
            target1.AddTask(task1);
            project1.AddTarget(target1);

            test1.SubmitBuildRequest();

            test1.WaitForResults();
            test1.ValidateTargetBuilt(RequestDefinition.defaultTargetName);
            test1.ValidateTargetEndResult(RequestDefinition.defaultTargetName, TargetResultCode.Failure, null);
        }

        #endregion

        #region Input and Output

        /// <summary>
        /// Build 1 project containing a single target. The target outputs a string
        /// </summary>
        [TestMethod]
        public void ValidateTargetOutput()
        {
            RequestDefinition test1 = new RequestDefinition(FullProjectPath("1.proj"), (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition project1 = test1.ProjectDefinition;
            TargetDefinition target1 = new TargetDefinition("Target1", null, "Foo", null, project1.ProjectXmlDocument);

            project1.AddTarget(target1);

            test1.SubmitBuildRequest();

            test1.WaitForResults();
            test1.ValidateTargetEndResult("Target1", TargetResultCode.Success, new string[1] { "Foo" });
        }

        #endregion

        #region Task Cancellation

        /// <summary>
        /// Cancel an executing task. When the task is cancelled the mocked task builder sets the exception to MockTaskBuilderException in TaskResults.
        /// This is later added as the target result also
        /// </summary>

        [TestMethod]
        [Ignore] // "Flakey on a slow machine need to investigate"
        [ExpectedException(typeof(BuildAbortedException))]
        public void TaskStatusOnCancellation()
        {
            RequestDefinition r1 = new RequestDefinition(FullProjectPath("1.proj"), "4.0", null, null, 5000, null, (IBuildComponentHost)_commonTests.Host);
            ProjectDefinition p1 = r1.ProjectDefinition;
            TargetDefinition t1 = new TargetDefinition(RequestDefinition.defaultTargetName, p1.ProjectXmlDocument);
            TaskDefinition ta1 = new TaskDefinition(RequestDefinition.defaultTaskName, null, false, p1.ProjectXmlDocument, TestUtilities.GetSuccessResult());

            t1.AddTask(ta1);
            p1.AddTarget(t1);

            r1.SubmitBuildRequest();
            ta1.WaitForTaskToStart();
            _commonTests.Host.ShutDownRequestEngine();

            r1.WaitForResultsThrowException();
        }

        #endregion

        #region Private methods


        /// <summary>
        /// Full path of the project file
        /// </summary>
        /// <param name="filename">File name</param>
        /// <returns>Full path to the file name</returns>
        private string FullProjectPath(string filename)
        {
            filename = System.IO.Path.Combine(_tempPath, filename);
            return filename;
        }

        #endregion
    }
}