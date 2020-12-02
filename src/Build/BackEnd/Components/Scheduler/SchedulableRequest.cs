// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;
using Microsoft.Build.Execution;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The state enumeration for SchedulableRequests.
    /// </summary>
    internal enum SchedulableRequestState
    {
        /// <summary>
        /// This request has been submitted but has never been scheduled so it has executed no tasks and does not currently have an
        /// entry residing on any node.  There may be multiple requests with the same global request id in this state.
        /// </summary>
        Unscheduled,

        /// <summary>
        /// This request may continue executing.  It already has an entry on a node.  There may only ever be one request with a given
        /// global request id in this state.
        /// </summary>
        Ready,

        /// <summary>
        /// This request is currently executing tasks on its node.  In this case it will be the only task executing on the node -
        /// all other tasks are either Ready or Blocked.  There may only ever be one request with a given global request id in this state.
        /// </summary>
        Executing,

        /// <summary>
        /// This request is currently blocked on one or more requests which must complete before it may continue.  There may only ever be one
        /// request with a given global request id in this state.
        /// </summary>
        Blocked,

        /// <summary>
        /// This request has yielded control of the node while it is running a long-running out-of-process program.  Any number of tasks on a 
        /// node may be in the yielding state.
        /// </summary>
        Yielding,

        /// <summary>
        /// This request has completed and removed from the system.
        /// </summary>
        Completed
    }

    /// <summary>
    /// A representation of a BuildRequest and associated data used by the Scheduler to track work being done by the build system.
    /// SchedulableRequests implicitly form a directed acyclic graph showing the blocking/blocked relationship between the requests
    /// known to the system at any given time.  These associations are updated by the BlockByRequest, UnblockWithResult and ResumeExecution
    /// methods.  These methods, along with Complete, cause state changes which the SchedulingData object will record.  That data can be
    /// queried to determine the state of any request or node in the system.
    /// </summary>
    internal class SchedulableRequest
    {
        /// <summary>
        /// The request collection to which this belongs.
        /// </summary>
        private SchedulingData _schedulingData;

        /// <summary>
        /// The current state.
        /// </summary>
        private SchedulableRequestState _state;

        /// <summary>
        /// The node to which this request is assigned.
        /// </summary>
        private int _assignedNodeId;

        /// <summary>
        /// The BuildRequest this class represents.
        /// </summary>
        private BuildRequest _request;

        /// <summary>
        /// The schedulable request which issued this request.
        /// </summary>
        private SchedulableRequest _parent;

        /// <summary>
        /// The list of targets which were actively building at the time we were blocked.
        /// </summary>
        private string[] _activeTargetsWhenBlocked;

        /// <summary>
        /// The requests which must complete before we can continue executing.  Indexed by global request id and node request id.
        /// Each global request id may have multiple requests which map to it, but they will have separate node request ids.
        /// </summary>
        private Dictionary<BlockingRequestKey, SchedulableRequest> _requestsWeAreBlockedBy;

        /// <summary>
        /// The requests which cannot continue until we have finished executing.
        /// </summary>
        private HashSet<SchedulableRequest> _requestsWeAreBlocking;

        /// <summary>
        /// The time this request was created.
        /// </summary>
        private DateTime _creationTime;

        /// <summary>
        /// The time this request started building.
        /// </summary>
        private DateTime _startTime;

        /// <summary>
        /// The time this request was completed.
        /// </summary>
        private DateTime _endTime;

        /// <summary>
        /// Records of the amount of time spent in each of the states.
        /// </summary>
        private Dictionary<SchedulableRequestState, ScheduleTimeRecord> _timeRecords;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SchedulableRequest(SchedulingData collection, BuildRequest request, SchedulableRequest parent)
        {
            ErrorUtilities.VerifyThrowArgumentNull(collection, nameof(collection));
            ErrorUtilities.VerifyThrowArgumentNull(request, nameof(request));
            ErrorUtilities.VerifyThrow((parent == null) || (parent._schedulingData == collection), "Parent request does not belong to the same collection.");

            _schedulingData = collection;
            _request = request;
            _parent = parent;
            _assignedNodeId = -1;
            _requestsWeAreBlockedBy = new Dictionary<BlockingRequestKey, SchedulableRequest>();
            _requestsWeAreBlocking = new HashSet<SchedulableRequest>();

            _timeRecords = new Dictionary<SchedulableRequestState, ScheduleTimeRecord>(5);
            _timeRecords[SchedulableRequestState.Unscheduled] = new ScheduleTimeRecord();
            _timeRecords[SchedulableRequestState.Blocked] = new ScheduleTimeRecord();
            _timeRecords[SchedulableRequestState.Yielding] = new ScheduleTimeRecord();
            _timeRecords[SchedulableRequestState.Executing] = new ScheduleTimeRecord();
            _timeRecords[SchedulableRequestState.Ready] = new ScheduleTimeRecord();
            _timeRecords[SchedulableRequestState.Completed] = new ScheduleTimeRecord();

            ChangeToState(SchedulableRequestState.Unscheduled);
        }

        /// <summary>
        /// The current state of the request.
        /// </summary>
        public SchedulableRequestState State
        {
            get { return _state; }
        }

        /// <summary>
        /// The underlying BuildRequest.
        /// </summary>
        public BuildRequest BuildRequest
        {
            get { return _request; }
        }

        /// <summary>
        /// The request which issued this request.
        /// </summary>
        public SchedulableRequest Parent
        {
            get { return _parent; }
        }

        /// <summary>
        /// Returns the node to which this request is assigned.
        /// </summary>
        public int AssignedNode
        {
            get { return _assignedNodeId; }
        }

        /// <summary>
        /// The set of active targets.
        /// </summary>
        public IEnumerable<string> ActiveTargets
        {
            get
            {
                VerifyOneOfStates(new SchedulableRequestState[] { SchedulableRequestState.Yielding, SchedulableRequestState.Blocked, SchedulableRequestState.Executing });
                return _activeTargetsWhenBlocked;
            }
        }

        /// <summary>
        /// The target we are blocked on
        /// </summary>
        public string BlockingTarget { get; private set; }

        /// <summary>
        /// Gets a count of the requests we are blocked by.
        /// </summary>
        public int RequestsWeAreBlockedByCount
        {
            get
            {
                return _requestsWeAreBlockedBy.Count;
            }
        }

        /// <summary>
        /// Gets the set of requests for which we require results before we may proceed.
        /// </summary>
        public IEnumerable<SchedulableRequest> RequestsWeAreBlockedBy
        {
            get
            {
                return _requestsWeAreBlockedBy.Values;
            }
        }

        /// <summary>
        /// Gets a count of the requests we are blocking.
        /// </summary>
        public int RequestsWeAreBlockingCount
        {
            get
            {
                return _requestsWeAreBlocking.Count;
            }
        }

        /// <summary>
        /// Gets the set of requests which cannot proceed because they are waiting for results from us.
        /// </summary>
        public IEnumerable<SchedulableRequest> RequestsWeAreBlocking
        {
            get
            {
                return _requestsWeAreBlocking;
            }
        }

        /// <summary>
        /// The time this request was created.
        /// </summary>
        public DateTime CreationTime
        {
            get
            {
                return _creationTime;
            }

            set
            {
                ErrorUtilities.VerifyThrow(_creationTime == DateTime.MinValue, "Cannot set CreationTime twice.");
                _creationTime = value;
            }
        }

        /// <summary>
        /// The time this request started building.
        /// </summary>
        public DateTime StartTime
        {
            get
            {
                return _startTime;
            }

            set
            {
                ErrorUtilities.VerifyThrow(_startTime == DateTime.MinValue, "Cannot set StartTime twice.");
                _startTime = value;
            }
        }

        /// <summary>
        /// The time this request was completed.
        /// </summary>
        public DateTime EndTime
        {
            get
            {
                return _endTime;
            }

            set
            {
                ErrorUtilities.VerifyThrow(_endTime == DateTime.MinValue, "Cannot set EndTime twice.");
                _endTime = value;
            }
        }

        /// <summary>
        /// Gets the amount of time we spent in the specified state.
        /// </summary>
        public TimeSpan GetTimeSpentInState(SchedulableRequestState desiredState)
        {
            return _timeRecords[desiredState].AccumulatedTime;
        }

        /// <summary>
        /// Inticates the request is yielding the node.
        /// </summary>
        public void Yield(string[] activeTargets)
        {
            VerifyState(SchedulableRequestState.Executing);
            ErrorUtilities.VerifyThrowArgumentNull(activeTargets, nameof(activeTargets));
            _activeTargetsWhenBlocked = activeTargets;
            ChangeToState(SchedulableRequestState.Yielding);
        }

        /// <summary>
        /// Indicates the request is ready to reacquire the node.
        /// </summary>
        public void Reacquire()
        {
            VerifyState(SchedulableRequestState.Yielding);
            _activeTargetsWhenBlocked = null;
            ChangeToState(SchedulableRequestState.Ready);
        }

        /// <summary>
        /// Marks this request as being blocked by the specified request.  Establishes the correct relationships between the requests.
        /// </summary>
        /// <param name="blockingRequest">The request which is blocking this one.</param>
        /// <param name="activeTargets">The list of targets this request was currently building at the time it became blocked.</param>
        /// <param name="blockingTarget">Target that we are blocked on which is being built by <paramref name="blockingRequest"/></param>
        public void BlockByRequest(SchedulableRequest blockingRequest, string[] activeTargets, string blockingTarget = null)
        {
            VerifyOneOfStates(new SchedulableRequestState[] { SchedulableRequestState.Blocked, SchedulableRequestState.Executing });
            ErrorUtilities.VerifyThrowArgumentNull(blockingRequest, nameof(blockingRequest));
            ErrorUtilities.VerifyThrowArgumentNull(activeTargets, nameof(activeTargets));
            ErrorUtilities.VerifyThrow(BlockingTarget == null, "Cannot block again if we're already blocked on a target");

            // Note that the blocking request will typically be our parent UNLESS it is a request we blocked on because it was executing a target we wanted to execute.
            // Thus, we do not assert the parent-child relationship here.
            BlockingRequestKey key = new BlockingRequestKey(blockingRequest.BuildRequest);
            ErrorUtilities.VerifyThrow(!_requestsWeAreBlockedBy.ContainsKey(key), "We are already blocked by this request.");
            ErrorUtilities.VerifyThrow(!blockingRequest._requestsWeAreBlocking.Contains(this), "The blocking request thinks it is already blocking us.");

            // This method is only called when a request reports that it is blocked on other requests.  If the request is being blocked by a brand new
            // request, that request will be unscheduled.  If this request is blocked by an in-progress request which was executing a target it needed
            // to also execute, then that request is not unscheduled (because it was running on the node) and it is not executing (because this condition
            // can only occur against requests which are executing on the same node and since the request which called this method is the one currently
            // executing on that node, that means the request it is blocked by must either be itself blocked or ready.)
            blockingRequest.VerifyOneOfStates(new SchedulableRequestState[] { SchedulableRequestState.Yielding, SchedulableRequestState.Blocked, SchedulableRequestState.Ready, SchedulableRequestState.Unscheduled });

            // Update our list of active targets.  This has to be done before we detect circular dependencies because we use this information to detect
            // re-entrancy circular dependencies.
            _activeTargetsWhenBlocked = activeTargets;

            BlockingTarget = blockingTarget;

            DetectCircularDependency(blockingRequest);

            _requestsWeAreBlockedBy[key] = blockingRequest;
            blockingRequest._requestsWeAreBlocking.Add(this);

            ChangeToState(SchedulableRequestState.Blocked);
        }

        /// <summary>
        /// Indicates that there are partial results (project producing the result is still running) which can be used to unblock this request.  Updates the relationships between requests.
        /// </summary>
        public void UnblockWithPartialResultForBlockingTarget(BuildResult result)
        {
            VerifyOneOfStates(new SchedulableRequestState[] { SchedulableRequestState.Blocked, SchedulableRequestState.Unscheduled });
            ErrorUtilities.VerifyThrowArgumentNull(result, nameof(result));

            BlockingRequestKey key = new BlockingRequestKey(result);
            DisconnectRequestWeAreBlockedBy(key);
            BlockingTarget = null;
        }

        /// <summary>
        /// Indicates that there are results which can be used to unblock this request.  Updates the relationships between requests.
        /// </summary>
        public void UnblockWithResult(BuildResult result)
        {
            VerifyOneOfStates(new SchedulableRequestState[] { SchedulableRequestState.Blocked, SchedulableRequestState.Unscheduled });
            ErrorUtilities.VerifyThrowArgumentNull(result, nameof(result));

            BlockingRequestKey key = new BlockingRequestKey(result);
            DisconnectRequestWeAreBlockedBy(key);
            _activeTargetsWhenBlocked = null;
            BlockingTarget = null;
        }

        /// <summary>
        /// Resumes execution of the request on the specified node.
        /// </summary>
        public void ResumeExecution(int nodeId)
        {
            ErrorUtilities.VerifyThrow(_assignedNodeId == Scheduler.InvalidNodeId || _assignedNodeId == nodeId, "Request must always resume on the same node on which it was started.");

            VerifyOneOfStates(new SchedulableRequestState[] { SchedulableRequestState.Ready, SchedulableRequestState.Unscheduled });
            ErrorUtilities.VerifyThrow((_state == SchedulableRequestState.Ready) || !_schedulingData.IsRequestScheduled(this), "Another instance of request {0} is already scheduled.", _request.GlobalRequestId);
            ErrorUtilities.VerifyThrow(!_schedulingData.IsNodeWorking(nodeId), "Cannot resume execution of request {0} because node {1} is already working.", _request.GlobalRequestId, nodeId);

            int requiredNodeId = _schedulingData.GetAssignedNodeForRequestConfiguration(_request.ConfigurationId);
            ErrorUtilities.VerifyThrow(requiredNodeId == Scheduler.InvalidNodeId || requiredNodeId == nodeId, "Request {0} cannot be assigned to node {1} because its configuration is already assigned to node {2}", _request.GlobalRequestId, nodeId, requiredNodeId);

            _assignedNodeId = nodeId;
            ChangeToState(SchedulableRequestState.Executing);
        }

        /// <summary>
        /// Completes this request.
        /// </summary>
        public void Complete(BuildResult result)
        {
            VerifyOneOfStates(new SchedulableRequestState[] { SchedulableRequestState.Ready, SchedulableRequestState.Executing, SchedulableRequestState.Unscheduled });
            ErrorUtilities.VerifyThrow(_state != SchedulableRequestState.Ready || result.CircularDependency, "Request can only be Completed from the Ready state if the result indicates a circular dependency occurred.");
            ErrorUtilities.VerifyThrow(_requestsWeAreBlockedBy.Count == 0, "We can't be complete if we are still blocked on requests.");

            // Any requests we were blocking we will no longer be blocking.
            List<SchedulableRequest> requestsToUnblock = new List<SchedulableRequest>(_requestsWeAreBlocking);
            foreach (SchedulableRequest requestWeAreBlocking in requestsToUnblock)
            {
                requestWeAreBlocking.UnblockWithResult(result);
            }

            ChangeToState(SchedulableRequestState.Completed);
        }

        /// <summary>
        /// Removes an unscheduled request.
        /// </summary>
        public void Delete()
        {
            VerifyState(SchedulableRequestState.Unscheduled);
            ErrorUtilities.VerifyThrow(_requestsWeAreBlockedBy.Count == 0, "We are blocked by requests.");
            ErrorUtilities.VerifyThrow(_requestsWeAreBlocking.Count == 0, "We are blocking by requests.");
            ChangeToState(SchedulableRequestState.Completed);
        }

        /// <summary>
        /// Verifies that the current state is as expected.
        /// </summary>
        public void VerifyState(SchedulableRequestState requiredState)
        {
            ErrorUtilities.VerifyThrow(_state == requiredState, "Request {0} expected to be in state {1} but state is actually {2}", _request.GlobalRequestId, requiredState, _state);
        }

        /// <summary>
        /// Verifies that the current state is as expected.
        /// </summary>
        public void VerifyOneOfStates(SchedulableRequestState[] requiredStates)
        {
            foreach (SchedulableRequestState requiredState in requiredStates)
            {
                if (_state == requiredState)
                {
                    return;
                }
            }

            ErrorUtilities.ThrowInternalError("State {0} is not one of the expected states.", _state);
        }

        /// <summary>
        /// Change to the specified state.  Update internal counters.
        /// </summary>
        private void ChangeToState(SchedulableRequestState newState)
        {
            DateTime currentTime = DateTime.UtcNow;
            _timeRecords[_state].EndState(currentTime);
            _timeRecords[newState].StartState(currentTime);
            if (_state != newState)
            {
                SchedulableRequestState previousState = _state;
                _state = newState;
                _schedulingData.UpdateFromState(this, previousState);
            }
        }

        /// <summary>
        /// Detects a circular dependency.  Throws a CircularDependencyException if one exists.  Circular dependencies can occur
        /// under the following conditions:
        /// 1. If the blocking request's global request ID appears in the ancestor chain (Direct).
        /// 2. If a request appears in the ancestor chain and has a different global request ID but has an active target that
        ///    matches one of the targets specified in the blocking request (Direct).
        /// 3. If the blocking request exists elsewhere as a blocked request with the same global request ID, and one of its children
        ///    (recursively) matches this request's global request ID (Indirect).
        /// 4. If the blocking request's configuration is part of another request elsewhere which is also blocked, and that request
        ///    is building targets this blocking request is building, and one of that blocked request's children (recursively)
        ///    matches this request's global request ID (Indirect).
        /// </summary>
        private void DetectCircularDependency(SchedulableRequest blockingRequest)
        {
            DetectDirectCircularDependency(blockingRequest);
            DetectIndirectCircularDependency(blockingRequest);
        }

        /// <summary>
        /// Detects a circular dependency where the request which is about to block us is already blocked by us, usually as a result
        /// of it having been previously scheduled in a multiproc scenario, but before this request was able to execute.
        /// </summary>
        /// <remarks>
        /// Let A be 'this' project and B be 'blockingRequest' (the request which is going to block A.)  
        /// An indirect circular dependency exists if there is a dependency path from B to A.  If there is no 
        /// existing blocked request B' with the same global request id as B, then there can be no path from B to A because B is a brand new 
        /// request with no other dependencies.  If there is an existing blocked request B' with the same global request ID as B, then we 
        /// walk the set of dependencies recursively searching for A.  If A is found, we have a circular dependency.
        /// </remarks>
        private void DetectIndirectCircularDependency(SchedulableRequest blockingRequest)
        {
            // If there is already a blocked request which has the same configuration id as the blocking request and that blocked request is (recursively) 
            // waiting on this request, then that is an indirect circular dependency.
            SchedulableRequest alternateRequest = _schedulingData.GetBlockedRequestIfAny(blockingRequest.BuildRequest.GlobalRequestId);
            if (alternateRequest == null)
            {
                return;
            }

            Stack<SchedulableRequest> requestsToEvaluate = new Stack<SchedulableRequest>(16);
            HashSet<SchedulableRequest> evaluatedRequests = new HashSet<SchedulableRequest>();
            requestsToEvaluate.Push(alternateRequest);

            while (requestsToEvaluate.Count > 0)
            {
                SchedulableRequest requestToEvaluate = requestsToEvaluate.Pop();

                // If we make it to a child which is us, then it's a circular dependency.
                if (requestToEvaluate.BuildRequest.GlobalRequestId == this.BuildRequest.GlobalRequestId)
                {
                    ThrowIndirectCircularDependency(blockingRequest, requestToEvaluate);
                }

                evaluatedRequests.Add(requestToEvaluate);

                // If the request is not scheduled, it's possible that is because it's been scheduled elsewhere and is blocked.
                // Follow that path if it exists.                        
                if (requestToEvaluate.State == SchedulableRequestState.Unscheduled)
                {
                    requestToEvaluate = _schedulingData.GetBlockedRequestIfAny(requestToEvaluate.BuildRequest.GlobalRequestId);

                    // If there was no scheduled request to evaluate, move on.
                    if (requestToEvaluate == null || evaluatedRequests.Contains(requestToEvaluate))
                    {
                        continue;
                    }
                }

                // This request didn't cause a circular dependency, check its children.
                foreach (SchedulableRequest childRequest in requestToEvaluate.RequestsWeAreBlockedBy)
                {
                    if (!evaluatedRequests.Contains(childRequest))
                    {
                        requestsToEvaluate.Push(childRequest);
                    }
                }
            }
        }

        /// <summary>
        /// Build our ancestor list then throw the circular dependency error.
        /// </summary>
        private void ThrowIndirectCircularDependency(SchedulableRequest blockingRequest, SchedulableRequest requestToEvaluate)
        {
            // We found a request which has the same global request ID as us in a chain which leads from the (already blocked) request 
            // which is trying to block us.  Calculate its list of ancestors by walking up the parent list.
            List<SchedulableRequest> ancestors = new List<SchedulableRequest>(16);
            while (requestToEvaluate.Parent != null)
            {
                ancestors.Add(requestToEvaluate.Parent);
                requestToEvaluate = requestToEvaluate.Parent;
            }

            ancestors.Reverse(); // Because the list should be in the order from root to child.
            CleanupForCircularDependencyAndThrow(blockingRequest, ancestors);
        }

        /// <summary>
        /// Detects a circular dependency where the blocking request is in our direct ancestor chain.
        /// </summary>
        private void DetectDirectCircularDependency(SchedulableRequest blockingRequest)
        {
            // A circular dependency occurs when this project (or any of its ancestors) has the same global request id as the 
            // blocking request.
            List<SchedulableRequest> ancestors = new List<SchedulableRequest>(16);
            SchedulableRequest currentRequest = this;
            do
            {
                ancestors.Add(currentRequest);
                if (currentRequest.BuildRequest.GlobalRequestId == blockingRequest.BuildRequest.GlobalRequestId)
                {
                    // We are directly conflicting with an instance of ourselves.
                    CleanupForCircularDependencyAndThrow(blockingRequest, ancestors);
                }

                currentRequest = currentRequest.Parent;
            }
            while (currentRequest != null);
        }

        /// <summary>
        /// Removes associations with all blocking requests and throws an exception.
        /// </summary>
        private void CleanupForCircularDependencyAndThrow(SchedulableRequest requestCausingFailure, List<SchedulableRequest> ancestors)
        {
            if (_requestsWeAreBlockedBy.Count != 0)
            {
                List<SchedulableRequest> tempRequests = new List<SchedulableRequest>(_requestsWeAreBlockedBy.Values);
                foreach (SchedulableRequest requestWeAreBlockedBy in tempRequests)
                {
                    BlockingRequestKey key = new BlockingRequestKey(requestWeAreBlockedBy.BuildRequest);
                    DisconnectRequestWeAreBlockedBy(key);
                }
            }
            else
            {
                ChangeToState(SchedulableRequestState.Ready);
            }

            _activeTargetsWhenBlocked = null;

            // The blocking request itself is no longer valid if it was unscheduled.
            if (requestCausingFailure.State == SchedulableRequestState.Unscheduled)
            {
                requestCausingFailure.Delete();
            }

            throw new SchedulerCircularDependencyException(requestCausingFailure.BuildRequest, ancestors);
        }

        /// <summary>
        /// Removes the association between this request and the one we are blocked by.
        /// </summary>
        internal void DisconnectRequestWeAreBlockedBy(BlockingRequestKey blockingRequestKey)
        {
            ErrorUtilities.VerifyThrow(_requestsWeAreBlockedBy.ContainsKey(blockingRequestKey), "We are not blocked by the specified request.");

            SchedulableRequest unblockingRequest = _requestsWeAreBlockedBy[blockingRequestKey];
            ErrorUtilities.VerifyThrow(unblockingRequest._requestsWeAreBlocking.Contains(this), "The request unblocking us doesn't think it is blocking us.");

            _requestsWeAreBlockedBy.Remove(blockingRequestKey);
            unblockingRequest._requestsWeAreBlocking.Remove(this);

            // If the request we are blocked by also happens to be unscheduled, remove it as well so we don't try to run it later.  This is 
            // because circular dependency errors cause us to fail all outstanding requests on the current request.  See BuildRequsetEntry.ReportResult.
            if (unblockingRequest.State == SchedulableRequestState.Unscheduled)
            {
                unblockingRequest.Delete();
            }

            if (_requestsWeAreBlockedBy.Count == 0)
            {
                ChangeToState(SchedulableRequestState.Ready);
            }
        }

        /// <summary>
        /// A key for blocking requests combining the global request and node request ids.
        /// </summary>
        internal class BlockingRequestKey
        {
            /// <summary>
            /// The global request id.
            /// </summary>
            private int _globalRequestId;

            /// <summary>
            /// The request id known to the node.
            /// </summary>
            private int _nodeRequestId;

            /// <summary>
            /// Constructor over a request.
            /// </summary>
            public BlockingRequestKey(BuildRequest request)
            {
                _globalRequestId = request.GlobalRequestId;
                _nodeRequestId = request.NodeRequestId;
            }

            /// <summary>
            /// Constructor over a result.
            /// </summary>
            public BlockingRequestKey(BuildResult result)
            {
                _globalRequestId = result.GlobalRequestId;
                _nodeRequestId = result.NodeRequestId;
            }

            /// <summary>
            /// Equals override.
            /// </summary>
            public override bool Equals(object obj)
            {
                if (obj != null)
                {
                    BlockingRequestKey other = obj as BlockingRequestKey;
                    if (other != null)
                    {
                        return (other._globalRequestId == _globalRequestId) && (other._nodeRequestId == _nodeRequestId);
                    }
                }

                return base.Equals(obj);
            }

            /// <summary>
            /// GetHashCode override.
            /// </summary>
            public override int GetHashCode()
            {
                return _globalRequestId ^ _nodeRequestId;
            }
        }
    }
}
