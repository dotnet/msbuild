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

        private Common_Tests commonTests;
        private ResultsCache resultsCache;
        private string assemblyPath;
        private string tempPath;

        #endregion

        #region Constructor

        /// <summary>
        /// Setup the object
        /// </summary>
        public Integration_Tests()
        {
            this.commonTests = new Common_Tests(this.GetComponent, true);
            this.resultsCache = null;
            this.tempPath = System.IO.Path.GetTempPath();
            this.assemblyPath = Path.GetDirectoryName(
                new Uri(System.Reflection.Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath);
            this.assemblyPath = Path.Combine(this.assemblyPath, "Microsoft.Build.Unittest.dll");
        }

        #endregion

        #region Common

        /// <summary>
        /// Delegate to common test setup
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            this.resultsCache = new ResultsCache();
            this.commonTests.Setup();
        }

        /// <summary>
        /// Delegate to common test teardown
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            this.commonTests.TearDown();
            this.resultsCache = null;
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
                    return (IBuildComponent)this.resultsCache;

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
                        this.assemblyPath);

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
                        this.assemblyPath);

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
                        this.assemblyPath);

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
            this.assemblyPath);

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
            this.assemblyPath);

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
            