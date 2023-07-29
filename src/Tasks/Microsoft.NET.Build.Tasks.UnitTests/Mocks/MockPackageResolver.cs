// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class MockPackageResolver : IPackageResolver
    {
        private readonly string _root;

        public MockPackageResolver(string root = "/root")
        {
            _root = root;
        }
        public string GetPackageDirectory(string packageId, NuGetVersion version)
        {
            string packageRoot = null;
            return GetPackageDirectory(packageId, version, out packageRoot);
        }
        public string GetPackageDirectory(string packageId, NuGetVersion version, out string packageRoot)
        {
            packageRoot = _root;
            return Path.Combine(_root, packageId, version.ToNormalizedString(), "path");
        }

        public string ResolvePackageAssetPath(LockFileTargetLibrary package, string relativePath) => Path.Combine(GetPackageDirectory(package.Name, package.Version), relativePath);
    }
}
