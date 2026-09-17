// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Framework;

/// <summary>
/// A statically-known, reflection-free map from a task parameter type name (the <c>ParameterType</c>
/// string of a <c>&lt;UsingTask&gt;</c> <c>&lt;ParameterGroup&gt;</c> parameter) to its <see cref="Type"/>.
/// </summary>
/// <remarks>
/// <para>
/// MSBuild restricts task parameter types to a small, well-defined set (see
/// <c>TaskParameterTypeVerifier</c>): any value type, <see cref="string"/>, and the
/// <see cref="ITaskItem"/> family - each also allowed as an array. Resolving such a type from its
/// declared name historically used <see cref="Type.GetType(string)"/>, which is incompatible with
/// trimming and Native AOT (the type cannot be determined statically, so the trimmer cannot know to
/// preserve it).
/// </para>
/// <para>
/// This registry replaces that lookup for the common, product-known types: the intrinsic value types,
/// <see cref="string"/>, and the MSBuild <see cref="ITaskItem"/> types are pre-registered here, so they
/// resolve with no reflection in a trimmed/AOT image. Registered <em>value</em> types are member-rooted
/// for trimming (a value parameter may be converted from its string form) via the
/// <c>[DynamicallyAccessedMembers]</c> on <see cref="RegisterValueType{T}"/>; <em>item</em> types are
/// validated by assignability only and are never member-reflected, so they need no member rooting. A host
/// that uses additional task parameter types can register them at startup through
/// <c>Microsoft.Build.Utilities.TaskItem.RegisterTaskParameterValueType</c> and
/// <c>RegisterTaskParameterItemType</c>, which forward here.
/// </para>
/// <para>
/// Names that the registry does not know fall back to <see cref="Type.GetType(string)"/> only when the
/// <c>Microsoft.Build.EnableReflectiveTaskParameterTypes</c> feature switch is enabled (the JIT default);
/// in a trimmed/AOT application that switch is substituted <see langword="false"/> and an unknown name
/// fails observably instead. This type lives in Microsoft.Build.Framework so both the engine
/// (Microsoft.Build) and the public registration surface (Microsoft.Build.Utilities) can reach it.
/// </para>
/// </remarks>
internal static class TaskParameterTypeRegistry
{
    /// <summary>
    /// Maps a type's <see cref="Type.FullName"/> (for example <c>System.Int32</c>, <c>System.Int32[]</c>,
    /// or <c>Microsoft.Build.Framework.ITaskItem</c>) to the type. Keyed case-insensitively to mirror the
    /// case-insensitive fallback the by-name <see cref="Type.GetType(string)"/> resolution used.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Type> s_typesByName = new(StringComparer.OrdinalIgnoreCase);

    static TaskParameterTypeRegistry()
    {
        // string is the single most common task parameter type. It is neither a value type nor an
        // ITaskItem, so it is registered directly; the BCL already preserves the members it needs.
        Add(typeof(string));
        Add(typeof(string[]));

        // The intrinsic value types MSBuild accepts as task parameter types. Each call also roots the
        // type (and its array form) for trimming via the [DynamicallyAccessedMembers] on RegisterValueType.
        RegisterValueType<bool>();
        RegisterValueType<byte>();
        RegisterValueType<sbyte>();
        RegisterValueType<char>();
        RegisterValueType<short>();
        RegisterValueType<ushort>();
        RegisterValueType<int>();
        RegisterValueType<uint>();
        RegisterValueType<long>();
        RegisterValueType<ulong>();
        RegisterValueType<float>();
        RegisterValueType<double>();
        RegisterValueType<decimal>();
        RegisterValueType<DateTime>();

        // The MSBuild ITaskItem types visible from Framework. The concrete item types in higher assemblies
        // cannot be referenced from here: the engine's internal ProjectItemInstance.TaskItem is registered
        // from Microsoft.Build, and the public Microsoft.Build.Utilities.TaskItem - a higher-layer type the
        // engine does not reference - is registered by a host through the public API if it is declared as a
        // parameter type. (The out-of-proc task host's private TaskParameterTaskItem is never declared, so
        // it is not registered.)
        RegisterTaskItemType<ITaskItem>();
        RegisterTaskItemType<ITaskItem2>();
        RegisterTaskItemType<TaskItemData>();
    }

    /// <summary>
    /// Registers a value type (and its array form) as a resolvable task parameter type, rooting it for
    /// trimming so it can be resolved with no reflection in a trimmed/AOT image.
    /// </summary>
    /// <typeparam name="T">The value type to register. Enums and user-defined structs are permitted.</typeparam>
    internal static void RegisterValueType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        where T : struct
    {
        Add(typeof(T));
        Add(typeof(T[]));
    }

    /// <summary>
    /// Registers an <see cref="ITaskItem"/> type (and its array form) as a resolvable task parameter type.
    /// Item-typed parameters are validated by assignability and are never member-reflected through the
    /// registry, so only the type reference is needed (no <c>[DynamicallyAccessedMembers]</c> rooting) and
    /// resolution stays reflection-free in a trimmed/AOT image.
    /// </summary>
    /// <typeparam name="T">The <see cref="ITaskItem"/> type to register.</typeparam>
    internal static void RegisterTaskItemType<T>()
        where T : ITaskItem
    {
        Add(typeof(T));
        Add(typeof(T[]));
    }

    /// <summary>
    /// Looks up a task parameter type by its declared name, with no reflection.
    /// </summary>
    /// <param name="typeName">The expanded <c>ParameterType</c> name, for example <c>System.String</c>.</param>
    /// <returns>The registered <see cref="Type"/>, or <see langword="null"/> if no type is registered under that name.</returns>
    internal static Type? TryGetType(string typeName) =>
        s_typesByName.TryGetValue(typeName, out Type? type) ? type : null;

    private static void Add(Type type)
    {
        string? fullName = type.FullName;
        if (fullName is not null)
        {
            s_typesByName[fullName] = type;
        }
    }
}
