// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public sealed class MSBuildLogger : Logger
    {
        private readonly IFirstTimeUseNoticeSentinel _sentinel =
            new FirstTimeUseNoticeSentinel(new CliFallbackFolderPathCalculator());
        private readonly ITelemetry _telemetry;

        public MSBuildLogger()
        {
            try
            {
                string sessionId =
                    Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);

                if (sessionId != null)
                {
                    _telemetry = new Telemetry(_sentinel, sessionId);
                }
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }

        public override void Initialize(IEventSource eventSource)
        {
            try
            {
                if (_telemetry != null && _telemetry.Enabled)
                {
                    if (eventSource is IEventSource2 eventSource2)
                    {
                        eventSource2.TelemetryLogged += OnTelemetryLogged;
                    }
                }
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }

        private void OnTelemetryLogged(object sender, TelemetryEventArgs args)
        {
            _telemetry.TrackEvent(args.EventName, args.Properties, measurements: null);
        }

        public override void Shutdown()
        {
            try
            {
                _sentinel?.Dispose();
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }

            base.Shutdown();
        }
    }
}
