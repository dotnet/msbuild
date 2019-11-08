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
    public sealed class MSBuildLogger : INodeLogger
    {
        private readonly IFirstTimeUseNoticeSentinel _sentinel =
            new FirstTimeUseNoticeSentinel();
        private readonly ITelemetry _telemetry;
        private const string NewEventName = "msbuild";
        internal const string TargetFrameworkTelemetryEventName = "targetframeworkeval";
        internal const string SdkTaskBaseCatchExceptionTelemetryEventName = "taskBaseCatchException";

        internal const string TargetFrameworkVersionTelemetryPropertyKey = "TargetFrameworkVersion";
        internal const string RuntimeIdentifierTelemetryPropertyKey = "RuntimeIdentifier";
        internal const string SelfContainedTelemetryPropertyKey = "SelfContained";
        internal const string UseApphostTelemetryPropertyKey = "UseApphost";
        internal const string OutputTypeTelemetryPropertyKey = "OutputType";

        public MSBuildLogger()
        {
            try
            {
                string sessionId =
                    Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);

                if (sessionId != null)
                {
                    // senderCount: 0 to disable sender.
                    // When senders in different process running at the same
                    // time they will read from the same global queue and cause
                    // sending duplicated events. Disable sender to reduce it.
                    _telemetry = new Telemetry(
                        _sentinel,
                        sessionId,
                        senderCount: 0);
                }
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }

        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        public void Initialize(IEventSource eventSource)
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
                Dictionary<string, string> maskedProperties = new Dictionary<string, string>();

                foreach (var key in new[] {
                    TargetFrameworkVersionTelemetryPropertyKey,
                    RuntimeIdentifierTelemetryPropertyKey,
                    SelfContainedTelemetryPropertyKey,
                    UseApphostTelemetryPropertyKey,
                    OutputTypeTelemetryPropertyKey
                })
                {
                    if (args.Properties.TryGetValue(key, out string value))
                    {
                        maskedProperties.Add(key, Sha256Hasher.HashWithNormalizedCasing(value));
                    }
                }

                telemetry.TrackEvent(newEventName, maskedProperties, measurements: null);
            }

            if (args.EventName == SdkTaskBaseCatchExceptionTelemetryEventName)
            {
                telemetry.TrackEvent(args.EventName, args.Properties, measurements: null);
            }
        }

        private void OnTelemetryLogged(object sender, TelemetryEventArgs args)
        {
            FormatAndSend(_telemetry, args);
        }

        public void Shutdown()
        {
            try
            {
                _sentinel?.Dispose();
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }

        public LoggerVerbosity Verbosity { get; set; }

        public string Parameters { get; set; }
    }
}
