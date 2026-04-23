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
        /// On .NET Framework, <see cref="System.IO.Path.GetFullPath(string)"/> validates path characters and throws
        /// for illegal characters. In that case the original absolute path is returned as-is and a message is logged.
        /// </summary>
        internal static AbsolutePath TryGetCanonicalForm(this AbsolutePath absolutePath, TaskLoggingHelper log)
        {
            try
            {
                return absolutePath.GetCanonicalForm();
            }
            catch (Exception e)
            {
                log.LogMessageFromResources(MessageImportance.Low, "General.FailedToCanonicalizePath", absolutePath.Value, e.Message);
                return absolutePath;
            }
        }
    }
}
