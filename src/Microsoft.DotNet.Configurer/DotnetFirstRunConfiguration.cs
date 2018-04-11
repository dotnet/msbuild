// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Configurer
{
    public class DotnetFirstRunConfiguration
    {
        public bool GenerateAspNetCertificate { get; set; }

        public bool PrintTelemetryMessage { get; set; }

        public bool SkipFirstRunExperience { get; set; }
    }
}