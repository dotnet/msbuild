// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used to wrap the context within which the task is executed. This includes the
    /// project within which the task is being executed, the target, the task success
    /// or failure and task outputs. This class is instantiated inside the engine and is directly
    /// accessed outside of the engine domain. It is used for sharing data between the engine domain
    /// and the TEM.
    /// </summary>
    internal class TaskExecutionContext : ExecutionContext
    {
        #region Constructors
        /// <summary>
        /// Default constructor for creation of task execution wrapper
        /// </summary>
        internal TaskExecutionContext
        (
            Project parentProject,
            Target  parentTarget,
            XmlElement taskNode,
            ProjectBuildState buildContext,
            int handleId,
            int nodeIndex,
            BuildEventContext taskBuildEventContext
        )
            :base(handleId, nodeIndex, taskBuildEventContext)
        {
            this.parentProject = parentProject;
            this.parentTarget = parentTarget;
            this.taskNode = taskNode;
            this.buildContext = buildContext;
            this.thrownException = null;
        }
        #endregion

        #region Properties

        /// <summary>
        /// Returns true if the task completed successfully
        /// </summary>
        internal bool TaskExecutedSuccessfully
        {
            get
            {
                return this.taskExecutedSuccessfully;
            }
        }

        /// <summary>
        /// Returns the exception thrown during the task execution. The exception will either be
        /// InvalidProjectException or some unexpected exception that occured in the engine code,
        /// because unexpected task exceptions are converted to logged errors.
        /// </summary>
        internal Exception ThrownException
        {
            get
            {
                return this.thrownException;
            }
        }

        /// <summary>
        /// Project within which this task exists
        /// </summary>
        internal Project ParentProject
        {
            get
            {
                return this.parentProject;
            }
        }

        /// <summary>
        /// Target within which this task exists
        /// </summary>
        internal Target ParentTarget
        {
            get
            {
                return this.parentTarget;
            }
        }

        /// <summary>
        /// Project build context within which this task is executing
        /// </summary>
        internal ProjectBuildState BuildContext
        {
            get
            {
                return this.buildContext;
            }
        }

        /// <summary>
        /// XML node for the task
        /// </summary>
        internal XmlElement TaskNode
        {
            get
            {
                return this.taskNode;
            }
        }

        /// <summary>
        /// The build request that triggered the execution of this task
        /// </summary>
        internal BuildRequest TriggeringBuildRequest
        {
            get
            {
                BuildRequest buildRequest = buildContext.BuildRequest;
                ErrorUtilities.VerifyThrow(buildRequest != null, "There must be a non-null build request");
                return buildRequest;
            }
        }

        #endregion

        #region Methods
        /// <summary>
        /// This method is used to set the outputs of the task once the execution is complete
        /// </summary>
        internal void SetTaskOutputs
        (
            bool taskExecutedSuccessfully,
            Exception thrownException,
            long executionTime
        )
        {
            this.taskExecutedSuccessfully = taskExecutedSuccessfully;
            this.thrownException = thrownException;
            this.buildContext.BuildRequest.AddTaskExecutionTime(executionTime);
        }
        #endregion

        #region Member Data
        /// <summary>
        /// The project within which the target containing the task was run
        /// </summary>
        private Project parentProject;
        /// <summary>
        /// The target withing which the task is contained
        /// </summary>
        private Target parentTarget;
        /// <summary>
        /// The XML node for the task
        /// </summary>
        private XmlElement taskNode;
        /// <summary>
        /// Context within which the task execution was requested
        /// </summary>
        private ProjectBuildState buildContext;

        /// <summary>
        /// Task outputs
        /// </summary>
        bool                taskExecutedSuccessfully; 
        Exception           thrownException;

        #endregion
    }
}
