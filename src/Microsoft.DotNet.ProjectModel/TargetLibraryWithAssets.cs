// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel
{
    public abstract class TargetLibraryWithAssets : LibraryDescription
    {
        public TargetLibraryWithAssets(
            LibraryIdentity libraryIdentity,
            string sha512,
            string path,
            LockFileTargetLibrary lockFileLibrary,
            IEnumerable<ProjectLibraryDependency> dependencies,
            bool compatible,
            bool resolved,
            NuGetFramework framework = null)
            : base(
                  libraryIdentity,
                  sha512,
                  path,
                  dependencies: dependencies,
                  framework: null,
                  resolved: resolved,
                  compatible: compatible)
        {
            TargetLibrary = lockFileLibrary;
        }

        private LockFileTargetLibrary TargetLibrary { get; }

        public virtual IEnumerable<LockFileItem> RuntimeAssemblies => TargetLibrary.RuntimeAssemblies;

        public virtual IEnumerable<LockFileItem> CompileTimeAssemblies => TargetLibrary.CompileTimeAssemblies;

        public virtual IEnumerable<LockFileItem> ResourceAssemblies => TargetLibrary.ResourceAssemblies;

        public virtual IEnumerable<LockFileItem> NativeLibraries => TargetLibrary.NativeLibraries;

        public virtual IEnumerable<LockFileContentFile> ContentFiles => TargetLibrary.ContentFiles;

        public virtual IEnumerable<LockFileRuntimeTarget> RuntimeTargets => TargetLibrary.RuntimeTargets;
    }
}
