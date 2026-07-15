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
/// The <see cref="LoadedType"/> is built once. For the generic <see cref="TaskClassRegistry.Register{T}(string)"/>
/// overload it is supplied eagerly at registration (where the task type is trim-rooted). For the
/// <see cref="TaskClassRegistry.Register(string, Func{ITask})"/> overload - where only an untyped factory is
/// known - it is built lazily from the first constructed instance's type.
/// </remarks>
internal sealed class TaskClassRegistration
{
    private readonly Func<ITask> _createInstance;
    private readonly object _loadedTypeLock = new();
    private volatile LoadedType? _loadedType;

    /// <summary>
    /// Creates a registration whose <see cref="LoadedType"/> is already known (the generic, trim-rooted path).
    /// </summary>
    internal TaskClassRegistration(Func<ITask> createInstance, LoadedType loadedType)
    {
        _createInstance = createInstance;
        _loadedType = loadedType;
    }

    /// <summary>
    /// Creates a registration backed only by a factory; the <see cref="LoadedType"/> is computed lazily from
    /// the first instance's type.
    /// </summary>
    internal TaskClassRegistration(Func<ITask> createInstance) => _createInstance = createInstance;

    /// <summary>
    /// Constructs a new instance of the registered task. Reflection-free: it invokes the registered factory.
    /// </summary>
    internal ITask CreateInstance() => _createInstance();

    /// <summary>
    /// Gets the reflected type metadata the engine uses to discover and bind the task's parameters.
    /// </summary>
    internal LoadedType GetLoadedType()
    {
        if (_loadedType is null)
        {
            lock (_loadedTypeLock)
            {
                _loadedType ??= CreateLoadedTypeFromFactory();
            }
        }

        return _loadedType;
    }

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
    private LoadedType CreateLoadedTypeFromFactory()
        => TaskClassRegistry.CreateLoadedType(_createInstance().GetType());
}
