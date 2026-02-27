// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework;

/// <summary>
/// Extension methods for <see cref="MessageImportance"/>.
/// </summary>
internal static class MessageImportanceExtensions
{
    /// <summary>
    /// Returns true if the message importance is at least the required importance.
    /// High &gt;= Normal &gt;= Low.
    /// For example, High.IsAtLeast(MessageImportance.Normal) returns true, while
    /// Normal.IsAtLeast(MessageImportance.High) returns false.
    /// </summary>
    /// <param name="importance">The importance to evaluate.</param>
    /// <param name="requiredImportance">The minimum required importance.</param>
    /// <returns><see langword="true"/> if <paramref name="importance"/> is at least <paramref name="requiredImportance"/>; otherwise <see langword="false"/>.</returns>
    public static bool IsAtLeast(this MessageImportance importance, MessageImportance requiredImportance)
    {
        // Enum underlying values: High = 0, Normal = 1, Low = 2.
        // Smaller numeric value indicates higher importance.
        return importance <= requiredImportance;
    }
}
