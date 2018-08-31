using System;
using System.IO;

namespace Microsoft.DotNet.Cli
{
    internal static class CommandDirectoryContext
    {
        [ThreadStatic]
        private static string _currentDirectoryOverwrite;

        /// <summary>
        /// Expands a path similar to Path.GetFullPath() but allows unit tests a hook to inject an overwrite to the
        /// current working directory.
        /// </summary>
        /// <param name="path">A realtive or absolute path specifier</param>
        /// <returns>The full path to the target</returns>
        public static string ExpandPath(string path)
            => _currentDirectoryOverwrite is string basePath
                ? Path.GetFullPath(path, basePath)
                : Path.GetFullPath(path);

        /// <summary>
        /// Meant to be used only in unit test to remove dependency on special characters in the absolute repo path
        /// The overwrite will only affect the current thread.
        /// </summary>
        /// <param name="currentDirectory">Directory to be used instead of the current working directory.</param>
        /// <param name="action">Action to be executed with the overwritten directory in place.</param>
        public static void PerformActionWithOverwrittenCurrentDirectory(string currentDirectory, Action action)
        {
            if (_currentDirectoryOverwrite != null)
            {
                throw new InvalidOperationException(
                    $"Calls to {nameof(CommandDirectoryContext)}.{nameof(PerformActionWithOverwrittenCurrentDirectory)} cannot be nested.");
            }
            _currentDirectoryOverwrite = currentDirectory;
            try
            {
                action();
            }
            finally
            {
                _currentDirectoryOverwrite = null;
            }
        }
    }
}
