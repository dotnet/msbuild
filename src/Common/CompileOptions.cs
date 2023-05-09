// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
