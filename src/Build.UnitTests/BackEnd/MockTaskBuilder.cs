// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

using TargetLoggingContext = Microsoft.Build.BackEnd.Logging.TargetLoggingContext;
using System.Threading.Tasks;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// The mock task builder.
    /// </summary>
    internal class MockTaskBuilder : ITaskBuilder, IBuildComponent
    {
        /// <summary>
        /// The component host.
        /// </summary>
        private IBuildComponentHost _host;

        /// <summary>
        /// The current task number.
        /// </summary>
        private int _taskNumber;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MockTaskBuilder()
        {
            Reset();
        }

        /// <summary>
        /// The list of tasks executed.
        /// </summary>
        public List<ProjectTaskInstance> ExecutedTasks
        {
            get;
            set;
        }

        /// <summary>
        /// The list of OnError tasks.
        /// </summary>
        public List<ProjectOnErrorInstance> ErrorTasks
        {
            get;
            set;
        }

        /// <summary>
        /// The task ordinal to fail on.
        /// </summary>
        public int FailTaskNumber
        {
            get;
            set;
        }

        /// <summary>
        /// Resets the mock task builder to its clean state.
        /// </summary>
        public void Reset()
        {
            ErrorTasks = new List<ProjectOnErrorInstance>();
            ExecutedTasks = new List<ProjectTaskInstance>();
            FailTaskNumber = -1;
            _taskNumber = 0;
        }

        #region ITaskBuilder Members

        /// <summary>
        /// Executes the task.
        /// </summary>
        public Task<WorkUnitResult> ExecuteTask(TargetLoggingContext targetLoggingContext, BuildRequestEntry requestEntry, ITargetBuilderCallback targetBuilderCallback, ProjectTargetInstanceChild task, TaskExecutionMode mode, Lookup lookupForInference, Lookup lookupForExecution, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task<WorkUnitResult>.FromResult(new WorkUnitResult(WorkUnitResultCode.Canceled, WorkUnitActionCode.Stop, null));
            }

            ProjectOnErrorInstance errorTask = task as ProjectOnErrorInstance;
            if (null != errorTask)
            {
                ErrorTasks.Add(errorTask);
            }
            else
            {
                ProjectTaskInstance taskInstance = task as ProjectTaskInstance;
                ExecutedTasks.Add(taskInstance);

                if ((mode & TaskExecutionMode.InferOutputsOnly) == TaskExecutionMode.InferOutputsOnly)
                {
                    lookupForInference.AddNewItem(new ProjectItemInstance(requestEntry.RequestConfiguration.Project, taskInstance.Name + "_Item", "Item", task.Location.File));
                }
                else if ((mode & TaskExecutionMode.ExecuteTaskAndGatherOutputs) == TaskExecutionMode.ExecuteTaskAndGatherOutputs)
                {
                    lookupForExecution.AddNewItem(new ProjectItemInstance(requestEntry.RequestConfiguration.Project, taskInstance.Name + "_Item", "Item", task.Location.File));
                }

                if (String.Equals(taskInstance.Name, "CallTarget", StringComparison.OrdinalIgnoreCase))
                {
                    taskInstance.GetParameter("Targets");
                    targetBuilderCallback.LegacyCallTarget(taskInstance.GetParameter("Targets").Split(MSBuildConstants.SemicolonChar), false, taskInstance.Location);
                }

                _taskNumber++;
                if (FailTaskNumber == _taskNumber)
                {
                    if (taskInstance.ContinueOnError == "True")
                    {
                        return Task<WorkUnitResult>.FromResult(new WorkUnitResult(WorkUnitResultCode.Failed, WorkUnitActionCode.Continue, null));
                    }

                    return Task<WorkUnitResult>.FromResult(new WorkUnitResult(WorkUnitResultCode.Failed, WorkUnitActionCode.Stop, null));
                }
            }

            return Task<WorkUnitResult>.FromResult(new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, null));
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Sets the component host.
        /// </summary>
        /// <param name="host">The component host</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            _host = host;
        }

        /// <summary>
        /// Shuts down the component.
        /// </summary>
        public void ShutdownComponent()
        {
            _host = null;
        }

        #endregion
    }
}
