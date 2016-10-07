// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Versioning;
using System.IO;

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
            return Path.Combine(_root, packageId, version.ToNormalizedString(), "path");
        }
    }
}