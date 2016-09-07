// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Versioning;
using System.IO;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    public class MockPackageResolver : IPackageResolver
    {
        public string GetPackageDirectory(string packageId, NuGetVersion version)
        {
            return Path.Combine("/root", packageId, version.ToNormalizedString(), "path");
        }
    }
}