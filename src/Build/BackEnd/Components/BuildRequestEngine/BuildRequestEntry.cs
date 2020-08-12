// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Shared;
using Microsoft.Build.Execution;
using System.Diagnostics;

using BuildAbortedException = Microsoft.Build.Exceptions.BuildAbortedException;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Delegate is called when the state for a build request entry has changed.
    /// </summary>
    /// <param name="entry">The entry whose state has changed.</param>
    /// <param name="newState">The new state value.</param>
    internal delegate void BuildRequestEntryStateChangedDelegate(BuildRequestEntry entry, BuildRequestEntryState newState);

    /// <summary>
    /// The set of states in which a build request entry can be.
    /// </summary>
    internal enum BuildRequestEntryState
    {
        /// <summary>
        /// There should only ever be one entry in the Active state.  This is the request which is
        /// being actively built by the engine - i.e. it has a running task thread.  All other requests
        /// must be in one of the other states.  When in this state, the outstandingRequest and
        /// receivedResult members must be null.
        /// 
        /// Transitions: 
        ///     Waiting:  When an msbuild callback is made the active build request needs to wait
        ///               for the results in order to continue to process.
        ///     Complete: The build request has generated all of the required results.
        /// </summary>
        Active,

        /// <summary>
        /// This state means the node has received all of the results needed to continue processing this
        /// request.  When this state is set, the receivedResult member of this entry must be non-null.  
        /// The request engine can continue it at some later point when it is no longer busy.
        /// Any number of entries may be in this state.
        /// 
        /// Transitions:
        ///         Active: The build request engine picks this ready request to process.
        /// </summary>
        Ready,

        /// <summary>
        /// This state means the node is waiting for results from outstanding build requests.  When this 
        /// state is set, the outstandingRequest or outstandingConfiguration members of the entry 
        /// must be non-null.
        /// 
        /// Transitions: 
        ///           Ready: All of the results which caused the build request to wait have been received
        /// </summary>
        Waiting,

        /// <summary>
        /// This state means the request has completed and results are available.  The engine will remove
        /// the request from the list and the results will be returned to the node for processing.
        /// 
        /// Transitions: None, this is the final state of the build request
        /// </summary>
        Complete
    }

    /// <summary>
    /// BuildRequestEntry holds a build request and associated state data.
    /// </summary>
    internal class BuildRequestEntry
    {
        /// <summary>
        /// Mapping of Build Request Configurations to Build Requests waiting for configuration resolution.
        /// </summary>
        private Dictionary<int, List<BuildRequest>> _unresolvedConfigurations;

        /// <summary>
        /// The set of requests to issue.  This holds all of the requests as we prepare them.  Once their configurations
        /// have all been resolved, we will issue them to the Scheduler in the order received.
        /// </summary>
        private List<BuildRequest> _requestsToIssue;

        /// <summary>
        /// The list of unresolved configurations we need to issue.
        /// </summary>
        private List<BuildRequestConfiguration> _unresolvedConfigurationsToIssue;

        /// <summary>
        /// Mapping of nodeRequestIDs to Build Requests waiting for results.
        /// </summary>
        private Dictionary<int, BuildRequest> _outstandingRequests;

        /// <summary>
        /// Mapping of nodeRequestIDs to Build Results.
        /// </summary>
        private Dictionary<int, BuildResult> _outstandingResults;

        /// <summary>
        /// The ID of the request we are blocked waiting for.
        /// </summary>
        private int _blockingGlobalRequestId;

        /// <summary>
        /// The object used to build this request.
        /// </summary>
        private IRequestBuilder _requestBuilder;

        /// <summary>
        /// The project's root directory.
        /// </summary>
        private string _projectRootDirectory;

        /// <summary>
        /// Creates a build request entry from a build request.
        /// </summary>
        /// <param name="request">The originating build request.</param>
        /// <param name="requestConfiguration">The build request configuration.</param>
        internal BuildRequestEntry(BuildRequest request, BuildRequestConfiguration requestConfiguration)
        {
            ErrorUtilities.VerifyThrowArgumentNull(request, nameof(request));
            ErrorUtilities.VerifyThrowArgumentNull(requestConfiguration, nameof(requestConfiguration));
            ErrorUtilities.VerifyThrow(requestConfiguration.ConfigurationId == request.ConfigurationId, "Configuration id mismatch");

            GlobalLock = new Object();
            Request = request;
            RequestConfiguration = requestConfiguration;
            _blockingGlobalRequestId = BuildRequest.InvalidGlobalRequestId;
            Result = null;
            ChangeState(BuildRequestEntryState.Ready);
        }

        /// <summary>
        /// Raised when the state changes.
        /// </summary>
        public event BuildRequestEntryStateChangedDelegate OnStateChanged;

        /// <summary>
        /// Returns the object used to lock for synchronization of long-running operations.
        /// </summary>
        public Object GlobalLock { get; }

        /// <summary>
        /// Returns the root directory for the project being built by this request.
        /// </summary>
        public string ProjectRootDirectory => _projectRootDirectory ??
                                              (_projectRootDirectory = Path.GetDirectoryName(RequestConfiguration.ProjectFullPath));

        /// <summary>
        /// Returns the current state of the build request.
        /// </summary>
        public BuildRequestEntryState State { get; private set; }

        /// <summary>
        /// Returns the request which originated this entry.
        /// </summary>
        public BuildRequest Request { get; }

        /// <summary>
        /// Returns the build request configuration
        /// </summary>
        public BuildRequestConfiguration RequestConfiguration { get; }

        /// <summary>
        /// Returns the overall result for this request.
        /// </summary>
        public BuildResult Result { get; private set; }

        /// <summary>
        /// Returns the request builder.
        /// </summary>
        public IRequestBuilder Builder
        {
            [DebuggerStepThrough]
            get => _requestBuilder;

            [DebuggerStepThrough]
            set
            {
                ErrorUtilities.VerifyThrow(value == null || _requestBuilder == null, "Request Builder already set.");
                _requestBuilder = value;
            }
        }

        /// <summary>
        /// Informs the entry that it has configurations which need to be resolved.
        /// </summary>
        /// <param name="configuration">The configuration to be resolved.</param>
        public void WaitForConfiguration(BuildRequestConfiguration configuration)
        {
            ErrorUtilities.VerifyThrow(configuration.WasGeneratedByNode, "Configuration has already been resolved.");

            _unresolvedConfigurationsToIssue ??= new List<BuildRequestConfiguration>();
            _unresolvedConfigurationsToIssue.Add(configuration);
        }

        /// <summary>
        /// Waits for a result from a request.
        /// </summary>
        /// <param name="newRequest">The build request</param>
        public void WaitForResult(BuildRequest newRequest)
        {
            WaitForResult(newRequest, true);
        }

        /// <summary>
        /// Signals that we are waiting for a specific blocking request to finish.
        /// </summary>
        public void WaitForBlockingRequest(int blockingGlobalRequestId)
        {
            lock (GlobalLock)
            {
                ErrorUtilities.VerifyThrow(State == BuildRequestEntryState.Active, "Must be in Active state to wait for blocking request.  Config: {0} State: {1}", RequestConfiguration.ConfigurationId, State);

                _blockingGlobalRequestId = blockingGlobalRequestId;

                ChangeState(BuildRequestEntryState.Waiting);
            }
        }

        /// <summary>
        /// Waits for a result from a request which previously had an unresolved configuration.
        /// </summary>
        /// <param name="unresolvedConfigId">The id of the unresolved configuration.</param>
        /// <param name="configId">The id of the resolved configuration.</param>
        /// <returns>True if all unresolved configurations have been resolved, false otherwise.</returns>
        public bool ResolveConfigurationRequest(int unresolvedConfigId, int configId)
        {
            lock (GlobalLock)
            {
                if (_unresolvedConfigurations?.ContainsKey(unresolvedConfigId) != true)
                {
                    return false;
                }

                List<BuildRequest> requests = _unresolvedConfigurations[unresolvedConfigId];
                _unresolvedConfigurations.Remove(unresolvedConfigId);

                if (_unresolvedConfigurations.Count == 0)
                {
                    _unresolvedConfigurations = null;
                }

                foreach (BuildRequest request in requests)
                {
                    request.ResolveConfiguration(configId);
                    WaitForResult(request, false);
                }

                return _unresolvedConfigurations == null;
            }
        }

        /// <summary>
        /// Returns the set of build requests which should be issued to the scheduler.
        /// </summary>
        public List<BuildRequest> GetRequestsToIssueIfReady()
        {
            if (_unresolvedConfigurations == null && _requestsToIssue != null)
            {
                List<BuildRequest> requests = _requestsToIssue;
                _requestsToIssue = null;

                return requests;
            }

            return null;
        }

        /// <summary>
        /// Returns the list of unresolved configurations to issue.
        /// </summary>
        public List<BuildRequestConfiguration> GetUnresolvedConfigurationsToIssue()
        {
            if (_unresolvedConfigurationsToIssue != null)
            {
                List<BuildRequestConfiguration> configurationsToIssue = _unresolvedConfigurationsToIssue;
                _unresolvedConfigurationsToIssue = null;
                return configurationsToIssue;
            }

            return null;
        }

        /// <summary>
        /// Returns the list of currently active targets.
        /// </summary>
        public string[] GetActiveTargets()
        {
            var activeTargets = new string[RequestConfiguration.ActivelyBuildingTargets.Count];

            int index = 0;
            foreach (string target in RequestConfiguration.ActivelyBuildingTargets.Keys)
            {
                activeTargets[index++] = target;
            }

            return activeTargets;
        }

        /// <summary>
        /// This reports a result for a request on which this entry was waiting.
        /// PERF: Once we have fixed up all the result reporting, we can probably
        /// optimize this.  See the comment in BuildRequestEngine.ReportBuildResult.
        /// </summary>
        /// <param name="result">The result for the request.</param>
        public void ReportResult(BuildResult result)
        {
            lock (GlobalLock)
            {
                ErrorUtilities.VerifyThrowArgumentNull(result, nameof(result));
                ErrorUtilities.VerifyThrow(State == BuildRequestEntryState.Waiting || _outstandingRequests == null, "Entry must be in the Waiting state to report results, or we must have flushed our requests due to an error. Config: {0} State: {1} Requests: {2}", RequestConfiguration.ConfigurationId, State, _outstandingRequests != null);

                // If the matching request is in the issue list, remove it so we don't try to ask for it to be built.
                if (_requestsToIssue != null)
                {
                    for (int i = 0; i < _requestsToIssue.Count; i++)
                    {
                        if (_requestsToIssue[i].NodeRequestId == result.NodeRequestId)
                        {
                            _requestsToIssue.RemoveAt(i);
                            if (_requestsToIssue.Count == 0)
                            {
                                _requestsToIssue = null;
                            }

                            break;
                        }
                    }
                }

                // If this result is for the request we were blocked on locally (target re-entrancy) clear out our blockage.
                bool addResults = false;
                if (_blockingGlobalRequestId == result.GlobalRequestId)
                {
                    _blockingGlobalRequestId = BuildRequest.InvalidGlobalRequestId;
                    if (_outstandingRequests == null)
                    {
                        ErrorUtilities.VerifyThrow(result.CircularDependency, "Received result for target in progress and it wasn't a circular dependency error.");
                        addResults = true;
                    }
                }

                // We could be in the waiting state but waiting on configurations instead of results, or we received a circular dependency
                // result, which blows away everything else we were waiting on.
                if (_outstandingRequests != null)
                {
                    _outstandingRequests.Remove(result.NodeRequestId);

                    // If we wish to implement behavior where we stop building after the first failing request, then check for 
                    // overall results being failure rather than just circular dependency. Sync with BasicScheduler.ReportResult and
                    // BasicScheduler.ReportRequestBlocked.
                    if (result.CircularDependency || (_outstandingRequests.Count == 0 && (_unresolvedConfigurations == null || _unresolvedConfigurations.Count == 0)))
                    {
                        _outstandingRequests = null;
                        _unresolvedConfigurations = null;

                        // If we are in the middle of IssueBuildRequests and collecting requests (and cached results), one of those results
                        // was a failure.  As a result, this entry will fail, and submitting further requests from it would be pointless.
                        _requestsToIssue = null;
                        _unresolvedConfigurationsToIssue = null;
                    }

                    addResults = true;
                }

                if (addResults)
                {
                    // Update the local results record
                    _outstandingResults ??= new Dictionary<int, BuildResult>();
                    ErrorUtilities.VerifyThrow(!_outstandingResults.ContainsKey(result.NodeRequestId), "Request already contains results.");
                    _outstandingResults.Add(result.NodeRequestId, result);
                }

                // If we are out of outstanding requests, we are ready to continue.
                if (_outstandingRequests == null && _unresolvedConfigurations == null && _blockingGlobalRequestId == BuildRequest.InvalidGlobalRequestId)
                {
                    ChangeState(BuildRequestEntryState.Ready);
                }
            }
        }

        /// <summary>
        /// Unblocks an entry which was waiting for a specific global request id.
        /// </summary>
        public void Unblock()
        {
            lock (GlobalLock)
            {
                ErrorUtilities.VerifyThrow(State == BuildRequestEntryState.Waiting, "Entry must be in the waiting state to be unblocked. Config: {0} State: {1} Request: {2}", RequestConfiguration.ConfigurationId, State, Request.GlobalRequestId);
                ErrorUtilities.VerifyThrow(_blockingGlobalRequestId != BuildRequest.InvalidGlobalRequestId, "Entry must be waiting on another request to be unblocked.  Config: {0} Request: {1}", RequestConfiguration.ConfigurationId, Request.GlobalRequestId);

                _blockingGlobalRequestId = BuildRequest.InvalidGlobalRequestId;

                ChangeState(BuildRequestEntryState.Ready);
            }
        }

        /// <summary>
        /// Marks the entry as active and returns all of the results needed to continue.
        /// Results are returned as { nodeRequestId -> BuildResult }
        /// </summary>
        /// <returns>The results for all previously pending requests, or null if there were none.</returns>
        public IDictionary<int, BuildResult> Continue()
        {
            lock (GlobalLock)
            {
                ErrorUtilities.VerifyThrow(_unresolvedConfigurations == null, "All configurations must be resolved before Continue may be called.");
                ErrorUtilities.VerifyThrow(_outstandingRequests == null, "All outstanding requests must have been satisfied.");
                ErrorUtilities.VerifyThrow(State == BuildRequestEntryState.Ready, "Entry must be in the Ready state.  Config: {0} State: {1}", RequestConfiguration.ConfigurationId, State);

                IDictionary<int, BuildResult> ret = _outstandingResults;
                _outstandingResults = null;

                ChangeState(BuildRequestEntryState.Active);

                return ret;
            }
        }

        /// <summary>
        /// Starts to cancel the current request.
        /// </summary>
        public void BeginCancel()
        {
            lock (GlobalLock)
            {
                if (State == BuildRequestEntryState.Waiting)
                {
                    if (_outstandingResults == null && _outstandingRequests != null)
                    {
                        _outstandingResults = new Dictionary<int, BuildResult>(_outstandingRequests.Count);
                    }

                    if (_outstandingRequests != null)
                    {
                        foreach (KeyValuePair<int, BuildRequest> requestEntry in _outstandingRequests)
                        {
                            _outstandingResults[requestEntry.Key] = new BuildResult(requestEntry.Value, new BuildAbortedException());
                        }
                    }

                    if (_unresolvedConfigurations != null && _outstandingResults != null)
                    {
                        foreach (List<BuildRequest> requests in _unresolvedConfigurations.Values)
                        {
                            foreach (BuildRequest request in requests)
                            {
                                _outstandingResults[request.NodeRequestId] = new BuildResult(request, new BuildAbortedException());
                            }
                        }
                    }

                    _unresolvedConfigurations = null;
                    _outstandingRequests = null;
                    ChangeState(BuildRequestEntryState.Ready);
                }
            }

            _requestBuilder?.BeginCancel();
        }

        /// <summary>
        /// Waits for the current request until it's canceled.
        /// </summary>
        public void WaitForCancelCompletion()
        {
            _requestBuilder?.WaitForCancelCompletion();
        }

        /// <summary>
        /// Marks this entry as complete and sets the final results.
        /// </summary>
        /// <param name="result">The result of the build.</param>
        public void Complete(BuildResult result)
        {
            lock (GlobalLock)
            {
                ErrorUtilities.VerifyThrowArgumentNull(result, nameof(result));
                ErrorUtilities.VerifyThrow(Result == null, "Entry already Completed.");

                // If this request is determined to be a success, then all outstanding items must have been taken care of
                // and it must be in the correct state.  It can complete unsuccessfully for a variety of reasons in a variety 
                // of states.
                if (result.OverallResult == BuildResultCode.Success)
                {
                    ErrorUtilities.VerifyThrow(State == BuildRequestEntryState.Active, "Entry must be active before it can be Completed successfully.  Config: {0} State: {1}", RequestConfiguration.ConfigurationId, State);
                    ErrorUtilities.VerifyThrow(_unresolvedConfigurations == null, "Entry must not have any unresolved configurations.");
                    ErrorUtilities.VerifyThrow(_outstandingRequests == null, "Entry must have no outstanding requests.");
                    ErrorUtilities.VerifyThrow(_outstandingResults == null, "Results must be consumed before request may be completed.");
                }

                Result = result;
                ChangeState(BuildRequestEntryState.Complete);
            }
        }

        /// <summary>
        /// Adds a request to the set of waiting requests.
        /// </summary>
        private void WaitForResult(BuildRequest newRequest, bool addToIssueList)
        {
            lock (GlobalLock)
            {
                ErrorUtilities.VerifyThrow(State == BuildRequestEntryState.Active || State == BuildRequestEntryState.Waiting, "Must be in Active or Waiting state to wait for results.  Config: {0} State: {1}", RequestConfiguration.ConfigurationId, State);

                if (newRequest.IsConfigurationResolved)
                {
                    _outstandingRequests ??= new Dictionary<int, BuildRequest>();

                    ErrorUtilities.VerifyThrow(!_outstandingRequests.ContainsKey(newRequest.NodeRequestId), "Already waiting for local request {0}", newRequest.NodeRequestId);
                    _outstandingRequests.Add(newRequest.NodeRequestId, newRequest);
                }
                else
                {
                    ErrorUtilities.VerifyThrow(addToIssueList, "Requests with unresolved configurations should always be added to the issue list.");
                    _unresolvedConfigurations ??= new Dictionary<int, List<BuildRequest>>();

                    if (!_unresolvedConfigurations.ContainsKey(newRequest.ConfigurationId))
                    {
                        _unresolvedConfigurations.Add(newRequest.ConfigurationId, new List<BuildRequest>());
                    }

                    _unresolvedConfigurations[newRequest.ConfigurationId].Add(newRequest);
                }

                if (addToIssueList)
                {
                    _requestsToIssue ??= new List<BuildRequest>();
                    _requestsToIssue.Add(newRequest);
                }

                ChangeState(BuildRequestEntryState.Waiting);
            }
        }

        /// <summary>
        /// Updates the state of this entry.
        /// </summary>
        /// <param name="newState">The new state for this entry.</param>
        private void ChangeState(BuildRequestEntryState newState)
        {
            if (State != newState)
            {
                State = newState;

                BuildRequestEntryStateChangedDelegate stateEvent = OnStateChanged;

                stateEvent?.Invoke(this, newState);
            }
        }
    }
}
