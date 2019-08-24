// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The factory
    /// </summary>
    internal class IntrinsicTaskFactory : ITaskFactory
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public IntrinsicTaskFactory(Type intrinsicType)
        {
            this.TaskType = intrinsicType;
        }

        /// <summary>
        /// Returns the factory name
        /// </summary>
        public string FactoryName
        {
            get { return "Intrinsic Task Factory"; }
        }

        /// <summary>
        /// Returns the task type.
        /// </summary>
        public Type TaskType
        {
            get;
            private set;
        }

        /// <summary>
        /// Initialize the factory.
        /// </summary>
        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
        {
            if (!String.Equals(taskName, TaskType.Name, StringComparison.OrdinalIgnoreCase))
            {
                ErrorUtilities.ThrowInternalError("Unexpected task name {0}.  Expected {1}", taskName, TaskType.Name);
            }

            return true;
        }

        /// <summary>
        /// Gets all of the parameters on the task.
        /// </summary>
        public TaskPropertyInfo[] GetTaskParameters()
        {
            PropertyInfo[] infos = TaskType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var propertyInfos = new TaskPropertyInfo[infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                propertyInfos[i] = new ReflectableTaskPropertyInfo(infos[i]);
            }

            return propertyInfos;
        }

        /// <summary>
        /// Creates an instance of the task.
        /// </summary>
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost)
        {
            if (TaskType == typeof(MSBuild))
            {
                return new MSBuild();
            }
            else if (TaskType == typeof(CallTarget))
            {
                return new CallTarget();
            }

            ErrorUtilities.ThrowInternalError("Unexpected intrinsic task type {0}", TaskType);
            return null;
        }

        /// <summary>
        /// Cleanup for the task.
        /// </summary>
        public void CleanupTask(ITask task)
        {
        }
    }
}
