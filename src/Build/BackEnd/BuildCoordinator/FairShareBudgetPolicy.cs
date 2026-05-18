// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Default budget policy: each build gets an equal share of the remaining budget.
    ///
    /// Auto-detect defaults (no args):
    ///   TotalBudget        = 90% of logical cores
    ///   MaxConcurrentBuilds = TotalBudget / 4
    ///   Per-build grant     = remaining / slotsToFill, capped at requested
    /// </summary>
    public sealed class FairShareBudgetPolicy : INodeBudgetPolicy
    {
        public int TotalBudget { get; }
        public int MaxConcurrentBuilds { get; }

        /// <summary>Opinionated defaults from machine core count.</summary>
        public FairShareBudgetPolicy()
            : this(Math.Max(1, (int)(Environment.ProcessorCount * 0.9)))
        {
        }

        public FairShareBudgetPolicy(int totalBudget)
            : this(totalBudget, Math.Max(1, totalBudget / 4))
        {
        }

        public FairShareBudgetPolicy(int totalBudget, int maxConcurrentBuilds)
        {
            TotalBudget = Math.Max(1, totalBudget);
            MaxConcurrentBuilds = Math.Max(1, maxConcurrentBuilds);
        }

        public int GetGrantedNodes(int requestedNodes, int activeCount, int queuedCount, int allocatedNodes)
        {
            // Grant from the remaining budget, divided among slots that still need filling.
            // activeCount includes the build being granted; subtract 1 for already-granted builds.
            // This ensures total grants never exceed TotalBudget regardless of arrival order.
            int remaining = Math.Max(0, TotalBudget - allocatedNodes);
            int slotsToFill = Math.Max(1, MaxConcurrentBuilds - (activeCount - 1));
            int fairShare = Math.Max(1, remaining / Math.Max(1, slotsToFill));
            return Math.Min(fairShare, requestedNodes);
        }
    }
}
