// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Aggregation of a toolset version (eg. "2.0"), tools path, and optional set of associated properties
    /// </summary>
    public class Toolset
    {
        // Name of the tools version
        private string toolsVersion;

        // The MSBuildBinPath (and ToolsPath) for this tools version
        private string toolsPath;

        // Properties 
        private BuildPropertyGroup properties;

        /// <summary>
        /// Constructor taking only tools version and a matching tools path
        /// </summary>
        /// <param name="toolsVersion">Name of the toolset</param>
        /// <param name="toolsPath">Path to this toolset's tasks and targets</param>
        public Toolset(string toolsVersion, string toolsPath)
            : this(toolsVersion, toolsPath, null)
        {
        }

        /// <summary>
        /// Constructor that also associates a set of properties with the tools version
        /// </summary>
        /// <param name="toolsVersion">Name of the toolset</param>
        /// <param name="toolsPath">Path to this toolset's tasks and targets</param>
        /// <param name="buildProperties">Properties that should be associated with the Toolset.
        /// May be null, in which case an empty property group will be used.</param>
        public Toolset(string toolsVersion, string toolsPath, BuildPropertyGroup buildProperties)
        {
            ErrorUtilities.VerifyThrowArgumentLength(toolsVersion, "toolsVersion");
            ErrorUtilities.VerifyThrowArgumentLength(toolsPath, "toolsPath");

            this.toolsVersion = toolsVersion;
            this.ToolsPath = toolsPath;

            this.properties = new BuildPropertyGroup();
            if (buildProperties != null)
            {
                this.properties.ImportProperties(buildProperties);
            }
        }

        /// <summary>
        /// Name of this toolset
        /// </summary>
        public string ToolsVersion
        {
            get
            {
                return this.toolsVersion;
            }
        }

        /// <summary>
        /// Path to this toolset's tasks and targets. Corresponds to $(MSBuildToolsPath) in a project or targets file. 
        /// </summary>
        public string ToolsPath
        {
            get
            {
                return this.toolsPath;
            }
            private set
            {
                // Strip the trailing backslash if it exists.  This way, when somebody
                // concatenates does something like "$(MSBuildToolsPath)\CSharp.targets",
                // they don't end up with a double-backslash in the middle.  (It doesn't
                // technically hurt anything, but it doesn't look nice.)
                string toolsPathToUse = value;
                
                if (FileUtilities.EndsWithSlash(toolsPathToUse))
                {
                    string rootPath = Path.GetPathRoot(Path.GetFullPath(toolsPathToUse));

                    // Only if $(MSBuildBinPath) is *NOT* the root of a drive should we strip trailing slashes
                    if (!String.Equals(rootPath, toolsPathToUse, StringComparison.OrdinalIgnoreCase))
                    {
                        // Trim off one trailing slash
                        toolsPathToUse = toolsPathToUse.Substring(0, toolsPathToUse.Length - 1);
                    }
                }

                this.toolsPath = toolsPathToUse;
            }
        }

        /// <summary>
        /// Properties associated with the toolset
        /// </summary>
        public BuildPropertyGroup BuildProperties
        {
            get
            {
                return this.properties;
            }
        }

        /// <summary>
        /// Make a deep copy of the Toolset
        /// </summary>
        public Toolset Clone()
        {
            // Can't use BuildPropertyGroupProxy as it's not a BuildPropertyGroup,
            // so do a clone. This shouldn't be a perf issue because we expect toolsets to have
            // relatively few properties.
            return new Toolset
                (toolsVersion,
                 toolsPath,
                 properties.Clone(true /* deep clone */));
        }
    }
}
