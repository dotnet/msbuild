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
using Microsoft.Build.Collections;

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
    public class TaskBuilder_Tests
    {
        #region Data members

        private Common_Tests _commonTests;
        private ResultsCache _resultsCache;

        #endregion

        #region Constructor

        /// <summary>
        /// Setup the object
        /// </summary>
        public TaskBuilder_Tests()
        {
            _commonTests = new Common_Tests(this.GetComponent, true);
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


        #region Common Tests for Callback testing

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void ReferenceAProjectAlreadyBuiltInTheNode()
        {
            _commonTests.ReferenceAProjectAlreadyBuiltInTheNode();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentToolsVersion()
        {
            _commonTests.ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentToolsVersion();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentGlobalProperties()
        {
            _commonTests.ReferenceAProjectAlreadyBuiltInTheNodeButWithDifferentGlobalProperties();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildOneProjectWith1Reference()
        {
            _commonTests.BuildOneProjectWith1Reference();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildOneProjectWith3Reference()
        {
            _commonTests.BuildOneProjectWith3Reference();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildOneProjectWith3ReferenceWhere2AreTheSame()
        {
            _commonTests.BuildOneProjectWith3ReferenceWhere2AreTheSame();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithMiddleProjectHavingReferenceToANewProject()
        {
            _commonTests.BuildMultipleProjectsWithMiddleProjectHavingReferenceToANewProject();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithTheFirstProjectHavingReferenceToANewProject()
        {
            _commonTests.BuildMultipleProjectsWithTheFirstProjectHavingReferenceToANewProject();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithTheLastProjectHavingReferenceToANewProject()
        {
            _commonTests.BuildMultipleProjectsWithTheLastProjectHavingReferenceToANewProject();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithEachReferencingANewProject()
        {
            _commonTests.BuildMultipleProjectsWithEachReferencingANewProject();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWhereFirstReferencesMultipleNewProjects()
        {
            _commonTests.BuildMultipleProjectsWhereFirstReferencesMultipleNewProjects();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWhereFirstAndLastReferencesMultipleNewProjects()
        {
            _commonTests.BuildMultipleProjectsWhereFirstAndLastReferencesMultipleNewProjects();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithReferencesWhereSomeReferencesAreAlreadyBuilt()
        {
            _commonTests.BuildMultipleProjectsWithReferencesWhereSomeReferencesAreAlreadyBuilt();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void BuildMultipleProjectsWithReferencesAndDifferentToolsVersion()
        {
            _commonTests.BuildMultipleProjectsWithReferencesAndDifferentToolsVersion();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void Build3ProjectsWhere1HasAReferenceTo3()
        {
            _commonTests.Build3ProjectsWhere1HasAReferenceTo3();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void Build3ProjectsWhere2HasAReferenceTo3()
        {
            _commonTests.Build3ProjectsWhere2HasAReferenceTo3();
        }

        /// <summary>
        /// Delegate to common tests
        /// </summary>
        [TestMethod]
        public void Build3ProjectsWhere3HasAReferenceTo1()
        {
            _commonTests.Build3ProjectsWhere3HasAReferenceTo1();
        }

        #endregion
    }
}