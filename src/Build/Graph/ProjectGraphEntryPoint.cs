// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Shared;

#nullable disable

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

            ProjectFile = FileUtilities.NormalizePath(projectFile);
            GlobalProperties = globalProperties;
        }

        /// <summary>
        /// Gets the full path to the project file to use for this entry point.
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

        internal readonly IEnumerable<ProjectGraphEntryPoint> AsEnumerable()
        {
            yield return this;
        }
    }
}
