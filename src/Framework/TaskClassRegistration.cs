// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

/// <summary>
/// A single entry in the <see cref="TaskClassRegistry"/>: how to construct a registered task and the
/// reflected type metadata the engine binds its parameters against.
/// </summary>
/// <remarks>
/// The <see cref="LoadedType"/> is built once. For the generic <see cref="TaskClassRegistry.Register{T}()"/>
/// overload it is supplied eagerly at registration (where the task type is trim-rooted). For the
/// <see cref="TaskClassRegistry.Register(string, Func{ITask})"/> overload - where only an untyped factory is
/// known - it is built lazily from the first constructed instance's type.
/// <para>
/// The construction factory takes the <see cref="TaskEnvironment"/> the engine is running the task with, so a
/// task that declares a constructor accepting a <see cref="TaskEnvironment"/> can receive it during
/// construction (mirroring the reflective task-load path). Factories for tasks that do not need it simply
/// ignore the argument.
/// </para>
/// </remarks>
internal sealed class TaskClassRegistration
{
    private readonly Func<TaskEnvironment, ITask> _createInstance;

    /// <summary>
    /// The reflected type metadata the engine binds the task's parameters against. For the generic,
    /// trim-rooted overload the value is known at registration; for the untyped
    /// <see cref="TaskClassRegistry.Register(string, Func{ITask})"/> overload it is computed on first use by
    /// probing a constructed instance's runtime type. Either way <see cref="Lazy{T}"/> memoizes it in a
    /// thread-safe way, so no field is ever observed in an unset state.
    /// </summary>
    private readonly Lazy<LoadedType> _loadedType;

    /// <summary>
    /// Creates a registration whose <see cref="LoadedType"/> is already known (the generic, trim-rooted path).
    /// </summary>
    internal TaskClassRegistration(Func<TaskEnvironment, ITask> createInstance, LoadedType loadedType)
    {
        _createInstance = createInstance;
        _loadedType = new Lazy<LoadedType>(() => loadedType);
    }

    /// <summary>
    /// Creates a registration backed only by an untyped factory; the <see cref="LoadedType"/> is computed
    /// lazily from the first probed instance's type.
    /// </summary>
    internal TaskClassRegistration(Func<ITask> factory)
    {
        _createInstance = _ => factory();
        _loadedType = new Lazy<LoadedType>(() => CreateLoadedTypeFromFactory(factory));
    }

    /// <summary>
    /// Constructs a new instance of the registered task. Reflection-free unless the registered task type
    /// declares a <see cref="TaskEnvironment"/> constructor (which is invoked via <see cref="Activator"/> over
    /// the trim-rooted type). The engine still assigns the task's <see cref="TaskEnvironment"/> property after
    /// construction.
    /// </summary>
    /// <param name="taskEnvironment">The environment supplied to a task that declares a <see cref="TaskEnvironment"/> constructor.</param>
    internal ITask CreateInstance(TaskEnvironment taskEnvironment) => _createInstance(taskEnvironment);

    /// <summary>
    /// Gets the reflected type metadata the engine uses to discover and bind the task's parameters.
    /// </summary>
    internal LoadedType GetLoadedType() => _loadedType.Value;

    /// <summary>
    /// Builds the <see cref="LoadedType"/> for the factory-only registration from a probe instance's type.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:UnrecognizedReflectionPattern",
        Justification = "The Func<ITask> registration overload's task type is supplied by the host and is not statically known here. " +
            "The host is responsible for preserving the task type's public properties under trimming (for example by also registering it " +
            "through the generic RegisterTask<T> overload, which roots them, or via a TrimmerRootAssembly entry). The generic overload, " +
            "which the built-in tasks and most hosts use, supplies the LoadedType eagerly and is fully trim-safe.")]
    private static LoadedType CreateLoadedTypeFromFactory(Func<ITask> factory)
        => TaskClassRegistry.CreateLoadedType(factory().GetType());
}
