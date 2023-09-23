// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public sealed class MSBuildLogger : INodeLogger
    {
        private readonly IFirstTimeUseNoticeSentinel _sentinel =
            new FirstTimeUseNoticeSentinel();
        private readonly ITelemetry _telemetry;

        internal const string TargetFrameworkTelemetryEventName = "targetframeworkeval";
        internal const string BuildTelemetryEventName = "build";
        internal const string LoggingConfigurationTelemetryEventName = "loggingConfiguration";

        internal const string SdkTaskBaseCatchExceptionTelemetryEventName = "taskBaseCatchException";
        internal const string PublishPropertiesTelemetryEventName = "PublishProperties";
        internal const string WorkloadPublishPropertiesTelemetryEventName = "WorkloadPublishProperties";
        internal const string ReadyToRunTelemetryEventName = "ReadyToRun";

        internal const string TargetFrameworkVersionTelemetryPropertyKey = "TargetFrameworkVersion";
        internal const string RuntimeIdentifierTelemetryPropertyKey = "RuntimeIdentifier";
        internal const string SelfContainedTelemetryPropertyKey = "SelfContained";
        internal const string UseApphostTelemetryPropertyKey = "UseApphost";
        internal const string OutputTypeTelemetryPropertyKey = "OutputType";
        internal const string UseArtifactsOutputTelemetryPropertyKey = "UseArtifactsOutput";
        internal const string ArtifactsPathLocationTypeTelemetryPropertyKey = "ArtifactsPathLocationType";

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
            switch (args.EventName)
            {
                case TargetFrameworkTelemetryEventName:
                    {
                        var newEventName = $"msbuild/{TargetFrameworkTelemetryEventName}";
                        Dictionary<string, string> maskedProperties = new();

                        foreach (var key in new[] {
                            TargetFrameworkVersionTelemetryPropertyKey,
                            RuntimeIdentifierTelemetryPropertyKey,
                            SelfContainedTelemetryPropertyKey,
                            UseApphostTelemetryPropertyKey,
                            OutputTypeTelemetryPropertyKey,
                            UseArtifactsOutputTelemetryPropertyKey,
                            ArtifactsPathLocationTypeTelemetryPropertyKey
                        })
                        {
                            if (args.Properties.TryGetValue(key, out string value))
                            {
                                maskedProperties.Add(key, Sha256Hasher.HashWithNormalizedCasing(value));
                            }
                        }

                        telemetry.TrackEvent(newEventName, maskedProperties, measurements: null);
                        break;
                    }
                case BuildTelemetryEventName:
                    TrackEvent(telemetry, $"msbuild/{BuildTelemetryEventName}", args.Properties,
                        toBeHashed: new[] { "ProjectPath", "BuildTarget" },
                        toBeMeasured: new[] { "BuildDurationInMilliseconds", "InnerBuildDurationInMilliseconds" });
                    break;
                case LoggingConfigurationTelemetryEventName:
                    TrackEvent(telemetry, $"msbuild/{LoggingConfigurationTelemetryEventName}", args.Properties,
                        toBeHashed: Array.Empty<string>(),
                        toBeMeasured: new[] { "FileLoggersCount" });
                    break;
                // Pass through events that don't need special handling
                case SdkTaskBaseCatchExceptionTelemetryEventName:
                case PublishPropertiesTelemetryEventName:
                case ReadyToRunTelemetryEventName:
                case WorkloadPublishPropertiesTelemetryEventName:
                    TrackEvent(telemetry, args.EventName, args.Properties, Array.Empty<string>(), Array.Empty<string>() );
                    break;
                default:
                    // Ignore unknown events
                    break;
            }
        }

        private static void TrackEvent(ITelemetry telemetry, string eventName, IDictionary<string, string> eventProperties, string[] toBeHashed, string[] toBeMeasured)
        {
            Dictionary<string, string> properties = null;
            Dictionary<string, double> measurements = null;

            foreach (var propertyToBeHashed in toBeHashed)
            {
                if (eventProperties.TryGetValue(propertyToBeHashed, out string value))
                {
                    // Lets lazy allocate in case there is tons of telemetry 
                    properties ??= new Dictionary<string, string>(eventProperties);
                    properties[propertyToBeHashed] = Sha256Hasher.HashWithNormalizedCasing(value);
                }
            }

            foreach (var propertyToBeMeasured in toBeMeasured)
            {
                if (eventProperties.TryGetValue(propertyToBeMeasured, out string value))
                {
                    // Lets lazy allocate in case there is tons of telemetry 
                    properties ??= new Dictionary<string, string>(eventProperties);
                    properties.Remove(propertyToBeMeasured);
                    if (double.TryParse(value, CultureInfo.InvariantCulture, out double realValue))
                    {
                        // Lets lazy allocate in case there is tons of telemetry
                        measurements ??= new Dictionary<string, double>();
                        measurements[propertyToBeMeasured] = realValue;
                    }
                }
            }

            telemetry.TrackEvent(eventName, properties ?? eventProperties, measurements);
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
