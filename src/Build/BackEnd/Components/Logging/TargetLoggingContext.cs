// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// A logging context for building a specific target within a project.
    /// </summary>
    internal class TargetLoggingContext : BuildLoggingContext
    {

        /// <summary>
        /// The project to which this target is attached.
        /// </summary>
        private ProjectLoggingContext _projectLoggingContext;

        /// <summary>
        /// The target being built.
        /// </summary>
        private ProjectTargetInstance _target;

        /// <summary>
        /// Creates a new target logging context from an existing project context and target.
        /// </summary>
        internal TargetLoggingContext(ProjectLoggingContext projectLoggingContext, string projectFullPath, ProjectTargetInstance target, string parentTargetName, TargetBuiltReason buildReason)
            : base(projectLoggingContext, CreateInitialContext(projectLoggingContext, projectFullPath, target, parentTargetName, buildReason))
        {
            _projectLoggingContext = projectLoggingContext;
            _target = target;
            this.IsValid = true;
        }

        private static BuildEventContext CreateInitialContext(ProjectLoggingContext projectLoggingContext,
            string projectFullPath, ProjectTargetInstance target, string parentTargetName,
            TargetBuiltReason buildReason)
        {
            BuildEventContext buildEventContext = projectLoggingContext.LoggingService.LogTargetStarted(
                projectLoggingContext.BuildEventContext, target.Name, projectFullPath, target.Location.File,
                parentTargetName, buildReason);

            return buildEventContext;
        }

        /// <summary>
        /// Constructor used to support out-of-proc task host (proxy for in-proc logging service.)
        /// </summary>
        internal TargetLoggingContext(ILoggingService loggingService, BuildEventContext outOfProcContext)
            : base(loggingService, outOfProcContext, true)
        {
            this.IsValid = true;
        }

        /// <summary>
        /// Retrieves the project logging context.
        /// </summary>
        internal ProjectLoggingContext ProjectLoggingContext
        {
            get
            {
                return _projectLoggingContext;
            }
        }

        /// <summary>
        /// Retrieves the target.
        /// </summary>
        internal ProjectTargetInstance Target
        {
            get
            {
                return _target;
            }
        }

        /// <summary>
        /// Log that a target has finished
        /// </summary>
        internal void LogTargetBatchFinished(string projectFullPath, bool success, IEnumerable<TaskItem> targetOutputs)
        {
            this.CheckValidity();

            LoggingService.LogTargetFinished(BuildEventContext, _target.Name, projectFullPath, _target.Location.File, success, targetOutputs);
            this.IsValid = false;
        }

        /// <summary>
        /// Log that a task is about to start
        /// </summary>
        internal TaskLoggingContext LogTaskBatchStarted(string projectFullPath, ProjectTargetInstanceChild task, string taskAssemblyLocation)
        {
            this.CheckValidity();

            return new TaskLoggingContext(this, projectFullPath, task, taskAssemblyLocation);
        }
    }
}
