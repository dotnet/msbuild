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

        private string _nugetCachePath;

        private string NuGetCachePath
        {
            get
            {
                if (string.IsNullOrEmpty(_nugetCachePath))
                {
                    _nugetCachePath = PackageDependencyProvider.ResolvePackagesPath(null, null);
                }

                return _nugetCachePath;
            }
        }

        private string Sentinel => Path.Combine(NuGetCachePath, SENTINEL);

        public NuGetCacheSentinel() : this(string.Empty, FileSystemWrapper.Default.File)
        {
        }

        internal NuGetCacheSentinel(string nugetCachePath, IFile file)
        {
            _file = file;
            _nugetCachePath = nugetCachePath;
        }

        public bool Exists()
        {
            return _file.Exists(Sentinel);
        }

        public void CreateIfNotExists()
        {
            if (!Exists())
            {
                _file.CreateEmptyFile(Sentinel);
            }
        }
    }
}
