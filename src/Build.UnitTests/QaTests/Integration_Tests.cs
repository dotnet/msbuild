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
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.Collections;
using System.IO;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

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
    public class Integration_Tests
    {
        #region Data members

        private Common_Tests _commonTests;
        private ResultsCache _resultsCache;
        private string _assemblyPath;
        private string _tempPath;

        #endregion

        #region Constructor

        /// <summary>
        /// Setup the object
        /// </summary>
        public Integration_Tests()
        {
            _commonTests = new Common_Tests(this.GetComponent, true);
            _resultsCache = null;
            _tempPath = System.IO.Path.GetTempPath();
            _assemblyPath = Path.GetDirectoryName(
                new Uri(System.Reflection.Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath);
            _assemblyPath = Path.Combine(_assemblyPath, "Microsoft.Build.Unittest.dll");
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
                    TaskBuilder taskBuilder = new TaskBuilder();
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

        #region Data Input and Output from task

        /// <summary>
        /// Send some parameters to the task and expect certain outputs
        /// </summary>
        [TestMethod]
        [Ignore] // "Cannot use a project instance if the project is created from a file."
        public void InputAndOutputFromTask()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                      @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t'>
                                <QAMockTaskForIntegrationTests ExpectedOutput='Foo' TaskShouldThrowException='false'>
                                    <Output TaskParameter='TaskOutput' PropertyName='SomeProperty'/>
                                </QAMockTaskForIntegrationTests>
                            </Target>
                        </Project>"),
                        _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();
            ProjectPropertyInstance property = projectInstance.GetProperty("SomeProperty");
            Assert.IsTrue(property.EvaluatedValue == "Foo", "SomeProperty=Foo");
        }

        #endregion

        #region Data Output from target

        /// <summary>
        /// Target outputs
        /// </summary>
        [TestMethod]
        public void OutputFromTarget()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                      @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t' Outputs='$(SomeProperty)'>
                                <QAMockTaskForIntegrationTests ExpectedOutput='Foo' TaskShouldThrowException='false'>
                                    <Output TaskParameter='TaskOutput' PropertyName='SomeProperty'/>
                                </QAMockTaskForIntegrationTests>
                            </Target>
                        </Project>"),
                        _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();
            r1.ValidateTargetEndResult("t", TargetResultCode.Success, new string[1] { "Foo" });
        }

        #endregion

        #region Data changed

        /// <summary>
        /// Send some parameters to the task and expect certain outputs. This output overwrites an existing property
        /// </summary>
        [TestMethod]
        [Ignore] // "Cannot use a project instance if the project is created from a file."
        public void OutputFromTaskUpdatesProperty()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                      @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <PropertyGroup>
                                <SomeProperty>oldvalue</SomeProperty>
                            </PropertyGroup>

                            <Target Name='t'>
                                <QAMockTaskForIntegrationTests ExpectedOutput='Foo' TaskShouldThrowException='false'>
                                    <Output TaskParameter='TaskOutput' PropertyName='SomeProperty'/>
                                </QAMockTaskForIntegrationTests>
                            </Target>
                        </Project>"),
                        _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();
            ProjectPropertyInstance property = projectInstance.GetProperty("SomeProperty");
            Assert.IsTrue(property.EvaluatedValue == "Foo", "SomeProperty=Foo");
        }

        #endregion

        #region OnError

        /// <summary>
        /// Target1 executes task1. Task1 has an error with continue on error. Target1 then executes task2 with has an error with stop on error.
        /// Target1 has OnError to execute target2
        /// </summary>
        [TestMethod]
        public void OnErrorTargetIsBuilt()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
            _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 executes task1. Task1 has an error with continue on error. Target1 then executes task2 with has an error with stop on error.
        /// Target1 has OnError to execute target2
        /// </summary>
        [TestMethod]
        public void OnErrorTargetIsBuilt2()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true' ContinueOnError='true'/>
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
            _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target0 depends on Target1 which executes task1. Task1 has an error with stop on error. Target1 has OnError to execute target2
        /// </summary>
        [TestMethod]
        public void OnErrorTargetIsBuilt3()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t0' DependsOnTargets='t1' />

                            <Target Name='t1' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t0", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t0", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 executeserror with stop on error. Target1 has OnError to execute target2 and target3
        /// </summary>
        [TestMethod]
        public void MultipleOnErrorTargetIsBuiltOnDependsOnTarget()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2;t3' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                            <Target Name='t3' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Success, null);
            r1.ValidateNonPrimaryTargetEndResult("t3", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 executes task1 which does not error. Target1 has OnError to execute target2
        /// </summary>
        [TestMethod]
        public void OnErrorTargetIsNotBuiltInNonErrorCondition()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1' >
                                <QAMockTaskForIntegrationTests />
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Success, null);
            r1.ValidateTargetDidNotBuild("t2");
        }

        /// <summary>
        /// Target1 executes task1. Task1 has an error with continue on error. Target1 has OnError to execute target2
        /// </summary>
        [TestMethod]
        public void OnErrorTargetIsNotBuiltWithContinueOnError()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true' ContinueOnError='true'/>
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
            _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Success, null);
            r1.ValidateTargetDidNotBuild("t2");
        }

        /// <summary>
        /// Target1 which executes task1. Task1 has an error with stop on error. Target1 has OnError to execute target2 and target3
        /// </summary>
        [TestMethod]
        public void MultipleOnErrorTargetIsBuilt()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2;t3' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                            <Target Name='t3' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Success, null);
            r1.ValidateNonPrimaryTargetEndResult("t3", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 executes task1. Task1 has an error with Stop on error. Target1 has OnError to execute target2 but is above task1
        /// (OnError is above the failing task)
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void OnErrorIsDefinedAboveTheFailingTask()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
               @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <OnError ExecuteTargets='t2' />
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateTargetDidNotBuild("t2");
        }

        /// <summary>
        /// Request builds target1 and target2. Target2 has an error and calls target1
        /// </summary>
        [TestMethod]
        public void OnErrorTargetIsATargetAlreadyBuilt()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t1' />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1;t2", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Success, null);
            r1.ValidateTargetEndResult("t2", TargetResultCode.Failure, null);
        }

        /// <summary>
        /// Initial target has target1. Target1 has an error and contains OnError which builds target2
        /// </summary>
        [TestMethod]
        public void OnErrorTargetInvolvingInitialTarget()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace' InitialTargets='t1'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", null, out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Default target has target1. Target1 has an error and contains OnError which builds target2
        /// </summary>
        [TestMethod]
        public void OnErrorTargetInvolvingDefaultTarget()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace' DefaultTargets='t1'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", null, out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 which fails has an OnError with target2 and target3 where target2 has a task with stop on error and has OnError.
        /// Target3 is also executed
        /// </summary>
        [TestMethod]
        public void ErrorInFirstOnErrorTarget()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2;t3' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t4' />
                            </Target>

                            <Target Name='t3' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                            <Target Name='t4' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t3", TargetResultCode.Success, null);
            r1.ValidateNonPrimaryTargetEndResult("t4", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 which fails has an OnError with target2 and target3 where target2 has a task with stop on error and has OnError.
        /// Target3 is executed in this case
        /// </summary>
        [TestMethod]
        public void ErrorInFirstOnErrorTarget2()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2;t3' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                            <Target Name='t3' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true'/>
                                <OnError ExecuteTargets='t4' />
                            </Target>

                            <Target Name='t4' >
                                <QAMockTaskForIntegrationTests />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();
            
            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Success, null);
            r1.ValidateNonPrimaryTargetEndResult("t3", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t4", TargetResultCode.Success, null);
        }

        /// <summary>
        /// Target1 which fails has an OnError with target2 and target3 where target2 also errors and has OnError
        /// </summary>
        [TestMethod]
        public void ContinueOnErrorInFirstOnErrorTarget()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2;t3' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests />
                            </Target>
                                
                            <Target Name='t3' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t4' />
                            </Target>

                            <Target Name='t4' />

                        </Project>"),
            _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResults();

            r1.ValidateTargetEndResult("t1", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t3", TargetResultCode.Failure, null);
            r1.ValidateNonPrimaryTargetEndResult("t2", TargetResultCode.Success, null);
            r1.ValidateNonPrimaryTargetEndResult("t4", TargetResultCode.Success, null);
        }


        /// <summary>
        /// Circular dependency with OnError
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CircularDependency()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true'/>
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t1' />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResultsThrowException();
        }

        /// <summary>
        /// Circular dependency with OnError where OnError also has an OnError
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CircularDependencyChain1()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true'/>
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t3' />
                            </Target>

                            <Target Name='t3' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2' />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResultsThrowException();
        }

        /// <summary>
        /// Circular dependency with OnError where OnError also has an OnError
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CircularDependencyChain2()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true'/>
                                <OnError ExecuteTargets='t2' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t3' />
                            </Target>

                            <Target Name='t3' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t1' />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResultsThrowException();
        }

        /// <summary>
        /// Circular dependency with OnError where OnError also has an OnError
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CircularDependencyChain3()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true'/>
                                <OnError ExecuteTargets='t3' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t3' />
                            </Target>

                            <Target Name='t3' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2' />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResultsThrowException();
        }

        /// <summary>
        /// Circular dependency with OnError where OnError also has an OnError
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void CircularDependencyChain4()
        {
            string projectFileContents = String.Format(ObjectModelHelpers.CleanupFileContents(
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                            <UsingTask TaskName='QAMockTaskForIntegrationTests' AssemblyFile='{0}' />

                            <Target Name='t1'>
                                <QAMockTaskForIntegrationTests TaskShouldError='true'/>
                                <OnError ExecuteTargets='t3' />
                            </Target>

                            <Target Name='t2' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t1' />
                            </Target>

                            <Target Name='t3' >
                                <QAMockTaskForIntegrationTests TaskShouldError='true' />
                                <OnError ExecuteTargets='t2' />
                            </Target>

                        </Project>"),
                _assemblyPath);

            ProjectInstance projectInstance = null;
            RequestDefinition r1 = GetRequestUsingProject(projectFileContents, "1.proj", "t1", out projectInstance);

            r1.SubmitBuildRequest();

            r1.WaitForResultsThrowException();
        }

        #endregion

        #region Private Helper

        /// <summary>
        /// Creates a projectinstance from a string which contains the project file contents
        /// </summary>
        private ProjectInstance GenerateProjectInstanceFromXml(string projectFileContents)
        {
            Project projectDefinition = new Project(XmlReader.Create(new StringReader(projectFileContents)));
            return projectDefinition.CreateProjectInstance();
        }

        /// <summary>
        /// Given a string containing the project file content return a RequestBuilder with all the information of the project
        /// populated.
        /// </summary>
        private RequestDefinition GetRequestUsingProject(string projectFileContents, string projectName, string targetName, out ProjectInstance project)
        {
            ProjectDefinition p1 = new ProjectDefinition(projectName);
            project = p1.MSBuildProjectInstance = GenerateProjectInstanceFromXml(projectFileContents);
            string[] targetNames = null;
            if (targetName != null)
            {
                targetNames = targetName.Split(';');
            }

            projectName = System.IO.Path.Combine(_tempPath, projectName);
            RequestDefinition r1 = new RequestDefinition(projectName, "2.0", targetNames, null, 0, null, (IBuildComponentHost)_commonTests.Host);
            r1.ProjectDefinition = p1;

            return r1;
        }

        #endregion
    }

    /// <summary>
    /// Mock task implementation which will be used by the above tests
    /// </summary>
    public class QAMockTaskForIntegrationTests : Microsoft.Build.Framework.ITask
    {
        #region Private data

        /// <summary>
        /// Task host
        /// </summary>
        private Microsoft.Build.Framework.ITaskHost _taskHost;

        /// <summary>
        /// Build engine
        /// </summary>
        private Microsoft.Build.Framework.IBuildEngine _buildEngine;

        /// <summary>
        /// Expected output
        /// </summary>
        private string _expectedOutput;

        /// <summary>
        /// Task should show an exception
        /// </summary>
        private bool _taskShouldThrow;

        /// <summary>
        /// Task should return false from Execute indicating error
        /// </summary>
        private bool _taskShouldError;

        #endregion

        /// <summary>
        /// Default constructor
        /// </summary>
        public QAMockTaskForIntegrationTests()
        {
            _taskShouldError = false;
            _taskShouldThrow = false;
            _expectedOutput = String.Empty;
        }


        #region ITask Members

        /// <summary>
        /// BuildEngine that can be used to access some engine capabilities like building project
        /// </summary>
        public Microsoft.Build.Framework.IBuildEngine BuildEngine
        {
            get
            {
                return _buildEngine;
            }
            set
            {
                _buildEngine = value;
            }
        }

        /// <summary>
        /// Task Host
        /// </summary>
        public Microsoft.Build.Framework.ITaskHost HostObject
        {
            get
            {
                return _taskHost;
            }
            set
            {
                _taskHost = value;
            }
        }

        /// <summary>
        /// Expected output to populate
        /// </summary>
        public string ExpectedOutput
        {
            set
            {
                _expectedOutput = value;
            }
        }

        /// <summary>
        /// If the task should succeed or fail
        /// </summary>
        public bool TaskShouldError
        {
            set
            {
                _taskShouldError = value;
            }
        }

        /// <summary>
        /// Expected output to populate
        /// </summary>
        public bool TaskShouldThrowException
        {
            set
            {
                _taskShouldThrow = value;
            }
        }

        /// <summary>
        /// Output from the task which is set to the expected output
        /// </summary>
        [Microsoft.Build.Framework.Output]
        public string TaskOutput
        {
            get
            {
                return _expectedOutput;
            }
        }

        /// <summary>
        /// Execution of the task
        /// </summary>
        public bool Execute()
        {
            if (_taskShouldThrow)
            {
                throw new QAMockTaskForIntegrationTestsException();
            }

            if (_taskShouldError)
            {
                return false;
            }

            return true;
        }

        #endregion
    }

    /// <summary>
    /// Exception object for the mock task 
    /// </summary>
    [Serializable]
    public class QAMockTaskForIntegrationTestsException : Exception
    {
        public QAMockTaskForIntegrationTestsException()
            : base("QAMockTaskForIntegrationTestsException")
        {
        }
    }
}