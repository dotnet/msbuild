// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using System.Collections;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// A logging context for building a specific target within a project.
    /// </summary>
    internal class TargetLoggingContext : BuildLoggingContext
    {
        /// <summary>
        /// Should target outputs be logged also.
        /// </summary>
        private static bool s_enableTargetOutputLogging = !String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDTARGETOUTPUTLOGGING"));

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
            : base(projectLoggingContext)
        {
            _projectLoggingContext = projectLoggingContext;
            _target = target;

            this.BuildEventContext = LoggingService.LogTargetStarted(projectLoggingContext.BuildEventContext, target.Name, projectFullPath, target.Location.File, parentTargetName, buildReason);
            this.IsValid = true;
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
        /// Should target outputs be logged also.
        /// </summary>
        internal static bool EnableTargetOutputLogging
        {
            get { return s_enableTargetOutputLogging; }
            set { s_enableTargetOutputLogging = value; }
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
            ErrorUtilities.VerifyThrow(IsValid, "Should be valid");

            TargetOutputItemsInstanceEnumeratorProxy targetOutputWrapper = null;

            // Only log target outputs if we are going to log a target finished event and the environment variable is set and the target outputs are not null
            if (!LoggingService.OnlyLogCriticalEvents && s_enableTargetOutputLogging && targetOutputs != null)
            {
                targetOutputWrapper = new TargetOutputItemsInstanceEnumeratorProxy(targetOutputs);
            }

            LoggingService.LogTargetFinished(BuildEventContext, _target.Name, projectFullPath, _target.Location.File, success, targetOutputWrapper);
            this.IsValid = false;
        }

        /// <summary>
        /// Log that a task is about to start
        /// </summary>
        internal TaskLoggingContext LogTaskBatchStarted(string projectFullPath, ProjectTargetInstanceChild task)
        {
            ErrorUtilities.VerifyThrow(IsValid, "Should be valid");

            return new TaskLoggingContext(this, projectFullPath, task);
        }

        /// <summary>
        /// An enumerable wrapper for items that clones items as they are requested,
        /// so that writes have no effect on the items.
        /// </summary>
        /// <remarks>
        /// This class is designed to be passed to loggers.
        /// The expense of copying items is only incurred if and when 
        /// a logger chooses to enumerate over it.
        /// </remarks>
        internal class TargetOutputItemsInstanceEnumeratorProxy : IEnumerable<TaskItem>
        {
            /// <summary>
            /// Enumerable that this proxies
            /// </summary>
            private IEnumerable<TaskItem> _backingItems;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="backingItems">Enumerator this class should proxy</param>
            internal TargetOutputItemsInstanceEnumeratorProxy(IEnumerable<TaskItem> backingItems)
            {
                _backingItems = backingItems;
            }

            /// <summary>
            /// Returns an enumerator that provides copies of the items
            /// in the backing store.
            /// Each dictionary entry has key of the item type and value of an ITaskItem.
            /// Type of the enumerator is imposed by backwards compatibility for ProjectStartedEvent.
            /// </summary>
            public IEnumerator<TaskItem> GetEnumerator()
            {
                foreach (TaskItem item in _backingItems)
                {
                    yield return item.DeepClone();
                }
            }

            /// <summary>
            /// Returns an enumerator that provides copies of the items
            /// in the backing store.
            /// </summary>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator)GetEnumerator();
            }
        }
    }
}
