// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstTimeUseConfigurer
    {
        public static readonly string SENTINEL = $"{Product.Version}.dotnetSentinel";

        private IFile _file;
        private INuGetCachePrimer _nugetCachePrimer;
        private INuGetCacheResolver _nugetCacheResolver;

        public DotnetFirstTimeUseConfigurer(INuGetCachePrimer nugetCachePrimer, INuGetCacheResolver nugetCacheResolver)
            : this(nugetCachePrimer, nugetCacheResolver, FileSystemWrapper.Default.File)
        {
        }

        internal DotnetFirstTimeUseConfigurer(
            INuGetCachePrimer nugetCachePrimer,
            INuGetCacheResolver nugetCacheResolver,
            IFile file)
        {
            _file = file;
            _nugetCachePrimer = nugetCachePrimer;
            _nugetCacheResolver = nugetCacheResolver;
        }

        public void Configure()
        {
            if(ShouldPrimeNugetCache())
            {
                _nugetCachePrimer.PrimeCache();
            }
        }

        private bool ShouldPrimeNugetCache()
        {
            var nugetCachePath = _nugetCacheResolver.ResolveNugetCachePath();
            var sentinel = Path.Combine(nugetCachePath, SENTINEL);

            return !_file.Exists(sentinel);
        }
    }
}
