// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.OpenTelemetry.ClientExtensions;
using Microsoft.VisualStudio.OpenTelemetry.ClientExtensions.Exporters;
using Microsoft.VisualStudio.OpenTelemetry.Collector.Interfaces;
using Microsoft.VisualStudio.OpenTelemetry.Collector.Settings;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Build.Framework.Telemetry
{
    /// <summary>
    /// A static class to instrument telemetry via OpenTelemetry.
    /// </summary>
    public static class FrameworkTelemetry
    {

        private const string OTelNamespace = "Microsoft.VisualStudio.OpenTelemetry.MSBuild";
        private const string vsMajorVersion = "17.0";
        private static IOpenTelemetryCollector? collector;
        private static TracerProvider? tracerProvider;
        private static MeterProvider? meterProvider;

        private static bool isInitialized;
        // private static ILoggerFactory? loggerFactory;
        public static Microsoft.Extensions.Logging.ILogger? logger;


        /// <summary>
        /// Gets an <see cref="ActivitySource"/> that is configured to create <see cref="Activity"/> objects
        /// that can get reported as a VS telemetry event when disposed.
        /// </summary>
        internal static MSBuildActivitySourceWrapper DefaultTelemetrySource { get; } = new();

        /// <summary>
        /// Configures the <see cref="DefaultTelemetrySource"/> to send telemetry through the Open Telemetry pipeline.
        /// </summary>
        /// <remarks>
        /// This should get called once at the start of the process. Subsequent calls are no-ops.
        /// If this is not called, then <see cref="Activity"/> objects created from <see cref="DefaultTelemetrySource"/> will always be <see langword="null"/>.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Enable()
        {
            // this relies on single thread being here
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;

            IOpenTelemetryExporterSettings defaultExporterSettings = OpenTelemetryExporterSettingsBuilder
                .CreateVSDefault(vsMajorVersion)
                .Build();
            IOpenTelemetryCollectorSettings collectorSettings = OpenTelemetryCollectorSettingsBuilder
                .CreateVSDefault(vsMajorVersion)
                .Build();

            using ILoggerFactory factory = LoggerFactory.Create(builder => { builder.AddOpenTelemetry(logging => { logging.AddVisualStudioDefaultLogExporter(defaultExporterSettings); logging.AddOtlpExporter(); }); });

            tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddVisualStudioDefaultTraceExporter(defaultExporterSettings)
                .AddOtlpExporter()
                .Build();
            logger = factory.CreateLogger(OTelNamespace);

            meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddVisualStudioDefaultMetricExporter(defaultExporterSettings)
                .Build();

            collector = OpenTelemetryCollectorProvider.CreateCollector(collectorSettings);
            collector.StartAsync();
        }

        private const string SamplePropertyPrefix = "VS.MSBuild.Event.";
        internal static void EndOfBuildTelemetry(BuildTelemetry buildTelemetry)
        {
            Enable();
#pragma warning disable CS8604 // Possible null reference argument.
            using var telemetryActivity = TelemetryHelpers.StartActivity("build", initialProperties: new
                 Dictionary<string, object>
                {
                    { "StartAt", buildTelemetry.StartAt?.ToString() },
                    { "InnerStartAt", buildTelemetry.InnerStartAt?.ToString() },
                    { "FinishedAt", buildTelemetry.FinishedAt?.ToString() },
                    { "Success", buildTelemetry.Success },
                    { "Target", buildTelemetry.Target },
                    { "ServerFallbackReason", buildTelemetry.ServerFallbackReason },
                    { "Version", buildTelemetry.Version?.ToString() },
                    { "DisplayVersion", buildTelemetry.DisplayVersion },
                    { "SAC", buildTelemetry.SACEnabled },
                    { "BuildCheckEnabled", buildTelemetry.BuildCheckEnabled },
                });
#pragma warning restore CS8604 // Possible null reference argument.
            telemetryActivity.AddBaggage("baggage", "hey");
            telemetryActivity.AddEvent(new ActivityEvent("hey2"));
            telemetryActivity.AddEvent(new ActivityEvent(OTelNamespace + "hey3"));
            telemetryActivity.SetStartTime(buildTelemetry.StartAt ?? DateTime.UtcNow);
            telemetryActivity.Stop();
            telemetryActivity.SetEndTime(buildTelemetry.FinishedAt ?? DateTime.UtcNow);
            telemetryActivity.SetCustomProperty(SamplePropertyPrefix + "hey", "hello");
            telemetryActivity.Dispose();
        }

        internal class MSBuildActivitySourceWrapper
        {
            private const string OTelNamespace = "Microsoft.VisualStudio.OpenTelemetry.MSBuild";
            internal MSBuildActivitySourceWrapper()
            {
                Source = new ActivitySource(OTelNamespace, vsMajorVersion);
            }
            public ActivitySource Source { get; }

            public string Name => Source.Name;

            public string? Version => Source.Version;


            public Activity StartActivity(string name = "", ActivityKind kind = ActivityKind.Internal)
            {
                // If the current activity has a remote parent, then we should start a child activity with the same parent ID.
                Activity? activity = Activity.Current?.HasRemoteParent is true
                    ? Source.StartActivity(name, kind, parentId: Activity.Current.ParentId)
                    : Source.StartActivity(name);

                if (activity is null)
                {
                    activity = new Activity(name);
                    activity.Start();
                }

                return activity;
            }
        }
    }
    public static class TelemetryHelpers
    {

        private const string EventPrefix = "VS/MSBuild/Event/";
        public static Activity StartActivity(string name, IDictionary<string, object> initialProperties)
        {
            return FrameworkTelemetry.DefaultTelemetrySource
                .StartActivity(EventPrefix + name, ActivityKind.Internal)
                .WithTags(initialProperties);
        }
        public static Activity WithTags(this Activity activity, IDictionary<string, object> tags)
        {
            if (tags is null)
            {
                return activity;
            }

            foreach (KeyValuePair<string, object> tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }

            return activity;
        }
    }
}
#endif
