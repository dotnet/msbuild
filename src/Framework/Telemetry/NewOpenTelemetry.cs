// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
#if NETFRAMEWORK
using Microsoft.VisualStudio.OpenTelemetry.ClientExtensions;
using Microsoft.VisualStudio.OpenTelemetry.ClientExtensions.Exporters;
using Microsoft.VisualStudio.OpenTelemetry.Collector.Interfaces;
using Microsoft.VisualStudio.OpenTelemetry.Collector.Settings;
#endif
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Build.Framework.Telemetry
{

    public static class TelemetryConstants
    {
        public const string VSNamespace = "Microsoft.VisualStudio.OpenTelemetry.MSBuild";
        public const string MSBuildSourceName = "Microsoft.Build";
        public const string EventPrefix = "VS/MSBuild/";
        public const string PropertyPrefix = "VS.MSBuild.";
        public const string Version = "1.0.0";
    }

    public class TelemetryConfiguration
    {
        private static readonly Lazy<TelemetryConfiguration> _instance =
            new(() => new TelemetryConfiguration());

        public static TelemetryConfiguration Instance => _instance.Value;

        // Will be populated with actual env vars later
        public const string OptOutEnvVar = "PLACEHOLDER_OPTOUT";
        public const string VSTelemetryOptOutEnvVar = "PLACEHOLDER_VS_OPTOUT";
        public const string OTLPExportEnvVar = "PLACEHOLDER_OTLP_ENABLE";
        public const string NoCollectorsEnvVar = "PLACEHOLDER_NO_COLLECTORS";

        private TelemetryConfiguration()
        {
            RefreshConfiguration();
        }

        public bool IsEnabled { get; private set; }
        public bool IsVSTelemetryEnabled { get; private set; }
        public bool IsOTLPExportEnabled { get; private set; }
        public bool ShouldInitializeCollectors { get; private set; }

        public void RefreshConfiguration()
        {
            IsEnabled = !IsEnvVarEnabled(OptOutEnvVar);
            IsVSTelemetryEnabled = IsEnabled && !IsEnvVarEnabled(VSTelemetryOptOutEnvVar);
            // IsOTLPExportEnabled = IsEnabled && IsEnvVarEnabled(OTLPExportEnvVar);
#if DEBUG
            IsOTLPExportEnabled = true;
#endif
            ShouldInitializeCollectors = IsEnabled && !IsEnvVarEnabled(NoCollectorsEnvVar);
        }

        private static bool IsEnvVarEnabled(string name) =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name));
    }

    public static class BuildTelemetryManager
    {
        private static ITelemetrySession? _currentSession;

        public static void Initialize(bool isVisualStudioBuild, string? hostName = null)
        {
            if (_currentSession != null)
            {
                throw new InvalidOperationException("Telemetry session already initialized");
            }

            _currentSession = TelemetrySessionFactory.Create(isVisualStudioBuild, hostName);
        }

        public static void Shutdown()
        {
            if (_currentSession != null)
            {
                _currentSession.Dispose();
                _currentSession = null;
            }
        }

        public static Activity? StartActivity(string name, IDictionary<string, object>? tags = null)
        {
            return _currentSession?.StartActivity(
                $"{TelemetryConstants.EventPrefix}{name}",
                tags?.ToDictionary(
                    kvp => $"{TelemetryConstants.PropertyPrefix}{kvp.Key}",
                    kvp => kvp.Value));
        }
    }

    // This would be internal in reality, shown here for completeness
    internal interface ITelemetrySession : IDisposable
    {
        Activity? StartActivity(string name, IDictionary<string, object>? tags = null);
    }
    internal static class TelemetrySessionFactory
    {
        public static ITelemetrySession Create(bool isVisualStudioBuild, string? hostName)
        {
            var session = new TelemetrySession(isVisualStudioBuild, hostName);
            session.Initialize();
            return session;
        }
    }

    internal class TelemetrySession : ITelemetrySession
    {
        private readonly bool _isVisualStudioBuild;
        private readonly string? _hostName;
        private readonly MSBuildActivitySource _activitySource;
        private readonly List<IDisposable> _collectors;
        private bool _isDisposed;

        public TelemetrySession(bool isVisualStudioBuild, string? hostName)
        {
            _isVisualStudioBuild = isVisualStudioBuild;
            _hostName = hostName;
            _activitySource = new MSBuildActivitySource();
            _collectors = new();
        }

        public void Initialize()
        {
            var config = TelemetryConfiguration.Instance;

            if (config.IsOTLPExportEnabled)
            {
                _collectors.Add(new OTLPCollector(_activitySource).Initialize());
            }

#if NETFRAMEWORK
            if (_isVisualStudioBuild && config.IsVSTelemetryEnabled)
            {
                _collectors.Add(new VSCollector(_activitySource).Initialize());
            }
#endif
        }

        public Activity? StartActivity(string name, IDictionary<string, object>? tags = null)
        {
            if (_isDisposed)
            {
                return null;
            }

            return _activitySource.StartActivity(name, tags);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            foreach (var collector in _collectors)
            {
                collector.Dispose();
            }

            _collectors.Clear();
        }
    }

    internal class MSBuildActivitySource
    {
        private readonly ActivitySource _source;

        public MSBuildActivitySource()
        {
            _source = new ActivitySource(
                TelemetryConstants.MSBuildSourceName,
                TelemetryConstants.Version);
        }

        public Activity? StartActivity(string name, IDictionary<string, object>? tags)
        {
            var activity = Activity.Current?.HasRemoteParent == true
                ? _source.StartActivity(name, ActivityKind.Internal, parentId: Activity.Current.ParentId)
                : _source.StartActivity(name);

            if (activity != null && tags != null)
            {
                foreach (var tag in tags)
                {
                    activity.SetTag(tag.Key, tag.Value);
                }
            }

            return activity;
        }
    }

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

}

