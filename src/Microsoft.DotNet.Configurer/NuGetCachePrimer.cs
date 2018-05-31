// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    public class NuGetCachePrimer : INuGetCachePrimer
    {
        private readonly IFile _file;

        private readonly INuGetPackagesArchiver _nugetPackagesArchiver;

        private readonly INuGetCacheSentinel _nuGetCacheSentinel;

        public NuGetCachePrimer(INuGetPackagesArchiver nugetPackagesArchiver, INuGetCacheSentinel nuGetCacheSentinel)
            : this(nugetPackagesArchiver, nuGetCacheSentinel, FileSystemWrapper.Default.File)
        {
        }

        internal NuGetCachePrimer(
            INuGetPackagesArchiver nugetPackagesArchiver,
            INuGetCacheSentinel nuGetCacheSentinel,
            IFile file)
        {
            _nugetPackagesArchiver = nugetPackagesArchiver;

            _nuGetCacheSentinel = nuGetCacheSentinel;

            _file = file;
        }

        public void PrimeCache()
        {
            if (SkipPrimingTheCache())
            {
                return;
            }

            var nuGetFallbackFolder = CliFolderPathCalculator.CliFallbackFolderPath;

            _nugetPackagesArchiver.ExtractArchive(nuGetFallbackFolder);

            _nuGetCacheSentinel.CreateIfNotExists();
        }

        public bool SkipPrimingTheCache()
        {
            return !_file.Exists(_nugetPackagesArchiver.NuGetPackagesArchive);
        }
    }
}
