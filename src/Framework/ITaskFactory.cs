// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface that a task factory Instance should implement
    /// </summary>
    public interface ITaskFactory
    {
        /// <summary>
        /// Gets the name of the factory.
        /// </summary>
        /// <value>The name of the factory.</value>
        string FactoryName { get; }

        /// <summary>
        /// Gets the type of the task this factory will instantiate.  Implementations must return a value for this property.
        /// </summary>
        Type TaskType { get; }

        /// <summary>
        /// Initializes this factory for instantiating tasks with a particular inline task block.
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="parameterGroup">The parameter group.</param>
        /// <param name="taskBody">The task body.</param>
        /// <param name="taskFactoryLoggingHost">The task factory logging host.</param>
        /// <returns>A value indicating whether initialization was successful.</returns>
        /// <remarks>
        /// <para>MSBuild engine will call this to initialize the factory. This should initialize the factory enough so that the factory can be asked
        /// whether or not task names can be created by the factory.</para>
        /// <para>
        /// The taskFactoryLoggingHost will log messages in the context of the target where the task is first used.
        /// </para>
        /// </remarks>
        bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost);

        /// <summary>
        /// Get the descriptions for all the task's parameters.
        /// </summary>
        /// <returns>A non-null array of property descriptions.</returns>
        TaskPropertyInfo[] GetTaskParameters();

        /// <summary>
        /// Create an instance of the task to be used.
        /// </summary>
        /// <param name="taskFactoryLoggingHost">
        /// The task factory logging host will log messages in the context of the task.
        /// </param>
        /// <returns>
        /// The generated task, or <c>null</c> if the task failed to be created.
        /// </returns>
        ITask CreateTask(IBuildEngine taskFactoryLoggingHost);

        /// <summary>
        /// Cleans up any context or state that may have been built up for a given task.
        /// </summary>
        /// <param name="task">The task to clean up.</param>
        /// <remarks>
        /// For many factories, this method is a no-op.  But some factories may have built up
        /// an AppDomain as part of an individual task instance, and this is their opportunity
        /// to shutdown the AppDomain.
        /// </remarks>
        void CleanupTask(ITask task);
    }
}
