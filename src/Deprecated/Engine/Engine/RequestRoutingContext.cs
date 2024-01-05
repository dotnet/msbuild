// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This context is created to contain information about a build request that has been forwarded to
    /// a child node for execution. All further communication from the child with regard to the build
    /// request (such a logging messages, errors, follow up build requests or build result) will be
    /// processing using information from this context.
    /// </summary>
    internal class RequestRoutingContext : ExecutionContext
    {
        #region Constructors
        /// <summary>
        /// Default constructor for a routing context
        /// </summary>
        internal RequestRoutingContext
        (
            int handleId,
            int nodeIndex,
            int parentHandleId,
            int parentNodeIndex,
            int parentRequestId,
            CacheScope cacheScope,
            BuildRequest triggeringBuildRequest,
            BuildEventContext buildEventContext
        )
            : base(handleId, nodeIndex, buildEventContext)
        {
            this.parentHandleId = parentHandleId;
            this.parentNodeIndex = parentNodeIndex;
            this.parentRequestId = parentRequestId;
            this.cacheScope = cacheScope;
            this.triggeringBuildRequest = triggeringBuildRequest;
        }
        #endregion

        #region Properties
        /// <summary>
        /// The handle to the parent context which maybe invalidHandle if the request
        /// originated from the host.
        /// </summary>
        internal int ParentHandleId
        {
            get
            {
                return this.parentHandleId;
            }
        }

        /// <summary>
        /// The node from the triggering build request (overwritten on the build request during routing)
        /// </summary>
        internal int ParentNodeIndex
        {
            get
            {
                return this.parentNodeIndex;
            }
        }

        /// <summary>
        /// The request Id from the triggering build request (overwritten on the build request during routing)
        /// </summary>
        internal int ParentRequestId
        {
            get
            {
                return this.parentRequestId;
            }
        }

        /// <summary>
        /// The cache scope where the result should be stored
        /// </summary>
        internal CacheScope CacheScope
        {
            get
            {
                return this.cacheScope;
            }
        }

        /// <summary>
        /// The build request being routed
        /// </summary>
        internal BuildRequest TriggeringBuildRequest
        {
            get
            {
                ErrorUtilities.VerifyThrow(triggeringBuildRequest != null, "This must be a routing context");
                return triggeringBuildRequest;
            }
        }

        #endregion

        #region Data
        // The handle Id for the parent context
        private int parentHandleId;
        // The node from the triggering build request (overwritten on the build request during routing)
        private int parentNodeIndex;
        // The request Id from the triggering build request (overwritten on the build request during routing)
        private int parentRequestId;
        // The build request being routed
        private BuildRequest triggeringBuildRequest;
        // The cache scope where the result should be stored
        private CacheScope cacheScope;
        #endregion
    }
}
