// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstTimeUseConfigurer
    {
        private IReporter _reporter;
        private IEnvironmentProvider _environmentProvider;
        private INuGetCachePrimer _nugetCachePrimer;
        private INuGetCacheSentinel _nugetCacheSentinel;
        private IFirstTimeUseNoticeSentinel _firstTimeUseNoticeSentinel;

        public DotnetFirstTimeUseConfigurer(
            INuGetCachePrimer nugetCachePrimer,
            INuGetCacheSentinel nugetCacheSentinel,
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel,
            IEnvironmentProvider environmentProvider,
            IReporter reporter)
        {
            _nugetCachePrimer = nugetCachePrimer;
            _nugetCacheSentinel = nugetCacheSentinel;
            _firstTimeUseNoticeSentinel = firstTimeUseNoticeSentinel;
            _environmentProvider = environmentProvider;
            _reporter = reporter;
        }

        public void Configure()
        {
            if (ShouldPrintFirstTimeUseNotice())
            {
                PrintFirstTimeUseNotice();
            }

            if (ShouldPrimeNugetCache())
            {
                PrintNugetCachePrimeMessage();
                _nugetCachePrimer.PrimeCache();
            }
        }

        private bool ShouldPrintFirstTimeUseNotice()
        {
            var showFirstTimeUseNotice =
                _environmentProvider.GetEnvironmentVariableAsBool("DOTNET_PRINT_TELEMETRY_MESSAGE", true);

            return ShouldRunFirstRunExperience() &&
                showFirstTimeUseNotice &&
                !_firstTimeUseNoticeSentinel.Exists();
        }

        private void PrintFirstTimeUseNotice()
        {
            string firstTimeUseWelcomeMessage = LocalizableStrings.FirstTimeWelcomeMessage;

            _reporter.WriteLine();
            _reporter.WriteLine(firstTimeUseWelcomeMessage);

            _firstTimeUseNoticeSentinel.CreateIfNotExists();
        }

        private bool ShouldPrimeNugetCache()
        {
            return ShouldRunFirstRunExperience() &&
                !_nugetCachePrimer.SkipPrimingTheCache();
        }

        private void PrintNugetCachePrimeMessage()
        {
            string cachePrimeMessage = LocalizableStrings.NugetCachePrimeMessage;
            _reporter.WriteLine();
            _reporter.WriteLine(cachePrimeMessage);
        }

        private bool ShouldRunFirstRunExperience()
        {
            var skipFirstTimeExperience = 
                _environmentProvider.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", false);

            return !skipFirstTimeExperience &&
                !_nugetCacheSentinel.Exists() &&
                !_nugetCacheSentinel.InProgressSentinelAlreadyExists();
        }
    }
}
