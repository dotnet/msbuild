// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Extension methods for <see cref="TaskEnvironment"/> and <see cref="AbsolutePath"/> used by built-in tasks.
    /// </summary>
    internal static class TaskEnvironmentExtensions
    {
        /// <summary>
        /// Returns the canonical form of an <see cref="AbsolutePath"/> (resolving ".." segments, etc.),
        /// or the original absolute path if canonicalization fails.
        /// <see cref="System.IO.Path.GetFullPath(string)"/> on .NET Framework validates path characters and throws
        /// <see cref="ArgumentException"/> for illegal characters (e.g. <c>|</c>, <c>&lt;</c>, <c>&gt;</c>).
        /// .NET Core is more permissive and delegates character validation to the OS.
        /// </summary>
        /// <param name="absolutePath">The absolute path to canonicalize.</param>
        /// <param name="log">Optional logger. When provided, a low-importance diagnostic message is logged on failure.</param>
        internal static AbsolutePath GetCanonicalFormNoThrow(this AbsolutePath absolutePath, TaskLoggingHelper? log = null)
        {
            try
            {
                return absolutePath.GetCanonicalForm();
            }
            catch (Exception e)
            {
                log?.LogMessageFromResources(MessageImportance.Low, "General.FailedToCanonicalizePath", absolutePath.Value, e.Message);
                return absolutePath;
            }
        }

        /// <summary>
        /// Absolutizes each non-empty path in the array using <see cref="TaskEnvironment.GetAbsolutePath"/>.
        /// Returns <see langword="null"/> if <paramref name="paths"/> is <see langword="null"/>.
        /// Empty or null entries are passed through unchanged.
        /// </summary>
        internal static AbsolutePath[]? GetAbsolutePathsOrNull(this TaskEnvironment taskEnvironment, string[]? paths)
        {
            if (paths is null)
            {
                return null;
            }

            var result = new AbsolutePath[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                result[i] = string.IsNullOrEmpty(paths[i])
                    ? new AbsolutePath(paths[i], ignoreRootedCheck: true)
                    : taskEnvironment.GetAbsolutePath(paths[i]);
            }

            return result;
        }

        /// <summary>
        /// Converts an array of <see cref="AbsolutePath"/> to a string array of <see cref="AbsolutePath.Value"/>s.
        /// Returns <see langword="null"/> if <paramref name="paths"/> is <see langword="null"/>.
        /// </summary>
        internal static string[]? ToStringArray(this AbsolutePath[]? paths)
        {
            if (paths is null)
            {
                return null;
            }

            var result = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                result[i] = paths[i].Value;
            }

            return result;
        }

        /// <summary>
        /// Converts an array of <see cref="AbsolutePath"/> to a string array of <see cref="AbsolutePath.OriginalValue"/>s
        /// (the user-supplied path before any absolutization/canonicalization).
        /// Returns <see langword="null"/> if <paramref name="paths"/> is <see langword="null"/>.
        /// </summary>
        internal static string[]? ToOriginalValueArray(this AbsolutePath[]? paths)
        {
            if (paths is null)
            {
                return null;
            }

            var result = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                result[i] = paths[i].OriginalValue;
            }

            return result;
        }
    }
}
