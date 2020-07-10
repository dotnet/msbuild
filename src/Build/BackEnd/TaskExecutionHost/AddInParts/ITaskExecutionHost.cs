// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;
using TargetLoggingContext = Microsoft.Build.BackEnd.Logging.TargetLoggingContext;
using TaskLoggingContext = Microsoft.Build.BackEnd.Logging.TaskLoggingContext;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Flags requrned by ITaskExecutionHost.FindTask().
    /// </summary>
    [Flags]
    internal enum TaskRequirements
    {
        /// <summary>
        /// The task was not found.
        /// </summary>
        None = 0,

        /// <summary>
        /// The task must be executed on an STA thread.
        /// </summary>
        RequireSTAThread = 0x01,

        /// <summary>
        /// The task must be executed in a separate AppDomain.
        /// </summary>
        RequireSeparateAppDomain = 0x02
    }

    /// <summary>
    /// This interface represents the host for task execution.  When used in the in-proc scenario, these method calls essentially
    /// are pass-throughs to just set some member variables and call methods directly on the task and associated objects.
    /// In the out-of-proc/AppDomain-isolated case, the object implementing these methods may break apart the information
    /// in the parameters to be consumed by the IContract representing the remote object through MAF.
    /// 
    /// REFACTOR - Eliminate this interface.
    /// </summary>
    internal interface ITaskExecutionHost
    {
        /// <summary>
        /// The associated project.
        /// </summary>
        ProjectInstance ProjectInstance
        {
            get;
        }

        /// <summary>
        /// Flag to determine whether or not to log task inputs.
        /// </summary>
        bool LogTaskInputs { get; }

        /// <summary>
        /// Initialize the host with the objects required to communicate with the host process.
        /// </summary>
        void InitializeForTask(IBuildEngine2 buildEngine, TargetLoggingContext loggingContext, ProjectInstance projectInstance, string taskName, ElementLocation taskLocation, ITaskHost taskHost, bool continueOnError,
#if FEATURE_APPDOMAIN
            AppDomainSetup appDomainSetup,
#endif
            bool isOutOfProc, CancellationToken cancellationToken);

        /// <summary>
        /// Ask the task host to find its task in the registry and get it ready for initializing the batch
        /// </summary>
        /// <returns>The task requirements if the task is found, null otherwise.</returns>
        TaskRequirements? FindTask(IDictionary<string, string> taskIdentityParameters);

        /// <summary>
        /// Initializes for running a particular batch
        /// </summary>
        /// <returns>True if the task is instantiated, false otherwise.</returns>
        bool InitializeForBatch(TaskLoggingContext loggingContext, ItemBucket batchBucket, IDictionary<string, string> taskIdentityParameters);

        /// <summary>
        /// Sets a task parameter using an unevaluated value, which will be expanded by the batch bucket.
        /// </summary>
        bool SetTaskParameters(IDictionary<string, (string, ElementLocation)> parameters);

        /// <summary>
        /// Gets all of the outputs and stores them in the batch bucket.
        /// </summary>
        bool GatherTaskOutputs(string parameterName, ElementLocation parameterLocation, bool outputTargetIsItem, string outputTargetName);

        /// <summary>
        /// Signal that we are done with this bucket.
        /// </summary>
        void CleanupForBatch();

        /// <summary>
        /// Signal that we are done with this task.
        /// </summary>
        void CleanupForTask();

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>
        /// True if execution succeeded, false otherwise.
        /// </returns>
        bool Execute();
    }
}
