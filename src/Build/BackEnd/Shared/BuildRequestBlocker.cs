// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Build.Execution;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Indicates what the action is for requests which are yielding.
    /// </summary>
    internal enum YieldAction : byte
    {
        /// <summary>
        /// The request is yielding its control of the node.
        /// </summary>
        Yield,

        /// <summary>
        /// The request is ready to reacquire control of the node.
        /// </summary>
        Reacquire,

        /// <summary>
        /// There is no yield action
        /// </summary>
        None,
    }

    /// <summary>
    /// This class is used to inform the Scheduler that a request on a node is being blocked from further progress.  There
    /// are two cases for this:
    /// 1) The request may be blocked waiting for a target to complete in the same project but which is assigned to
    ///    another request.
    /// 2) The request may be blocked because it has child requests which need to be satisfied to proceed.
    /// </summary>
    internal class BuildRequestBlocker : INodePacket
    {
        /// <summary>
        /// The yield action, if any.
        /// </summary>
        private YieldAction _yieldAction;

        /// <summary>
        /// The global request id of the request which is being blocked from continuing.
        /// </summary>
        private int _blockedGlobalRequestId;

        /// <summary>
        /// The set of targets which are currently in progress for the blocked global request ID.
        /// </summary>
        private string[] _targetsInProgress;

        /// <summary>
        /// The request on which we are blocked, if any.
        /// </summary>
        private int _blockingGlobalRequestId;

        /// <summary>
        /// The name of the blocking target, if any.
        /// </summary>
        private string _blockingTarget;

        private BuildResult _partialBuildResult;

        /// <summary>
        /// The requests which need to be built to unblock the request, if any.
        /// </summary>
        private BuildRequest[] _buildRequests;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        internal BuildRequestBlocker(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Constructor for the blocker where we are blocked waiting for a target.
        /// </summary>
        internal BuildRequestBlocker(int blockedGlobalRequestId, string[] targetsInProgress, int blockingGlobalRequestId, string blockingTarget)
            : this(blockedGlobalRequestId, targetsInProgress)
        {
            _blockingGlobalRequestId = blockingGlobalRequestId;
            _blockingTarget = blockingTarget;
        }

        /// <summary>
        /// Constructor for the blocker where we are blocked waiting for requests to be satisfied.
        /// </summary>
        internal BuildRequestBlocker(int blockedGlobalRequestId, string[] targetsInProgress, BuildRequest[] buildRequests)
            : this(blockedGlobalRequestId, targetsInProgress)
        {
            _buildRequests = buildRequests;
        }

        /// <summary>
        /// Constructor for a blocker used by yielding requests.
        /// </summary>
        internal BuildRequestBlocker(int blockedGlobalRequestId, string[] targetsInProgress, YieldAction action)
            : this(blockedGlobalRequestId, targetsInProgress)
        {
            _yieldAction = action;
            _blockingGlobalRequestId = blockedGlobalRequestId;
        }

        /// <summary>
        /// Constructor for a blocker used by results-transfer requests
        /// </summary>
        /// <param name="blockedGlobalRequestId">The request needing results transferred</param>
        internal BuildRequestBlocker(int blockedGlobalRequestId)
        {
            _blockedGlobalRequestId = blockedGlobalRequestId;
            _blockingGlobalRequestId = blockedGlobalRequestId;
            _targetsInProgress = null;
            _yieldAction = YieldAction.None;
        }

        /// <summary>
        /// Constructor for common values.
        /// </summary>
        private BuildRequestBlocker(int blockedGlobalRequestId, string[] targetsInProgress)
        {
            _blockedGlobalRequestId = blockedGlobalRequestId;
            _blockingGlobalRequestId = BuildRequest.InvalidGlobalRequestId;
            _targetsInProgress = targetsInProgress;
            _yieldAction = YieldAction.None;
        }

        public BuildRequestBlocker(int requestGlobalRequestId, string[] targetsInProgress, int unsubmittedRequestBlockingGlobalRequestId, string unsubmittedRequestBlockingTarget, BuildResult partialBuildResult)
        : this(requestGlobalRequestId, targetsInProgress, unsubmittedRequestBlockingGlobalRequestId, unsubmittedRequestBlockingTarget)
        {
            _partialBuildResult = partialBuildResult;
        }

        /// <summary>
        /// Returns the type of packet.
        /// </summary>
        public NodePacketType Type
        {
            [DebuggerStepThrough]
            get
            { return NodePacketType.BuildRequestBlocker; }
        }

        /// <summary>
        /// Accessor for the blocked request id.
        /// </summary>
        public int BlockedRequestId
        {
            [DebuggerStepThrough]
            get
            {
                return _blockedGlobalRequestId;
            }
        }

        /// <summary>
        /// Accessor for the set of targets currently in progress.
        /// </summary>
        public string[] TargetsInProgress
        {
            [DebuggerStepThrough]
            get
            {
                return _targetsInProgress;
            }
        }

        /// <summary>
        /// Accessor for the blocking request id, if any.
        /// </summary>
        public int BlockingRequestId
        {
            [DebuggerStepThrough]
            get
            {
                return _blockingGlobalRequestId;
            }
        }

        /// <summary>
        /// Accessor for the blocking request id, if any.
        /// </summary>
        public string BlockingTarget
        {
            [DebuggerStepThrough]
            get
            {
                return _blockingTarget;
            }
        }

        /// <summary>
        /// Accessor for the blocking build requests, if any.
        /// </summary>
        public BuildRequest[] BuildRequests
        {
            [DebuggerStepThrough]
            get
            {
                return _buildRequests;
            }
        }

        /// <summary>
        /// Accessor for the yield action.
        /// </summary>
        public YieldAction YieldAction
        {
            [DebuggerStepThrough]
            get
            {
                return _yieldAction;
            }
        }

        public BuildResult PartialBuildResult => _partialBuildResult;

        #region INodePacketTranslatable Members

        /// <summary>
        /// Serialization method.
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _blockedGlobalRequestId);
            translator.Translate(ref _targetsInProgress);
            translator.Translate(ref _blockingGlobalRequestId);
            translator.Translate(ref _blockingTarget);
            translator.TranslateEnum(ref _yieldAction, (int)_yieldAction);
            translator.TranslateArray(ref _buildRequests);
            translator.Translate(ref _partialBuildResult, packetTranslator => BuildResult.FactoryForDeserialization(packetTranslator));
        }

        #endregion

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new BuildRequestBlocker(translator);
        }
    }
}
