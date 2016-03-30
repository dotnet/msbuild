// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;

namespace Microsoft.DotNet.ProjectModel
{
    /// <summary>
    /// Represents an MSBuild project.
    /// It has been built by MSBuild, so it behaves like a package: can provide all assets up front
    /// 
    /// The Path represents the path to the project.json, if there is one near the csproj file.
    /// </summary>
    public class MSBuildProjectDescription : TargetLibraryWithAssets
    {
        public MSBuildProjectDescription(
            string path, 
            string msbuildProjectPath, 
            LockFileProjectLibrary projectLibrary, 
            LockFileTargetLibrary lockFileLibrary, 
            IEnumerable<LibraryRange> dependencies, 
            bool compatible, 
            bool resolved)
            : base(
                  new LibraryIdentity(projectLibrary.Name, projectLibrary.Version, LibraryType.MSBuildProject),
                  string.Empty, //msbuild projects don't have hashes
                  path,
                  lockFileLibrary,
                  dependencies,
                  resolved: resolved,
                  compatible: compatible,
                  framework: null)
        {
            ProjectLibrary = projectLibrary;
            MsbuildProjectPath = msbuildProjectPath;
        }

        public LockFileProjectLibrary ProjectLibrary { get; }
        public string MsbuildProjectPath { get; set; }
    }
}
