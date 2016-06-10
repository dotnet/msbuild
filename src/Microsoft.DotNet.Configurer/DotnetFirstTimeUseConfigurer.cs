// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstTimeUseConfigurer
    {
        private IEnvironmentProvider _environmentProvider;
        private INuGetCachePrimer _nugetCachePrimer;
        private INuGetCacheSentinel _nugetCacheSentinel;

        public DotnetFirstTimeUseConfigurer(
            INuGetCachePrimer nugetCachePrimer,
            INuGetCacheSentinel nugetCacheSentinel,
            IEnvironmentProvider environmentProvider)
        {
            _nugetCachePrimer = nugetCachePrimer;
            _nugetCacheSentinel = nugetCacheSentinel;
            _environmentProvider = environmentProvider;
        }

        public void Configure()
        {
            if(ShouldPrimeNugetCache())
            {
                PrintFirstTimeUseNotice();

                _nugetCachePrimer.PrimeCache();
            }
        }

        private void PrintFirstTimeUseNotice()
        {
            const string firstTimeUseWelcomeMessage = @"Welcome to .NET Core!
---------------------
Learn more about .NET Core @ https://aka.ms/dotnet-docs. Use dotnet --help to see available commands or go to https://aka.ms/dotnet-cli-docs.
Telemetry
--------------
The .NET Core tools collect usage data in order to improve your experience. The data is anonymous and does not include commandline arguments. The data is collected by Microsoft and shared with the community.
You can opt out of telemetry by setting a DOTNET_CLI_TELEMETRY_OPTOUT environment variable to 1 using your favorite shell.
You can read more about .NET Core tools telemetry @ https://aka.ms/dotnet-cli-telemetry.
Configuring...
-------------------
A command is running to initially populate your local package cache, to improve restore speed and enable offline access. This command will take up to a minute to complete and will only happen once.";

            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine(firstTimeUseWelcomeMessage);
        }

        private bool ShouldPrimeNugetCache()
        {
            var skipFirstTimeExperience = 
                _environmentProvider.GetEnvironmentVariableAsBool("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", false);

            return !skipFirstTimeExperience &&
                !_nugetCacheSentinel.Exists() &&
                !_nugetCacheSentinel.InProgressSentinelAlreadyExists();
        }
    }
}
