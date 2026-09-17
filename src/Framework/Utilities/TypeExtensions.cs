// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Shared;

internal static class TypeExtensions
{
    /// <summary>
    /// The member set a late-bound (property-function) receiver type must preserve for trimming.
    /// <c>InvokePublicMember</c> only ever binds members in this set, so preserving it is
    /// sufficient; annotating the receiver with it makes that requirement machine-checked at every
    /// call site, so a caller passing a type whose public surface is not preserved fails the build.
    /// </summary>
    private const DynamicallyAccessedMemberTypes PublicMemberSurface =
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicMethods |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields;

    extension(Type type)
    {
        [UnconditionalSuppressMessage("SingleFile", "IL3000",
            Justification = "Assembly.Location is empty under single-file/Native AOT; the empty result is handled here (and AOT hosts supply the MSBuild path via MSBUILD_EXE_PATH rather than relying on it).")]
        public string GetAssemblyPath()
        {
            // Path.GetFullPath throws on an empty string, which is exactly what Assembly.Location returns
            // for a single-file/Native AOT app, so return the empty path as-is in that case. In a hosted
            // (non-single-file) host Location is populated and this behaves as before.
            string location = type.Assembly.Location;
            return location.Length == 0 ? location : Path.GetFullPath(location);
        }

        /// <summary>
        /// Returns a boxed zero-initialized instance when the receiver is a value type, or
        /// <see langword="null"/> for a reference type. Used to seed an argument slot for a value-type
        /// <c>out</c> parameter so late-bound member resolution has a correctly typed box to write into.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [UnconditionalSuppressMessage("Trimming", "IL2067",
            Justification = "Activator.CreateInstance is only invoked for value types (guarded by Type.IsValueType), which always have a public parameterless constructor.")]
        public object? CreateDefault()
            => type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    extension([DynamicallyAccessedMembers(PublicMemberSurface)] Type type)
    {
        /// <summary>
        /// Invokes a public member on the receiver type using the standard binder, after verifying
        /// that <paramref name="bindingFlags"/> cannot reach non-public members.
        /// </summary>
        /// <remarks>
        /// This is the un-suppressed, public half of the pair. It owns both guarantees the trim suppression
        /// below depends on: the runtime guarantee (it throws if <see cref="BindingFlags.NonPublic"/> is set,
        /// so only public members can be bound) and the machine-checked guarantee (the
        /// <see cref="DynamicallyAccessedMembersAttribute"/> on the receiver, which the trim analyzer enforces
        /// at every call site). It delegates the single reflective call that needs a suppression to the private
        /// <c>InvokeMemberPublicOnly</c>, so that suppression cannot be reached except through this validated
        /// entry point and therefore cannot be misused or silently invalidated.
        /// </remarks>
        public object? InvokePublicMember(string name, BindingFlags bindingFlags, object? target, object?[]? args)
        {
            Assumed.Zero(
                (int)(bindingFlags & BindingFlags.NonPublic),
                $"'{BindingFlags.NonPublic}' is not permitted for {nameof(InvokePublicMember)}; only public members may be bound.");

            return InvokeMemberPublicOnly(type, name, bindingFlags, target, args);
        }
    }

    /// <summary>
    /// Performs the actual <see cref="Type.InvokeMember(string, BindingFlags, Binder, object, object[], CultureInfo)"/>
    /// call. This private method is the single, encapsulated home of the IL2070 suppression for property-function
    /// invocation; it must only ever be reached through <c>InvokePublicMember</c>.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Type.InvokeMember's reflection contract always lists non-public members, but the sole caller InvokePublicMember rejects BindingFlags.NonPublic (re-asserted below), so only public members are bound. The public surface of the receiver type is preserved for trimming by the [DynamicallyAccessedMembers] annotation on InvokePublicMember's receiver, which the trim analyzer enforces at every call site. Keeping this method private prevents the suppression from being bypassed or misused.")]
    private static object? InvokeMemberPublicOnly(
        [DynamicallyAccessedMembers(PublicMemberSurface)] Type type,
        string name,
        BindingFlags bindingFlags,
        object? target,
        object?[]? args)
    {
        Debug.Assert(
            (bindingFlags & BindingFlags.NonPublic) == 0,
            "InvokeMemberPublicOnly must never be called with BindingFlags.NonPublic; InvokePublicMember rejects it before delegating here.");
        return type.InvokeMember(name, bindingFlags, Type.DefaultBinder, target, args, CultureInfo.InvariantCulture);
    }
}
