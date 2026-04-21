// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Extension methods for <see cref="TaskEnvironment"/> used by built-in tasks.
    /// </summary>
    internal static class TaskEnvironmentExtensions
    {
        /// <summary>
        /// Absolutizes each non-empty path in the array using <see cref="TaskEnvironment.GetAbsolutePath"/>.
        /// Returns <see langword="null"/> if <paramref name="paths"/> is <see langword="null"/>.
        /// Empty or null entries are passed through unchanged.
        /// </summary>
        internal static string[]? GetAbsolutePathsOrNull(this TaskEnvironment taskEnvironment, string[]? paths)
        {
            if (paths == null)
            {
                return null;
            }

            var result = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                result[i] = string.IsNullOrEmpty(paths[i])
                    ? paths[i]
                    : taskEnvironment.GetAbsolutePath(paths[i]).Value;
            }

            return result;
        }
    }
}
