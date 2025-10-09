// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Defines a task factory that supports runtime and architecture-specific task creation.
    /// This interface extends <see cref="ITaskFactory2"/> to enable task factories to consume
    /// UsingTask parameters such as Runtime and Architecture for more granular control over
    /// task execution environments.
    /// </summary>
    public interface ITaskFactory3 : ITaskFactory2
    {
        /// <summary>
        /// Initializes the task factory with parameters that control the execution environment.
        /// </summary>
        /// <param name="taskName">
        /// The name of the task as declared in the UsingTask element.
        /// </param>
        /// <param name="factoryIdentityParameters">
        /// Special parameters that control how the factory executes tasks. These parameters
        /// are specified at the UsingTask level and apply to all tasks created by this factory instance.
        /// Custom task factories may define additional parameters as needed.
        /// </param>
        /// <param name="parameterGroup">
        /// A dictionary mapping parameter names to their metadata (<see cref="TaskPropertyInfo"/>).
        /// This defines the input and output properties that tasks created by this factory will expose.
        /// May be <c>null</c> if the task has no parameters.
        /// </param>
        /// <param name="taskBody">
        /// The inline content of the task definition, typically containing implementation code
        /// for inline tasks.
        /// </param>
        /// <param name="taskFactoryLoggingHost">
        /// A logging interface for reporting messages, warnings, and errors during factory initialization.
        /// Messages logged through this host will appear in the context of the target where the task
        /// is first referenced.
        /// </param>
        /// <returns>
        /// <c>true</c> if the factory initialized successfully and can create tasks; 
        /// <c>false</c> if initialization failed.
        /// </returns>
        bool Initialize(
            string taskName,
            TaskHostParameters factoryIdentityParameters,
            IDictionary<string, TaskPropertyInfo> parameterGroup,
            string taskBody,
            IBuildEngine taskFactoryLoggingHost);

        /// <summary>
        /// Creates a new instance of the task with optional execution environment parameters.
        /// </summary>
        /// <param name="taskFactoryLoggingHost">
        /// A logging interface for reporting messages, warnings, and errors during task creation
        /// and execution. Messages logged through this host will appear in the context of the
        /// specific task invocation.
        /// </param>
        /// <param name="taskIdentityParameters">
        /// Special parameters that control how this specific task instance executes.
        /// These task-level parameters can override or refine the factory-level parameters
        /// specified in the Initialize method.
        /// </param>
        /// <returns>
        /// A new <see cref="ITask"/> instance configured according to the specified parameters,
        /// or <c>null</c> if task creation failed. When <c>null</c> is returned, the factory
        /// should log appropriate error messages through the <paramref name="taskFactoryLoggingHost"/>.
        /// </returns>
        ITask CreateTask(
            IBuildEngine taskFactoryLoggingHost,
            TaskHostParameters taskIdentityParameters);
    }
}
