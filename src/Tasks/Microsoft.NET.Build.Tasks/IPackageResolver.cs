// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    internal interface IPackageResolver
    {
        string GetPackageDirectory(string packageId, NuGetVersion version);
        string GetPackageDirectory(string packageId, NuGetVersion version, out string packageRoot);
    }
}
