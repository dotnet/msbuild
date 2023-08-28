// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstRunConfiguration
    {
        public bool GenerateAspNetCertificate { get; }

        public bool TelemetryOptout { get; }

        public bool AddGlobalToolsToPath { get; }

        public bool NoLogo { get; }

        public bool SkipWorkloadIntegrityCheck { get; }

        public DotnetFirstRunConfiguration(
            bool generateAspNetCertificate,
            bool telemetryOptout,
            bool addGlobalToolsToPath,
            bool nologo,
            bool skipWorkloadIntegrityCheck)
        {
            GenerateAspNetCertificate = generateAspNetCertificate;
            TelemetryOptout = telemetryOptout;
            AddGlobalToolsToPath = addGlobalToolsToPath;
            NoLogo = nologo;
            SkipWorkloadIntegrityCheck = skipWorkloadIntegrityCheck;
        }
    }
}
