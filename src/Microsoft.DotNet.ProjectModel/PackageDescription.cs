// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel.Graph;

namespace Microsoft.DotNet.ProjectModel
{
    public class PackageDescription : LibraryDescription
    {
        public PackageDescription(
            string path,
            LockFilePackageLibrary package,
            LockFileTargetLibrary lockFileLibrary,
            IEnumerable<LibraryRange> dependencies,
            bool compatible)
            : base(
                  new LibraryIdentity(package.Name, package.Version, LibraryType.Package),
                  "sha512-" + package.Sha512,
                  path,
                  dependencies: dependencies,
                  framework: null,
                  resolved: compatible,
                  compatible: compatible)
        {
            Library = package;
            Target = lockFileLibrary;
        }

        public LockFileTargetLibrary Target { get; set; }
        public LockFilePackageLibrary Library { get; set; }
    }
}
