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
using Microsoft.Build.Collections;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using NodeLoggingContext = Microsoft.Build.BackEnd.Logging.NodeLoggingContext;
using Microsoft.Build.Unittest;

namespace Microsoft.Build.UnitTests.QA
{
    /// <summary>
    /// Mock implementation of the RequestBuilder component. RequestBuilder exists for each BuildRequestEntry
    /// </summary>
    internal class QARequestBuilder : IRequestBuilder, IBuildComponent
    {
        #region Data members

        private IBuildComponentHost _host;
        private ITestDataProvider _testDataProvider;
        private IResultsCache _resultsCache;
        private IConfigCache _configCache;
        private Thread _builderThread;
        private BuildRequestEntry _requestedEntry;
        private AutoResetEvent _continueEvent;
        private AutoResetEvent _cancelEvent;
        private ManualResetEvent _threadStarted;
        private RequestDefinition _currentProjectDefinition;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor that takes in nothing.
        /// </summary>
        public QARequestBuilder()
        {
            _host = null;
            _configCache = null;
            _resultsCache = null;
            _builderThread = null;
            _requestedEntry = null;
            _cancelEvent = new AutoResetEvent(false);
            _continueEvent = new AutoResetEvent(false);
            _threadStarted = new ManualResetEvent(false);
            _currentProjectDefinition = null;
        }

        #endregion

        #region Events

        /// <summary>
        /// Used when a new BuildRequest is to be sent
        /// </summary>
        public event NewBuildRequestsDelegate OnNewBuildRequests;

        /// <summary>
        /// Called when a BuildRequest is completed
        /// </summary>
        public event BuildRequestCompletedDelegate OnBuildRequestCompleted;

        /// <summary>
        /// Called when a BuildRequest is blocked.
        /// </summary>
        public event BuildRequestBlockedDelegate OnBuildRequestBlocked;

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// The component is being initialized
        /// </summary>
        public void InitializeComponent(IBuildComponentHost host)
        {
            _host = host;
            _resultsCache = (IResultsCache)(_host.GetComponent(BuildComponentType.ResultsCache));
            _configCache = (IConfigCache)(_host.GetComponent(BuildComponentType.ConfigCache));
            _testDataProvider = (ITestDataProvider)(_host.GetComponent(BuildComponentType.TestDataProvider));
        }

        /// <summary>
        /// The component is shutting down
        /// </summary>
        public void ShutdownComponent()
        {
            _requestedEntry = null;
            _currentProjectDefinition = null;
        }

        #endregion

        #region IRequestBuilder Members

