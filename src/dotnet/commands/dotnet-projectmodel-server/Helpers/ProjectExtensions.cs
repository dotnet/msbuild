// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.ProjectModel.Server.Helpers
{
    public static class ProjectExtensions
    {
        public static IEnumerable<string> ResolveSearchPaths(this Project project)
        {
            GlobalSettings settings;
            return project.ResolveSearchPaths(out settings);
        }

        public static IEnumerable<string> ResolveSearchPaths(this Project project, out GlobalSettings globalSettings)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            var searchPaths = new HashSet<string> { Directory.GetParent(project.ProjectDirectory).FullName };

            globalSettings = project.ResolveGlobalSettings();
            if (globalSettings != null)
            {
                foreach (var searchPath in globalSettings.ProjectSearchPaths)
                {
                    var path = Path.Combine(globalSettings.DirectoryPath, searchPath);
                    searchPaths.Add(Path.GetFullPath(path));
                }
            }

            return searchPaths;
        }

        public static GlobalSettings ResolveGlobalSettings(this Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            GlobalSettings settings;
            var root = ProjectRootResolver.ResolveRootDirectory(project.ProjectDirectory);
            if (GlobalSettings.TryGetGlobalSettings(root, out settings))
            {
                return settings;
            }
            else
            {
                return null;
            }
        }
    }
}
