// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstRunConfiguration
    {
        public bool GenerateAspNetCertificate { get; }

        public bool SkipFirstRunExperience { get; }

        public bool TelemetryOptout { get; }

        public DotnetFirstRunConfiguration(
            bool generateAspNetCertificate,
            bool skipFirstRunExperience,
            bool telemetryOptout)
        {
            GenerateAspNetCertificate = generateAspNetCertificate;
            SkipFirstRunExperience = skipFirstRunExperience;
            TelemetryOptout = telemetryOptout;
        }
    }
}
