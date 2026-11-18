// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Decides which instance property-function calls ("dotting in") are permitted under the restricted mode.
/// </summary>
/// <remarks>
/// <para>
/// Used when the <c>RestrictPropertyFunctionReceivers</c> feature switch (in Microsoft.Build.Framework) is
/// enabled. It limits dotting to a curated, bounded set of receiver types so the members reachable by
/// reflection are predictable and statically known, which keeps the property-function path trim compatible.
/// </para>
/// <para>
/// Common read-only navigation such as <c>$([System.IO.Directory]::GetParent(x).Parent.FullName)</c>
/// remains available; chains that would otherwise reach an open-ended type graph (for example
/// <c>$([System.IO.Directory]::GetParent(x).GetFiles().GetValue(0).OpenWrite())</c>) are not permitted.
/// </para>
/// </remarks>
internal static class PropertyFunctionReceiver
{
    /// <summary>
    /// Non-primitive receiver types whose entire public instance surface is permitted. Primitive types
    /// (the numeric types, <see cref="bool"/>, and <see cref="char"/>) are covered by
    /// <see cref="Type.IsPrimitive"/> in <see cref="IsAllowed"/> and are intentionally not listed here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the value-like and text-processing types that common function chains flow through
    /// (string and decimal operations, date math, globalization, regex results).
    /// </para>
    /// </remarks>
    private static readonly FrozenSet<Type> s_allowedReceivers = new[]
    {
        typeof(string), typeof(decimal),
        typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
        typeof(Guid), typeof(Version), typeof(CultureInfo),
        typeof(Uri), typeof(Regex), typeof(Match), typeof(Group), typeof(Capture),
    }.ToFrozenSet();

    /// <summary>
    /// The members permitted on a <c>FileSystemInfo</c> (<c>DirectoryInfo</c> / <c>FileInfo</c>):
    /// read-only metadata and navigation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an allowlist on purpose - enumeration (<c>GetFiles</c>, <c>EnumerateFiles</c>, ...), stream
    /// creation (<c>Open*</c>, <c>Create*</c>), and the members that change the file system (<c>Delete</c>,
    /// <c>MoveTo</c>, <c>CopyTo</c>, <c>Replace</c>, ...) are not listed, so the reachable surface stays
    /// bounded and predictable.
    /// </para>
    /// </remarks>
    private static readonly FrozenSet<string> s_fileSystemInfoMembers = new[]
    {
        nameof(FileSystemInfo.FullName), nameof(FileSystemInfo.Name), nameof(FileSystemInfo.Exists), nameof(FileSystemInfo.Extension),
        nameof(FileInfo.Length), nameof(FileInfo.DirectoryName), nameof(FileInfo.IsReadOnly), nameof(ToString),
        nameof(DirectoryInfo.Parent), nameof(DirectoryInfo.Root), nameof(FileInfo.Directory),
        nameof(FileSystemInfo.Attributes), nameof(FileSystemInfo.CreationTime), nameof(FileSystemInfo.CreationTimeUtc),
        nameof(FileSystemInfo.LastAccessTime), nameof(FileSystemInfo.LastAccessTimeUtc),
        nameof(FileSystemInfo.LastWriteTime), nameof(FileSystemInfo.LastWriteTimeUtc),
#if NET
        nameof(FileSystemInfo.LinkTarget),
#endif
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal static bool TryExecuteFileSystemInfoMember(FileSystemInfo receiver, string methodName, BindingFlags bindingFlags, object[] args, out object? result)
    {
        result = null;
        if (args.Length != 0 || !s_fileSystemInfoMembers.TryGetValue(methodName, out string? member))
        {
            return false;
        }

        if ((bindingFlags & BindingFlags.GetProperty) == 0)
        {
            if (member == nameof(ToString) && (bindingFlags & BindingFlags.InvokeMethod) != 0)
            {
                result = receiver.ToString();
                return true;
            }

            return false;
        }

        switch (member)
        {
            case nameof(FileSystemInfo.FullName): result = receiver.FullName; break;
            case nameof(FileSystemInfo.Name): result = receiver.Name; break;
            case nameof(FileSystemInfo.Exists): result = receiver.Exists; break;
            case nameof(FileSystemInfo.Extension): result = receiver.Extension; break;
            case nameof(FileSystemInfo.Attributes): result = receiver.Attributes; break;
            case nameof(FileSystemInfo.CreationTime): result = receiver.CreationTime; break;
            case nameof(FileSystemInfo.CreationTimeUtc): result = receiver.CreationTimeUtc; break;
            case nameof(FileSystemInfo.LastAccessTime): result = receiver.LastAccessTime; break;
            case nameof(FileSystemInfo.LastAccessTimeUtc): result = receiver.LastAccessTimeUtc; break;
            case nameof(FileSystemInfo.LastWriteTime): result = receiver.LastWriteTime; break;
            case nameof(FileSystemInfo.LastWriteTimeUtc): result = receiver.LastWriteTimeUtc; break;
#if NET
            case nameof(FileSystemInfo.LinkTarget): result = receiver.LinkTarget; break;
#endif
            case nameof(FileInfo.Length) when receiver is FileInfo file: result = file.Length; break;
            case nameof(FileInfo.DirectoryName) when receiver is FileInfo file: result = file.DirectoryName; break;
            case nameof(FileInfo.Directory) when receiver is FileInfo file: result = file.Directory; break;
            case nameof(FileInfo.IsReadOnly) when receiver is FileInfo file: result = file.IsReadOnly; break;
            case nameof(DirectoryInfo.Parent) when receiver is DirectoryInfo directory: result = directory.Parent; break;
            case nameof(DirectoryInfo.Root) when receiver is DirectoryInfo directory: result = directory.Root; break;
            default: return false;
        }

        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> if an instance member named <paramref name="methodName"/> may
    /// be invoked on a receiver of type <paramref name="receiverType"/> under the restricted mode.
    /// </summary>
    internal static bool IsAllowed(Type receiverType, string methodName)
    {
        // Primitives (the numeric types, bool, char), enums, and arrays are permitted in full. These
        // are single flag reads, so they run before the set lookup. Array element access
        // (Array.GetValue) and enum members return values whose runtime type is re-checked at the next
        // chain hop, so every receiver in the chain is validated.
        if (receiverType.IsPrimitive || receiverType.IsEnum || receiverType.IsArray)
        {
            return true;
        }

        // Other value/text types whose entire public instance surface is permitted (string, decimal,
        // date/time, globalization, regex results).
        if (s_allowedReceivers.Contains(receiverType))
        {
            return true;
        }

        // FileSystemInfo (DirectoryInfo / FileInfo): read-only navigation/metadata members only.
        // The file/directory static functions are registered against the real System.IO types (see
        // Constants.InitializeAvailableMethods), so receivers reached by dotting in (GetParent, Parent,
        // Directory, Root, ...) are always System.IO.*; the Microsoft.IO.Redist variants never surface.
        if (typeof(System.IO.FileSystemInfo).IsAssignableFrom(receiverType))
        {
            return s_fileSystemInfoMembers.Contains(methodName);
        }

        return false;
    }
}
