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

        private IBuildComponentHost host;
        private ITestDataProvider testDataProvider;
        private IResultsCache resultsCache;
        private IConfigCache configCache;
        private Thread builderThread;
        private BuildRequestEntry requestedEntry;
        private AutoResetEvent continueEvent;
        private AutoResetEvent cancelEvent;
        private ManualResetEvent threadStarted;
        private RequestDefinition currentProjectDefinition;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor that takes in nothing.
        /// </summary>
        public QARequestBuilder()
        {
            this.host = null;
            this.configCache = null;
            this.resultsCache = null;
            this.builderThread = null;
            this.requestedEntry = null;
            this.cancelEvent = new AutoResetEvent(false);
            this.continueEvent = new AutoResetEvent(false);
            this.threadStarted = new ManualResetEvent(false);
            this.currentProjectDefinition = null;
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
            this.host = host;
            this.resultsCache = (IResultsCache)(this.host.GetComponent(BuildComponentType.ResultsCache));
            this.configCache = (IConfigCache)(this.host.GetComponent(BuildComponentType.ConfigCache));
            this.testDataProvider = (ITestDataProvider)(this.host.GetComponent(BuildComponentType.TestDataProvider));
        }

        /// <summary>
        /// The component is shutting down
        /// </summary>
        public void ShutdownComponent()
        {
            this.requestedEntry = null;
            this.currentProjectDefinition = null;
        }

        #endregion

        #region IRequestBuilder Members

        /// <summary>
        /// Build a request entry
        /// </summary>
        /// <param name="entry"></param>
        public void BuildRequest(NodeLoggingContext nodeLoggingContext, BuildRequestEntry entry)
        {

            this.requestedEntry = entry;
            if (null == this.requestedEntry.RequestConfiguration.Project)
            {
                Project mockProject = new Project(XmlReader.Create(new System.IO.StringReader(
@"<Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
            <Target Name='t'>
            </Target>
            </Project>")));
                this.requestedEntry.RequestConfiguration.Project = mockProject.CreateProjectInstance();
            }

            this.currentProjectDefinition = this.testDataProvider[this.requestedEntry.Request.ConfigurationId];
            this.requestedEntry.Continue();
            this.builderThread = new Thread(BuilderThreadProc);
            this.builderThread.Name = "Builder Thread for Request: " + entry.Request.ConfigurationId.ToString();
            this.builderThread.Start();
        }

        /// <summary>
        /// Resume a request which was waiting or is new
        /// </summary>
        public void ContinueRequest()
        {
            this.threadStarted.WaitOne();
            this.continueEvent.Set();
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
            this.threadStarted.WaitOne();
            this.cancelEvent.Set();
        }

        /// <summary>
        /// Waits for the cancellation until it's completed, and cleans up the internal states.
        /// </summary>
        public void WaitForCancelCompletion()
        {
            this.builderThread.Join();
            this.cancelEvent.Close();
            this.continueEvent.Close();
            this.threadStarted.Close();
            this.builderThread = null;
        }

        #endregion

        #region Private methods
        
        /// <summary>
        /// Thread to process the build request
        /// </summary>
        private void BuilderThreadProc()
        {
            bool completeSuccess = true;
            WaitHandle[] handles = new WaitHandle[2] { cancelEvent, continueEvent };

            this.threadStarted.Set();

            // Add a request for each of the referenced projects. All we need to do is to make sure that the new project definition for the referenced
            // project has been added to the host collection

            FullyQualifiedBuildRequest[] fq = new FullyQualifiedBuildRequest[this.currentProjectDefinition.ChildDefinitions.Count];
            int fqCount = 0;
            foreach(RequestDefinition childDefinition in this.currentProjectDefinition.ChildDefinitions)
            {
                BuildRequestConfiguration unresolvedConfig = childDefinition.UnresolvedConfiguration;
                fq[fqCount++] = new FullyQualifiedBuildRequest(unresolvedConfig, childDefinition.TargetsToBuild, true);
            }

            try
            {
                // Check to see if there was a cancel before we do anything
                if (cancelEvent.WaitOne(1, false))
                {
                    HandleCancel();
                    return;
                }

                // Submit the build request for the references if we have any
                if (fqCount > 0)
                {
                    OnNewBuildRequests(this.requestedEntry, fq);

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
                        IDictionary<int, BuildResult> results = requestedEntry.Continue();
                        foreach (BuildResult configResult in results.Values)
                        {
                            if (configResult.OverallResult == BuildResultCode.Failure)
                            {
                                completeSuccess = false;
                            }
                            else
                            {
                                this.resultsCache.AddResult(configResult);
                            }
                        }
                    }
                }

                // Check to see if there was a cancel we process the final result
                if (cancelEvent.WaitOne(1, false))
                {
                    HandleCancel();
                    return;
                }

                // Simulate execution time for the actual entry if one was specified and if the entry built successfully
                if (this.currentProjectDefinition.ExecutionTime > 0 && completeSuccess == true)
                {
                    Thread.Sleep(this.currentProjectDefinition.ExecutionTime);
                }

                // Create and send the result
                BuildResult result = new BuildResult(requestedEntry.Request);

                // No specific target was asked to build. Return the default result
                if (requestedEntry.Request.Targets.Count == 0)
                {
                    result.AddResultsForTarget(RequestDefinition.defaultTargetName, new TargetResult(new TaskItem[1], completeSuccess ? TestUtilities.GetSuccessResult() : TestUtilities.GetStopWithErrorResult()));
                }
                else
                {
                    foreach (string target in requestedEntry.Request.Targets)
                    {
                        result.AddResultsForTarget(target, new TargetResult(new TaskItem[1], completeSuccess ? TestUtilities.GetSuccessResult() : TestUtilities.GetStopWithErrorResult()));
                    }
                }

                this.resultsCache.AddResult(result);
                this.requestedEntry.Complete(result);
                RaiseRequestComplete(this.requestedEntry);
                return;
            }

            catch (Exception e)
            {
                if (this.requestedEntry != null)
                {
                    string message = String.Format("Test: Unhandeled exception occured: \nMessage: {0} \nStack:\n{1}", e.Message, e.StackTrace);
                    BuildResult errorResult = new BuildResult(this.requestedEntry.Request, new InvalidOperationException(message));
                    this.requestedEntry.Complete(errorResult);
                    RaiseRequestComplete(this.requestedEntry);
                }
            }
            
        }

        /// <summary>
        /// Process the approprate action if the cancel event was set
        /// </summary>
        private void HandleCancel()
        {
            BuildResult res = new BuildResult(this.requestedEntry.Request, new BuildAbortedException());
            this.requestedEntry.Complete(res);
            RaiseRequestComplete(this.requestedEntry);
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