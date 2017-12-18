// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Configurer;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public sealed class MSBuildLogger : Logger
    {
        private readonly IFirstTimeUseNoticeSentinel _sentinel =
            new FirstTimeUseNoticeSentinel(new CliFolderPathCalculator());
        private readonly ITelemetry _telemetry;
        private const string NewEventName = "msbuild";
        private const string TargetFrameworkTelemetryEventName = "targetframeworkeval";
        private const string TargetFrameworkVersionTelemetryPropertyKey= "TargetFrameworkVersion";

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

        internal static void FormatAndSend(ITelemetry telemetry, TelemetryEventArgs args)
        {
            if (args.EventName == TargetFrameworkTelemetryEventName)
            {
                var newEventName = $"msbuild/{TargetFrameworkTelemetryEventName}";
                Dictionary<string, string>  maskedProperties = new Dictionary<string, string>();
                if (args.Properties.TryGetValue(TargetFrameworkVersionTelemetryPropertyKey, out string value))
                {
                    maskedProperties.Add(TargetFrameworkVersionTelemetryPropertyKey, Sha256Hasher.HashWithNormalizedCasing(value));
                }

                telemetry.TrackEvent(newEventName, maskedProperties, measurements: null);
            }
        }

        private void OnTelemetryLogged(object sender, TelemetryEventArgs args)
        {
            FormatAndSend(_telemetry, args);
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
