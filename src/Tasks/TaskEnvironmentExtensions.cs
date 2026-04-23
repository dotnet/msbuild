// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Extension methods for <see cref="TaskEnvironment"/> used by built-in tasks.
    /// </summary>
    internal static class TaskEnvironmentExtensions
    {
        /// <summary>
        /// Tries to return the canonical form of an <see cref="AbsolutePath"/> (resolving ".." segments, etc.).
        /// <see cref="System.IO.Path.GetFullPath(string)"/> on .NET Framework validates path characters and throws
        /// <see cref="ArgumentException"/> for illegal characters (e.g. <c>|</c>, <c>&lt;</c>, <c>&gt;</c>).
        /// .NET Core is more permissive and delegates character validation to the OS.
        /// When canonicalization fails, the original absolute path is returned as-is.
        /// </summary>
        /// <param name="absolutePath">The absolute path to canonicalize.</param>
        /// <param name="log">Optional logger. When provided, a low-importance diagnostic message is logged on failure.</param>
        internal static AbsolutePath TryGetCanonicalForm(this AbsolutePath absolutePath, TaskLoggingHelper? log = null)
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
    }
}
