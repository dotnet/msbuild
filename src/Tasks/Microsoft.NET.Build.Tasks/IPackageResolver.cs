// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    internal interface IPackageResolver
    {
        string GetPackageDirectory(string packageId, NuGetVersion version);
        string GetPackageDirectory(string packageId, NuGetVersion version, out string packageRoot);
        string ResolvePackageAssetPath(LockFileTargetLibrary package, string relativePath);
    }
}
