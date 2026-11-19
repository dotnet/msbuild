// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Globalization;
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
        "FullName", "Name", "Exists", "Extension", "Length", "DirectoryName",
        "IsReadOnly", "LinkTarget", "ToString",
        "Parent", "Root", "Directory",
        "Attributes",
        "CreationTime", "CreationTimeUtc",
        "LastAccessTime", "LastAccessTimeUtc",
        "LastWriteTime", "LastWriteTimeUtc",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

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
