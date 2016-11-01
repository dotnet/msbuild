// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public sealed class MSBuildLogger : Logger
    {
        private readonly INuGetCacheSentinel _sentinel = new NuGetCacheSentinel();
        private readonly ITelemetry _telemetry;

        public MSBuildLogger()
        {
            string sessionId = Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);

            if (sessionId != null)
            {
                _telemetry = new Telemetry(_sentinel, sessionId);
            }
        }

        public override void Initialize(IEventSource eventSource)
        {
            if (_telemetry != null)
            {
                IEventSource2 eventSource2 = eventSource as IEventSource2;

                if (eventSource2 != null)
                {
                    eventSource2.TelemetryLogged += (sender, telemetryEventArgs) =>
                    {
                        Console.WriteLine($"Received telemetry event '{telemetryEventArgs.EventName}'");
                        _telemetry.TrackEvent(telemetryEventArgs.EventName, telemetryEventArgs.Properties, measurements: null);
                    };
                }
            }
        }

        public override void Shutdown()
        {
            _sentinel?.Dispose();

            base.Shutdown();
        }
    }
}
