// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Configurer;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public sealed class MSBuildLogger : INodeLogger
    {
        private readonly IFirstTimeUseNoticeSentinel _sentinel =
            new FirstTimeUseNoticeSentinel();
        private readonly ITelemetry _telemetry;

        internal const string TargetFrameworkTelemetryEventName = "targetframeworkeval";
        internal const string BuildTelemetryEventName = "build";

        internal const string SdkTaskBaseCatchExceptionTelemetryEventName = "taskBaseCatchException";
        internal const string PublishPropertiesTelemetryEventName = "PublishProperties";
        internal const string ReadyToRunTelemetryEventName = "ReadyToRun";

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
            // Declare lack of dependency on having properties/items in ProjectStarted events
            // (since this logger doesn't ever care about those events it's irrelevant)
            if (eventSource is IEventSource4 eventSource4)
            {
                eventSource4.IncludeEvaluationPropertiesAndItems();
            }

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
            else if (args.EventName == BuildTelemetryEventName)
            {
                var newEventName = $"msbuild/{BuildTelemetryEventName}";
                Dictionary<string, string> properties = new Dictionary<string, string>(args.Properties);
                Dictionary<string, double> measurements = new Dictionary<string, double>();

                string[] toBeHashed = new[] { "ProjectPath", "BuildTarget" };
                foreach (var propertyToBeHashed in toBeHashed)
                {
                    if (properties.TryGetValue(propertyToBeHashed, out string value))
                    {
                        properties[propertyToBeHashed] = Sha256Hasher.HashWithNormalizedCasing(value);
                    }
                }

                string[] toBeMeasured = new[] { "BuildDurationInMilliseconds", "InnerBuildDurationInMilliseconds" };
                foreach (var propertyToBeMeasured in toBeMeasured)
                {
                    if (properties.TryGetValue(propertyToBeMeasured, out string value))
                    {
                        properties.Remove(propertyToBeMeasured);
                        if (double.TryParse(value, CultureInfo.InvariantCulture, out double realValue))
                        {
                            measurements[propertyToBeMeasured] = realValue;
                        }
                    }
                }

                telemetry.TrackEvent(newEventName, properties, measurements);
            }
            else
            {
                var passthroughEvents = new string[] {
                    SdkTaskBaseCatchExceptionTelemetryEventName, 
                    PublishPropertiesTelemetryEventName,
                    ReadyToRunTelemetryEventName };

                if (passthroughEvents.Contains(args.EventName))
                {
                    telemetry.TrackEvent(args.EventName, args.Properties, measurements: null);
                }
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
