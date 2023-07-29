// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli
{
    internal static class CommandDirectoryContext
    {
        [ThreadStatic]
        private static string _basePath;

        /// <summary>
        /// Expands a path similar to Path.GetFullPath() but gives unit tests a hook to inject an overwrite to the
        /// base path.
        /// </summary>
        /// <param name="path">A relative or absolute path specifier</param>
        /// <returns>The full path to the target</returns>
        public static string GetFullPath(string path)
            => _basePath != null
                ? Path.GetFullPath(path, _basePath)
                : Path.GetFullPath(path);

        /// <summary>
        /// Meant to be used only in unit test to remove dependency on special characters in the absolute repo path
        /// The overwrite will only affect the current thread.
        /// </summary>
        /// <param name="basePath">Directory to be used as base path instead of the current working directory.</param>
        /// <param name="action">Action to be executed with the overwritten directory in place.</param>
        public static void PerformActionWithBasePath(string basePath, Action action)
        {
            if (_basePath != null)
            {
                throw new InvalidOperationException(
                    $"Calls to {nameof(CommandDirectoryContext)}.{nameof(PerformActionWithBasePath)} cannot be nested.");
            }
            _basePath = basePath;
            Telemetry.Telemetry.CurrentSessionId = null;
            try
            {
                action();
            }
            finally
            {
                _basePath = null;
            }
        }
    }
}
