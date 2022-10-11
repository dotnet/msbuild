// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Reflection;

#if MICROSOFT_ENABLE_TELEMETRY
      [assembly: AssemblyMetadata("TelemetryOptOutDefault", Microsoft.DotNet.Cli.CompileOptions.TelemetryOptOutDefaultString)]
#endif

namespace Microsoft.DotNet.Cli
{
    static class CompileOptions
    {
        public const bool TelemetryOptOutDefault =
#if MICROSOFT_ENABLE_TELEMETRY
        false;
#else
        true;
#endif
        public const string TelemetryOptOutDefaultString =
#if MICROSOFT_ENABLE_TELEMETRY
        "False";
#else
        "True";
#endif
    }
}
