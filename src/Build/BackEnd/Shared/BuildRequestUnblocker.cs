// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Build.Shared;

using BuildResult = Microsoft.Build.Execution.BuildResult;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class is used by the Scheduler to unblock a blocked build request on the BuildRequestEngine.
    /// There are two cases:
    /// 1. The request was blocked waiting on a target in the same project.  In this case this class will contain
    ///    no information other than the request id.
    /// 2. The request was blocked on some set of build requests.  This class will then contain the build results 
    ///    needed to satisfy those requests.
    /// </summary>
    internal class BuildRequestUnblocker : ITranslatable, INodePacket
    {
        /// <summary>
        /// The node request id of the request which is blocked and now will either result or have results reported.
        /// </summary>
        private int _blockedGlobalRequestId = BuildRequest.InvalidGlobalRequestId;

        /// <summary>
        /// The build result which we wish to report.
        /// </summary>
        private BuildResult _buildResult;

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        internal BuildRequestUnblocker(ITranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Constructor for the unblocker where we are blocked waiting for a target.
        /// </summary>
        internal BuildRequestUnblocker(int globalRequestIdToResume)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(globalRequestIdToResume != BuildRequest.InvalidGlobalRequestId, nameof(globalRequestIdToResume));
            _blockedGlobalRequestId = globalRequestIdToResume;
        }

        /// <summary>
        /// Constructor for the unblocker where we are blocked waiting for results.
        /// </summary>
        internal BuildRequestUnblocker(BuildResult buildResult)
        {
            ErrorUtilities.VerifyThrowArgumentNull(buildResult, nameof(buildResult));
            _buildResult = buildResult;
            _blockedGlobalRequestId = buildResult.ParentGlobalRequestId;
        }

        /// <summary>
        /// Constructor for the unblocker for circular dependencies
        /// </summary>
        internal BuildRequestUnblocker(BuildRequest parentRequest, BuildResult buildResult)
            : this(buildResult)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parentRequest, nameof(parentRequest));
            _blockedGlobalRequestId = parentRequest.GlobalRequestId;
        }

        /// <summary>
        /// Returns the type of packet.
        /// </summary>
        public NodePacketType Type
        {
            [DebuggerStepThrough]
            get
            { return NodePacketType.BuildRequestUnblocker; }
        }

        /// <summary>
        /// Accessor for the blocked node request id.
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
        /// Accessor for the build results, if any.
        /// </summary>
        public BuildResult Result
        {
            [DebuggerStepThrough]
            get
            {
                return _buildResult;
            }
        }

        #region INodePacketTranslatable Members

        /// <summary>
        /// Serialization method.
        /// </summary>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _blockedGlobalRequestId);
            translator.Translate(ref _buildResult);
        }

        #endregion

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            return new BuildRequestUnblocker(translator);
        }
    }
}
