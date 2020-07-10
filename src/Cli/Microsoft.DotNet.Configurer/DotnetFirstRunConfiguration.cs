// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstRunConfiguration
    {
        public bool GenerateAspNetCertificate { get; }

        public bool TelemetryOptout { get; }

        public bool AddGlobalToolsToPath { get; }

        public bool NoLogo { get; }

        public DotnetFirstRunConfiguration(
            bool generateAspNetCertificate,
            bool telemetryOptout,
            bool addGlobalToolsToPath, 
            bool nologo)
        {
            GenerateAspNetCertificate = generateAspNetCertificate;
            TelemetryOptout = telemetryOptout;
            AddGlobalToolsToPath = addGlobalToolsToPath;
            NoLogo = nologo;
        }
    }
}
