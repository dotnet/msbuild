// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.ProjectModel.Resolution;

namespace Microsoft.DotNet.Configurer
{
    public class NuGetCacheSentinel : INuGetCacheSentinel
    {
        public static readonly string SENTINEL = $"{Product.Version}.dotnetSentinel";

        private readonly IFile _file;

        public NuGetCacheSentinel() : this(FileSystemWrapper.Default.File)
        {
        }

        internal NuGetCacheSentinel(IFile file)
        {
            _file = file;
        }

        public bool Exists()
        {
            var nugetCachePath = PackageDependencyProvider.ResolvePackagesPath(null, null);
            var sentinel = Path.Combine(nugetCachePath, SENTINEL);

            return !_file.Exists(sentinel);
        }
    }
}
