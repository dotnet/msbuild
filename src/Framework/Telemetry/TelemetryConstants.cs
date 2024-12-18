// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#if NETFRAMEWORK
using Microsoft.VisualStudio.OpenTelemetry.ClientExtensions;
using Microsoft.VisualStudio.OpenTelemetry.ClientExtensions.Exporters;
using Microsoft.VisualStudio.OpenTelemetry.Collector.Interfaces;
using Microsoft.VisualStudio.OpenTelemetry.Collector.Settings;
using OpenTelemetry;
using OpenTelemetry.Trace;
#endif

#if DEBUG && NETFRAMEWORK
using OpenTelemetry.Exporter;
#endif

namespace Microsoft.Build.Framework.Telemetry
{

    internal static class TelemetryConstants
    {
        /// <summary>
        /// "Microsoft.VisualStudio.OpenTelemetry.*" namespace is required by VS exporting/collection.
        /// </summary>
        public const string DefaultActivitySourceNamespace = "Microsoft.VisualStudio.OpenTelemetry.MSBuild";
        public const string EventPrefix = "VS/MSBuild/";
        public const string PropertyPrefix = "VS.MSBuild.";
        /// <summary>
        /// For VS OpenTelemetry Collector to apply the correct privacy policy.
        /// </summary>
        public const string VSMajorVersion = "17.0";

        /// <summary>
        /// https://learn.microsoft.com/en-us/dotnet/core/tools/telemetry
        /// </summary>
        public const string DotnetOptOut = "DOTNET_CLI_TELEMETRY_OPTOUT";
        public const string MSBuildOptout = "MSBUILD_TELEMETRY_OPTOUT";
    }

    /*
    internal class OTLPCollector : IDisposable
    {
        private readonly MSBuildActivitySource _activitySource;
        private TracerProvider? _tracerProvider;
        private MeterProvider? _meterProvider;

        public OTLPCollector(MSBuildActivitySource activitySource)
        {
            _activitySource = activitySource;
        }

        public OTLPCollector Initialize()
        {
            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(TelemetryConstants.MSBuildSourceName)
                .AddOtlpExporter()
                .Build();

            _meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(TelemetryConstants.MSBuildSourceName)
                .Build();

            return this;
        }

        public void Dispose()
        {
            _tracerProvider?.Dispose();
            _meterProvider?.Dispose();
        }
    }
    */
    /*
#if NETFRAMEWORK
    internal class VSCollector : IDisposable
    {
        private const string VsMajorVersion = "17.0";

        private readonly MSBuildActivitySource _activitySource;
        private IOpenTelemetryCollector? _collector;
        private TracerProvider? _tracerProvider;
        private MeterProvider? _meterProvider;

        public VSCollector(MSBuildActivitySource activitySource)
        {
            _activitySource = activitySource;
        }

        public VSCollector Initialize()
        {
            var exporterSettings = OpenTelemetryExporterSettingsBuilder
                .CreateVSDefault(VsMajorVersion)
                .Build();

            var collectorSettings = OpenTelemetryCollectorSettingsBuilder
                .CreateVSDefault(VsMajorVersion)
                .Build();

            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddVisualStudioDefaultTraceExporter(exporterSettings)
                .AddSource(TelemetryConstants.MSBuildSourceName)
                .Build();

            _meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddVisualStudioDefaultMetricExporter(exporterSettings)
                .AddMeter(TelemetryConstants.MSBuildSourceName)
                .Build();

            _collector = OpenTelemetryCollectorProvider.CreateCollector(collectorSettings);

            _collector.StartAsync();

            return this;
        }

        public void Dispose()
        {
            if (_collector != null)
            {
                _collector.Dispose();
            }
            _tracerProvider?.Dispose();
            _meterProvider?.Dispose();
        }
    }
#endif
*/
}
