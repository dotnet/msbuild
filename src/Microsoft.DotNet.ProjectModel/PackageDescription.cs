// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;

namespace Microsoft.DotNet.ProjectModel
{
    public class PackageDescription : TargetLibraryWithAssets
    {
        public PackageDescription(
            string path,
            LockFilePackageLibrary package,
            LockFileTargetLibrary lockFileLibrary,
            IEnumerable<LibraryRange> dependencies,
            bool compatible,
            bool resolved)
            : base(
                  new LibraryIdentity(package.Name, package.Version, LibraryType.Package),
                  "sha512-" + package.Sha512,
                  path,
                  lockFileLibrary,
                  dependencies,
                  resolved: resolved,
                  compatible: compatible,
                  framework: null)
        {
            PackageLibrary = package;
        }

        public LockFilePackageLibrary PackageLibrary { get; }

        public override IEnumerable<LockFileItem> RuntimeAssemblies => FilterPlaceholders(base.RuntimeAssemblies);

        public override IEnumerable<LockFileItem> CompileTimeAssemblies => FilterPlaceholders(base.CompileTimeAssemblies);

        public bool HasCompileTimePlaceholder => base.CompileTimeAssemblies.Any() && base.CompileTimeAssemblies.All(a => PackageDependencyProvider.IsPlaceholderFile(a));

        private static IEnumerable<LockFileItem> FilterPlaceholders(IEnumerable<LockFileItem> items)
        {
            return items.Where(a => !PackageDependencyProvider.IsPlaceholderFile(a));
        }
    }
}
