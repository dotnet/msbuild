// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.Common;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Microsoft.DotNet.Core.Build.Tasks
{
    internal class NuGetPackageResolver : IPackageResolver
    {
        private readonly FallbackPackagePathResolver _packagePathResolver;

        public NuGetPackageResolver(INuGetPathContext pathContext)
        {
            _packagePathResolver = new FallbackPackagePathResolver(pathContext);
        }

        public string GetPackageDirectory(string packageId, NuGetVersion version)
        {
            return _packagePathResolver.GetPackageDirectory(packageId, version);
        }
    }
}
