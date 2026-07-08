// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework;

/// <summary>
/// A statically-known, reflection-free map from a task name (the element name a target invokes, i.e. the
/// <c>TaskName</c> of a <c>&lt;UsingTask&gt;</c>) to a way to construct that task and the reflected metadata
/// the engine needs to bind its parameters.
/// </summary>
/// <remarks>
/// <para>
/// To run a task MSBuild normally loads the task assembly by path, resolves the task <see cref="Type"/>, and
/// constructs an instance - all reflective, and incompatible with trimming and Native AOT. A host that
/// references its task assemblies statically (for example the .NET SDK CLI when AOT-compiled) can instead
/// register those tasks here at startup, so the engine instantiates them with no assembly probing or
/// by-name type resolution.
/// </para>
/// <para>
/// A registration captures everything reflection-sensitive at registration time, where the task type is
/// statically known. The generic <see cref="Register{T}(string)"/> overload roots the task type's public
/// constructor and properties for trimming via its <c>[DynamicallyAccessedMembers]</c>, so the
/// <see cref="LoadedType"/> it builds (a <see cref="Type.GetProperties()"/> walk) stays trim-safe and the
/// engine never re-reflects the type by name. Hosts register through the public
/// <c>Microsoft.Build.Utilities.Task.RegisterTask</c> methods, which forward here.
/// </para>
/// <para>
/// This type lives in Microsoft.Build.Framework - the lowest assembly - so the engine
/// (Microsoft.Build), the task library (Microsoft.Build.Tasks.Core, which pre-registers the common
/// built-in tasks), and the public registration surface (Microsoft.Build.Utilities) can all reach it.
/// </para>
/// </remarks>
internal static class TaskClassRegistry
{
    /// <summary>
    /// Maps a task name to its registration. Keyed case-insensitively to mirror the case-insensitive task
    /// lookup the engine's <c>TaskRegistry</c> performs.
    /// </summary>
    private static readonly ConcurrentDictionary<string, TaskClassRegistration> s_tasksByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Set <see langword="true"/> once any task has been registered. The overwhelmingly common case is a
    /// host that never registers, so this lets <see cref="TryGetRegistration"/> skip hashing the task name
    /// and probing the dictionary on every task invocation until a registration actually exists. Written
    /// after the entry is published to the dictionary, so a reader observing <see langword="true"/> sees it.
    /// </summary>
    private static volatile bool s_hasRegistrations;

    /// <summary>
    /// Registers a task type under the given name so the engine can instantiate it without loading its
    /// assembly or resolving its type by reflection. The <c>[DynamicallyAccessedMembers]</c> roots the
    /// type's public constructor and properties so construction and reflective parameter binding stay
    /// trim-safe in a trimmed/AOT image.
    /// </summary>
    /// <typeparam name="T">The task type to register.</typeparam>
    /// <param name="taskName">The name a target uses to invoke the task (the <c>TaskName</c> of a <c>&lt;UsingTask&gt;</c>).</param>
    internal static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] T>(string taskName)
        where T : ITask, new()
    {
        ArgumentException.ThrowIfNullOrEmpty(taskName);

        // typeof(T) carries T's [DynamicallyAccessedMembers] here, so building the LoadedType (a
        // GetProperties walk) is trim-safe and is done once, eagerly, at registration.
        LoadedType loadedType = CreateLoadedType(typeof(T));
        s_tasksByName[taskName] = new TaskClassRegistration(static () => new T(), loadedType);
        s_hasRegistrations = true;
    }

    /// <summary>
    /// Registers a task under the given name with an explicit factory, so construction is fully
    /// reflection-free (the host supplies the constructor).
    /// </summary>
    /// <param name="taskName">The name a target uses to invoke the task (the <c>TaskName</c> of a <c>&lt;UsingTask&gt;</c>).</param>
    /// <param name="factory">A delegate that creates a new instance of the task.</param>
    internal static void Register(string taskName, Func<ITask> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskName);
        ArgumentNullException.ThrowIfNull(factory);

        // The host-supplied factory's task type is not statically known here, so the LoadedType (needed to
        // bind parameters) is built lazily from the first constructed instance's type. The host is
        // responsible for preserving that type's public properties under trimming (for example via the
        // generic Register<T> overload or a TrimmerRootAssembly entry).
        s_tasksByName[taskName] = new TaskClassRegistration(factory);
        s_hasRegistrations = true;
    }

    /// <summary>
    /// Looks up a registered task by name, with no reflection.
    /// </summary>
    /// <param name="taskName">The task name to resolve.</param>
    /// <param name="registration">The matching registration, or <see langword="null"/> if the name is not registered.</param>
    /// <returns><see langword="true"/> if a registration was found.</returns>
    internal static bool TryGetRegistration(string taskName, [NotNullWhen(true)] out TaskClassRegistration? registration)
    {
        // Hot path: the engine calls this for every task invocation. When nothing is registered (the common
        // case) skip the dictionary probe entirely.
        if (!s_hasRegistrations)
        {
            registration = null;
            return false;
        }

        return s_tasksByName.TryGetValue(taskName, out registration);
    }

    /// <summary>
    /// Builds a <see cref="LoadedType"/> for an already-loaded, trim-rooted task type. Only reflects over the
    /// type's properties (trim-safe given the <c>[DynamicallyAccessedMembers]</c> on <paramref name="taskType"/>),
    /// with synthetic assembly-name load info so no assembly is loaded by path.
    /// </summary>
    internal static LoadedType CreateLoadedType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] Type taskType)
    {
        Assembly assembly = taskType.Assembly;
        return new LoadedType(taskType, AssemblyLoadInfo.Create(assembly.FullName, null), assembly, typeof(ITaskItem));
    }
}
