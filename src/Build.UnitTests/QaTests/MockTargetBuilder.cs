// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Unittest;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using ProjectLoggingContext = Microsoft.Build.BackEnd.Logging.ProjectLoggingContext;
using System.Threading.Tasks;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// The mock component TargetBuilder object.
    /// </summary>
    internal class QAMockTargetBuilder : ITargetBuilder, IBuildComponent
    {
        /// <summary>
        /// The component host.
        /// </summary>
        private IBuildComponentHost _host;

        /// <summary>
        /// The BuildRequestEntry for which we are building targets.
        /// </summary>
        private BuildRequestEntry _requestEntry;

        /// <summary>
        /// The project logging context
        /// </summary>
        private ProjectLoggingContext _projectLoggingContext;

        /// <summary>
        /// Request Callback
        /// </summary>
        private IRequestBuilderCallback _requestCallBack;

        /// <summary>
        /// The test data provider
        /// </summary>
        private ITestDataProvider _testDataProvider;

        /// <summary>
        /// Test definition associated with the project that we are building
        /// </summary>
        private RequestDefinition _testDefinition;

        /// <summary>
        /// Event to notify that the build has been completed
        /// </summary>
        private AutoResetEvent _buildDone;

        /// <summary>
        /// The cancellation token
        /// </summary>
        private CancellationToken _cancellationToken;

        public QAMockTargetBuilder()
        {
            _host = null;
            _testDataProvider = null;
            _testDefinition = null;
            _requestCallBack = null;
            _requestEntry = null;
            _projectLoggingContext = null;
            _buildDone = new AutoResetEvent(false);
        }

        /// <summary>
        /// Builds the specified targets of an entry. The cancel event should only be set to true if we are planning
        /// on simulating execution time when a target is built
        /// </summary>
        public Task<BuildResult> BuildTargets(ProjectLoggingContext loggingContext, BuildRequestEntry entry, IRequestBuilderCallback callback, string[] targetNames, Lookup baseLookup, CancellationToken cancellationToken)
        {
            _requestEntry = entry;
            _projectLoggingContext = loggingContext;
            _requestCallBack = callback;
            _testDefinition = _testDataProvider[entry.Request.ConfigurationId];
            _cancellationToken = cancellationToken;
            BuildResult result = GenerateResults(targetNames);

            return Task<BuildResult>.FromResult(result);
        }

        #region IBuildComponent Members

        /// <summary>
        /// Sets the component host.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            _host = host;
            _testDataProvider = (ITestDataProvider)host.GetComponent(BuildComponentType.TestDataProvider);
        }

        /// <summary>
        /// Shuts down the component.
        /// </summary>
        public void ShutdownComponent()
        {
            _host = null;
            _testDataProvider = null;
            _testDefinition = null;
            _requestCallBack = null;
            _requestEntry = null;
            _projectLoggingContext = null;
        }

        /// <summary>
        /// Returns the tools version associated which the project configuration
        /// </summary>
        public string GetToolsVersion(string filename, string elementname, string attributename)
        {
            return _testDefinition.ToolsVersion;
        }

        #endregion

        #region Private Method

        /// <summary>
        /// Generate results for the targets requested to be built. Using the TestDataProvider also simulate any
        /// P2P callbacks on the first target. In order to test the cancels there is also functionality to allow the
        /// target execution to wait on a cancel event before exiting
        /// </summary>
        private BuildResult GenerateResults(string[] targetNames)
        {
            bool simulatedResults = false;
            BuildResult result = new BuildResult(_requestEntry.Request);
            foreach (string target in targetNames)
            {
                if (!simulatedResults)
                {
                    SimulateCallBacks();
                    simulatedResults = true;
                }

                // Wait for this to be cancelled
                if (_testDefinition.WaitForCancel)
                {
                    _cancellationToken.WaitHandle.WaitOne();
                    _buildDone.Set();
                    throw new BuildAbortedException();
                }

                if (_testDefinition.ExecutionTime > 0)
                {
                    Thread.Sleep(_testDefinition.ExecutionTime);
                }

                TaskItem[] items = new TaskItem[] { new TaskItem("itemValue", _requestEntry.RequestConfiguration.ProjectFullPath) };
                TargetResult targetResult = new TargetResult(items, TestUtilities.GetSuccessResult());
                result.AddResultsForTarget(target, targetResult);
            }

            _buildDone.Set();
            return result;
        }

        /// <summary>
        /// Simulates callback. Access the configuration for the primary project. Retrieve the test test data definition.
        /// Get the child definitions if available and simulate a callback for each of the child definitions. Note that the 
        /// targets to build parameter is the same for all the projects - that is we instruct to build the same set of targets
        /// for all of the projects. Thus the child test definitions of the entry should have the same set of targets available
        /// or a common set of targets available
        /// </summary>
        private void SimulateCallBacks()
        {
            if (_testDefinition.ChildDefinitions == null || _testDefinition.ChildDefinitions.Count < 1)
            {
                return;
            }

            int count = _testDefinition.ChildDefinitions.Count;
            string[] projectFiles = new string[count];
            PropertyDictionary<ProjectPropertyInstance>[] properties = new PropertyDictionary<ProjectPropertyInstance>[count];
            string[] toolsVersions = new string[count];
            string[] targetsToBuild = null;

            count = 0;
            foreach (RequestDefinition d in _testDefinition.ChildDefinitions)
            {
                projectFiles[count] = d.FileName;
                properties[count] = d.GlobalProperties;
                toolsVersions[count] = d.ToolsVersion;
                targetsToBuild = d.TargetsToBuild;
                count++;
            }

            _requestCallBack.BuildProjects(projectFiles, properties, toolsVersions, targetsToBuild, true);
        }

        #endregion
    }
}