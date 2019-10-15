// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Graph
{
    /// <summary>
    /// Represents an entry point into the project graph which is comprised of a project file and a set of global properties
    /// </summary>
    public struct ProjectGraphEntryPoint
    {
        /// <summary>
        /// Constructs an entry point with the given project file and no global properties.
        /// </summary>
        /// <param name="projectFile">The project file to use for this entry point</param>
        public ProjectGraphEntryPoint(string projectFile)
            : this(projectFile, null)
        {
        }

        /// <summary>
        /// Constructs an entry point with the given project file and global properties.
        /// </summary>
        /// <param name="projectFile">The project file to use for this entry point</param>
        /// <param name="globalProperties">The global properties to use for this entry point. May be null.</param>
        public ProjectGraphEntryPoint(string projectFile, IDictionary<string, string> globalProperties)
        {
            ErrorUtilities.VerifyThrowArgumentLength(projectFile, nameof(projectFile));

            ProjectFile = projectFile;
            GlobalProperties = globalProperties;
        }

        /// <summary>
        /// Gets the project file to use for this entry point.
        /// </summary>
        public string ProjectFile { get; }

        /// <summary>
        /// Gets the global properties to use for this entry point.
        /// </summary>
        public IDictionary<string, string> GlobalProperties { get; }

        internal static IEnumerable<ProjectGraphEntryPoint> CreateEnumerable(IEnumerable<string> entryProjectFiles)
        {
            foreach (string entryProjectFile in entryProjectFiles)
            {
                yield return new ProjectGraphEntryPoint(entryProjectFile);
            }
        }

        internal static IEnumerable<ProjectGraphEntryPoint> CreateEnumerable(IEnumerable<string> entryProjectFiles, IDictionary<string, string> globalProperties)
        {
            foreach (string entryProjectFile in entryProjectFiles)
            {
                yield return new ProjectGraphEntryPoint(entryProjectFile, globalProperties);
            }
        }

        internal IEnumerable<ProjectGraphEntryPoint> AsEnumerable()
        {
            yield return this;
        }
    }
}