        /// <summary>
        /// Build a request entry
        /// </summary>
        /// <param name="entry"></param>
        public void BuildRequest(NodeLoggingContext nodeLoggingContext, BuildRequestEntry entry)
        {
            _requestedEntry = entry;
            if (null == _requestedEntry.RequestConfiguration.Project)
            {
                Project mockProject = new Project(XmlReader.Create(new System.IO.StringReader(
@"<Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
            <Target Name='t'>
            </Target>
            </Project>")));
                _requestedEntry.RequestConfiguration.Project = mockProject.CreateProjectInstance();
            }

            _currentProjectDefinition = _testDataProvider[_requestedEntry.Request.ConfigurationId];
            _requestedEntry.Continue();
            _builderThread = new Thread(BuilderThreadProc);
            _builderThread.Name = "Builder Thread for Request: " + entry.Request.ConfigurationId.ToString();
            _builderThread.Start();
        }

        /// <summary>
        /// Resume a request which was waiting or is new
        /// </summary>
        public void ContinueRequest()
        {
            _threadStarted.WaitOne();
            _continueEvent.Set();
        }

        /// <summary>
        /// Cancel the request that we are processing
        /// </summary>
        public void CancelRequest()
        {
            this.BeginCancel();
            this.WaitForCancelCompletion();
        }

        /// <summary>
        /// Starts to cancel an existing request.
        /// </summary>
        public void BeginCancel()
        {
            _threadStarted.WaitOne();
            _cancelEvent.Set();
        }

        /// <summary>
        /// Waits for the cancellation until it's completed, and cleans up the internal states.
        /// </summary>
        public void WaitForCancelCompletion()
        {
            _builderThread.Join();
            _cancelEvent.Close();
            _continueEvent.Close();
            _threadStarted.Close();
            _builderThread = null;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Thread to process the build request
        /// </summary>
        private void BuilderThreadProc()
        {
            bool completeSuccess = true;
            WaitHandle[] handles = new WaitHandle[2] { _cancelEvent, _continueEvent };

            _threadStarted.Set();

            // Add a request for each of the referenced projects. All we need to do is to make sure that the new project definition for the referenced
            // project has been added to the host collection

            FullyQualifiedBuildRequest[] fq = new FullyQualifiedBuildRequest[_currentProjectDefinition.ChildDefinitions.Count];
            int fqCount = 0;
            foreach (RequestDefinition childDefinition in _currentProjectDefinition.ChildDefinitions)
            {
                BuildRequestConfiguration unresolvedConfig = childDefinition.UnresolvedConfiguration;
                fq[fqCount++] = new FullyQualifiedBuildRequest(unresolvedConfig, childDefinition.TargetsToBuild, true);
            }

            try
            {
                // Check to see if there was a cancel before we do anything
                if (_cancelEvent.WaitOne(1, false))
                {
                    HandleCancel();
                    return;
                }

                // Submit the build request for the references if we have any
                if (fqCount > 0)
                {
                    OnNewBuildRequests(_requestedEntry, fq);

                    // Wait for all of them to complete till our entry is marked ready
                    int evt = WaitHandle.WaitAny(handles);

                    // If a cancel occurs then we are done. Set the result to an exception
                    if (evt == 0)
                    {
                        HandleCancel();
                        return;
                    }

                    // If we get a continue then one of the reference has complete. Set the result in the cache only in case of success.
                    // Even though there may have been error - we cannot abandone the loop as there are already 
                    // requests in progress which may call back to this thread
                    else if (evt == 1)
                    {
                        IDictionary<int, BuildResult> results = _requestedEntry.Continue();
                        foreach (BuildResult configResult in results.Values)
                        {
                            if (configResult.OverallResult == BuildResultCode.Failure)
                            {
                                completeSuccess = false;
                            }
                            else
                            {
                                _resultsCache.AddResult(configResult);
                            }
                        }
                    }
                }

                // Check to see if there was a cancel we process the final result
                if (_cancelEvent.WaitOne(1, false))
                {
                    HandleCancel();
                    return;
                }

                // Simulate execution time for the actual entry if one was specified and if the entry built successfully
                if (_currentProjectDefinition.ExecutionTime > 0 && completeSuccess == true)
                {
                    Thread.Sleep(_currentProjectDefinition.ExecutionTime);
                }

                // Create and send the result
                BuildResult result = new BuildResult(_requestedEntry.Request);

                // No specific target was asked to build. Return the default result
                if (_requestedEntry.Request.Targets.Count == 0)
                {
                    result.AddResultsForTarget(RequestDefinition.defaultTargetName, new TargetResult(new TaskItem[1], completeSuccess ? TestUtilities.GetSuccessResult() : TestUtilities.GetStopWithErrorResult()));
                }
                else
                {
                    foreach (string target in _requestedEntry.Request.Targets)
                    {
                        result.AddResultsForTarget(target, new TargetResult(new TaskItem[1], completeSuccess ? TestUtilities.GetSuccessResult() : TestUtilities.GetStopWithErrorResult()));
                    }
                }

                _resultsCache.AddResult(result);
                _requestedEntry.Complete(result);
                RaiseRequestComplete(_requestedEntry);
                return;
            }

            catch (Exception e)
            {
                if (_requestedEntry != null)
                {
                    string message = String.Format("Test: Unhandeled exception occured: \nMessage: {0} \nStack:\n{1}", e.Message, e.StackTrace);
                    BuildResult errorResult = new BuildResult(_requestedEntry.Request, new InvalidOperationException(message));
                    _requestedEntry.Complete(errorResult);
                    RaiseRequestComplete(_requestedEntry);
                }
            }
        }

        /// <summary>
        /// Process the approprate action if the cancel event was set
        /// </summary>
        private void HandleCancel()
        {
            BuildResult res = new BuildResult(_requestedEntry.Request, new BuildAbortedException());
            _requestedEntry.Complete(res);
            RaiseRequestComplete(_requestedEntry);
        }

        /// <summary>
        /// Raises the request completed event
        /// </summary>
        /// <param name="entry">The entry.</param>
        private void RaiseRequestComplete(BuildRequestEntry entry)
        {
            if (OnBuildRequestCompleted != null)
            {
                OnBuildRequestCompleted(entry);
            }
        }

        /// <summary>
        /// Raises the request blocked event.
        /// </summary>
        public void RaiseRequestBlocked(BuildRequestEntry entry, int blockingId, string blockingTarget)
        {
            if (null != OnBuildRequestBlocked)
            {
                OnBuildRequestBlocked(entry, blockingId, blockingTarget);
            }
        }

        #endregion
    }
}