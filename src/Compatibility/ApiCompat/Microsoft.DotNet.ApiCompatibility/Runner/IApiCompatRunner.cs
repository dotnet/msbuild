// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Runner
{
    /// <summary>
    /// Enqueues work items and performs api compatibility checks on them.
    /// </summary>
    public interface IApiCompatRunner
    {
        /// <summary>
        /// The work items to be executed.
        /// </summary>
        IReadOnlyCollection<ApiCompatRunnerWorkItem> WorkItems { get; }

        /// <summary>
        /// Performs api comparison of the enqueued work items
        /// </summary>
        void ExecuteWorkItems();

        /// <summary>
        /// Enqueues an api compat work item which consists of a left, options and a right.
        /// </summary>
        /// <param name="workItem">The api compat work item to enqueue</param>
        void EnqueueWorkItem(ApiCompatRunnerWorkItem workItem);
    }
}
