// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Pluggable policy for allocating node budgets across concurrent builds.
    /// The coordinator delegates all grant decisions to this interface.
    /// </summary>
    public interface INodeBudgetPolicy
    {
        /// <summary>Total node budget shared across all active builds.</summary>
        int TotalBudget { get; }

        /// <summary>Maximum number of builds that can be active simultaneously.</summary>
        int MaxConcurrentBuilds { get; }

        /// <summary>
        /// Determine how many nodes to grant a build.
        /// </summary>
        /// <param name="requestedNodes">The MaxNodeCount the build was launched with.</param>
        /// <param name="activeCount">Number of currently active builds (including the one being granted).</param>
        /// <param name="queuedCount">Number of builds still waiting in the queue.</param>
        /// <param name="allocatedNodes">Total nodes already granted to other active builds.</param>
        /// <returns>The number of nodes to grant (at least 1).</returns>
        int GetGrantedNodes(int requestedNodes, int activeCount, int queuedCount, int allocatedNodes);
    }
}
