// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a cache of inputs to a compilation-style task.
    /// </summary>
    internal class Dependencies
    {
        /// <summary>
        /// Hashtable of other dependency files.
        /// Key is filename and value is DependencyFile.
        /// </summary>
        internal Dictionary<string, DependencyFile> dependencies = new();

        /// <summary>
        /// Look up a dependency file. Return null if it isn't there.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        internal DependencyFile GetDependencyFile(string filename)
        {
            dependencies.TryGetValue(filename, out DependencyFile file);
            return file;
        }

        /// <summary>
        /// Add a new dependency file.
        /// </summary>
        internal void AddDependencyFile(string filename, DependencyFile file)
        {
            dependencies[filename] = file;
        }

        /// <summary>
        /// Remove new dependency file.
        /// </summary>
        internal void RemoveDependencyFile(string filename)
        {
            dependencies.Remove(filename);
        }

        /// <summary>
        /// Remove all entries from the dependency table.
        /// </summary>
        internal void Clear()
        {
            dependencies.Clear();
        }
    }
}
