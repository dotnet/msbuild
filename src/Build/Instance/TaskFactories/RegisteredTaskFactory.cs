// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// An <see cref="ITaskFactory"/> that constructs a task that a host registered through
    /// <see cref="TaskClassRegistry"/> (via <c>Microsoft.Build.Utilities.Task.RegisterTask</c>), with no
    /// assembly loading or by-name type resolution.
    /// </summary>
    /// <remarks>
    /// The engine instantiates a registered task by calling <see cref="CreateRegisteredTask"/> directly,
    /// which is reflection-free and avoids the <see cref="ITaskFactory.CreateTask"/> interface member (that
    /// member is <c>[RequiresUnreferencedCode]</c>, so calling it would reintroduce a trim warning). The
    /// <see cref="LoadedType"/> this factory exposes was built at registration from the registered, trim-rooted
    /// task type, so parameter discovery and binding stay trim-safe.
    /// </remarks>
    internal sealed class RegisteredTaskFactory : ITaskFactory
    {
        /// <summary>
        /// The registration that supplies the reflection-free constructor.
        /// </summary>
        private readonly TaskClassRegistration _registration;

        /// <summary>
        /// The reflected type metadata, built from the registered task type at registration time.
        /// </summary>
        private readonly LoadedType _loadedType;

        internal RegisteredTaskFactory(TaskClassRegistration registration, LoadedType loadedType)
        {
            _registration = registration;
            _loadedType = loadedType;
        }

        /// <inheritdoc />
        public string FactoryName => "Registered Task Factory";

        /// <inheritdoc />
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
        public Type TaskType => _loadedType.Type;

        /// <summary>
        /// Constructs a new instance of the registered task. Reflection-free: it invokes the registered
        /// factory. The engine calls this instead of <see cref="CreateTask"/> so no trim-unsafe interface
        /// member is reached on the registered-task path.
        /// </summary>
        internal ITask CreateRegisteredTask() => _registration.CreateInstance();

        /// <inheritdoc />
        public TaskPropertyInfo[] GetTaskParameters() => _loadedType.Properties;

        /// <inheritdoc />
        [RequiresUnreferencedCode("Task factories create tasks by reflecting over a task type discovered or generated at runtime, which is incompatible with trimming.")]
        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost) => true;

        /// <inheritdoc />
        [RequiresUnreferencedCode("Task factories create tasks by reflecting over a task type discovered or generated at runtime, which is incompatible with trimming.")]
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost) => _registration.CreateInstance();

        /// <inheritdoc />
        public void CleanupTask(ITask task)
        {
        }
    }
}
