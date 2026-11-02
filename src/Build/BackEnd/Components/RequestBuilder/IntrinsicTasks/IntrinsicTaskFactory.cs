// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

#nullable disable

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
        public IntrinsicTaskFactory([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type intrinsicType)
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
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
        public Type TaskType
        {
            get;
            private set;
        }

        /// <summary>
        /// Initialize the factory.
        /// </summary>
        [RequiresUnreferencedCode("Task factories create tasks by reflecting over a task type discovered or generated at runtime, which is incompatible with trimming.")]
        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
        {
            Assumed.Equal(taskName, TaskType.Name, StringComparison.OrdinalIgnoreCase, $"Unexpected task name {taskName}.  Expected {TaskType.Name}");

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
        [RequiresUnreferencedCode("Task factories create tasks by reflecting over a task type discovered or generated at runtime, which is incompatible with trimming.")]
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost) => CreateIntrinsicTask();

        /// <summary>
        /// Creates the intrinsic task instance by direct construction (no reflection), so it is safe under
        /// trimming/Native AOT. The <see cref="ITaskFactory.CreateTask"/> interface member carries
        /// <c>[RequiresUnreferencedCode]</c> for general task factories; this engine-internal
        /// entry point does not, letting the registered/AOT task path construct intrinsic tasks directly.
        /// </summary>
        internal ITask CreateIntrinsicTask()
        {
            if (TaskType == typeof(MSBuild))
            {
                return new MSBuild();
            }
            else if (TaskType == typeof(CallTarget))
            {
                return new CallTarget();
            }

            return InternalError.Throw<ITask>($"Unexpected intrinsic task type {TaskType}");
        }

        /// <summary>
        /// Cleanup for the task.
        /// </summary>
        public void CleanupTask(ITask task)
        {
        }
    }
}
