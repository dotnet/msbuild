// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstTimeUseConfigurer
    {
        private INuGetCachePrimer _nugetCachePrimer;
        private INuGetCacheSentinel _nugetCacheSentinel;

        public DotnetFirstTimeUseConfigurer(INuGetCachePrimer nugetCachePrimer, INuGetCacheSentinel nugetCacheSentinel)
        {
            _nugetCachePrimer = nugetCachePrimer;
            _nugetCacheSentinel = nugetCacheSentinel;
        }

        public void Configure()
        {
            if(ShouldPrimeNugetCache())
            {
                Reporter.Output.WriteLine("Configuring dotnet CLI for first time use.");
                _nugetCachePrimer.PrimeCache();
            }
        }

        private bool ShouldPrimeNugetCache()
        {
            return !_nugetCacheSentinel.Exists();
        }
    }
}
