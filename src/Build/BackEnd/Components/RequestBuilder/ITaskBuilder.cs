// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using TargetLoggingContext = Microsoft.Build.BackEnd.Logging.TargetLoggingContext;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Flags indicating the mode in which the task builder should operate.
    /// </summary>
    [Flags]
    internal enum TaskExecutionMode
    {
        /// <summary>
        /// This entry is necessary to use the enum with binary math. It is never used outside 
        /// intermediate calculations.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// In this mode, the task engine actually runs the task and retrieves its outputs.
        /// </summary>
        ExecuteTaskAndGatherOutputs = 1,

        /// <summary>
        /// In this mode, the task engine only infers the task's outputs from its &lt;Output&gt; tags.
        /// </summary>
        InferOutputsOnly = 2
    }

    /// <summary>
    /// Interface representing an object which can build tasks.
    /// </summary>
    internal interface ITaskBuilder
    {
        /// <summary>
        /// Executes the specified task, batching it is necessary.
        /// </summary>
        /// <param name="targetLoggingContext">The logging context for the target</param>
        /// <param name="requestEntry">The build request entry</param>
        /// <param name="targetBuilderCallback">The callback to use for handling new build requests.</param>
        /// <param name="task">The node for the task</param>
        /// <param name="mode">The mode to use when executing the task.</param>
        /// <param name="lookupForInference">The lookup used when we are inferring outputs from inputs.</param>
        /// <param name="lookupForExecution">The lookup used when executing the task to get its outputs.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel processing of the task.</param>
        /// <returns>A Task representing the work to be done.</returns>
        Task<WorkUnitResult> ExecuteTask(TargetLoggingContext targetLoggingContext, BuildRequestEntry requestEntry, ITargetBuilderCallback targetBuilderCallback, ProjectTargetInstanceChild task, TaskExecutionMode mode, Lookup lookupForInference, Lookup lookupForExecution, CancellationToken cancellationToken);
    }
}
