// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#if NETFRAMEWORK
using Microsoft.VisualStudio.OpenTelemetry.ClientExtensions;
using Microsoft.VisualStudio.OpenTelemetry.ClientExtensions.Exporters;
using Microsoft.VisualStudio.OpenTelemetry.Collector.Interfaces;
using Microsoft.VisualStudio.OpenTelemetry.Collector.Settings;
using OpenTelemetry;
using OpenTelemetry.Trace;
#endif
using System;
using System.Diagnostics;
using System.Threading;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework.Telemetry
{

    /// <summary>
    /// Singleton class for configuring and managing the telemetry infrastructure with System.Diagnostics.Activity,
    /// OpenTelemetry SDK, and VS OpenTelemetry Collector.
    /// </summary>
    internal class OpenTelemetryManager
    {
        // Lazy<T> provides thread-safe lazy initialization.
        private static readonly Lazy<OpenTelemetryManager> s_instance =
            new Lazy<OpenTelemetryManager>(() => new OpenTelemetryManager(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Globally accessible instance of <see cref="OpenTelemetryManager"/>.
        /// </summary>
        public static OpenTelemetryManager Instance => s_instance.Value;

        private TelemetryState _telemetryState = TelemetryState.Uninitialized;
        private readonly object _initializeLock = new();
        private double _sampleRate = TelemetryConstants.DefaultSampleRate;

#if NETFRAMEWORK
        private TracerProvider? _tracerProvider;
        private IOpenTelemetryCollector? _collector;
#endif

        /// <summary>
        /// Optional activity source for MSBuild or other telemetry usage.
        /// </summary>
        public MSBuildActivitySource? DefaultActivitySource { get; private set; }

        private OpenTelemetryManager()
        {
        }

        /// <summary>
        /// Initializes the telemetry infrastructure. Multiple invocations are no-op, thread-safe.
        /// </summary>
        /// <param name="isStandalone">Differentiates between executing as MSBuild.exe or from VS/API.</param>
        public void Initialize(bool isStandalone)
        {
            // for lock free early exit
            if (_telemetryState != TelemetryState.Uninitialized)
            {
                return;
            }

            lock (_initializeLock)
            {
                // for correctness
                if (_telemetryState != TelemetryState.Uninitialized)
                {
                    return;
                }

                if (IsOptOut())
                {
                    _telemetryState = TelemetryState.OptOut;
                    return;
                }

                // TODO: temporary until we have green light to enable telemetry perf-wise
                if (!IsOptIn())
                {
                    _telemetryState = TelemetryState.Unsampled;
                    return;
                }

                if (!IsSampled())
                {
                    _telemetryState = TelemetryState.Unsampled;
                    return;
                }

                InitializeActivitySources();
            }
#if NETFRAMEWORK
            try
            {
                InitializeTracerProvider();

                // TODO: Enable commented logic when Collector is present in VS
                // if (isStandalone)
                InitializeCollector();

                // }
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException or System.IO.FileLoadException)
            {
                // catch exceptions from loading the OTel SDK or Collector to maintain usability of Microsoft.Build.Framework package in our and downstream tests in VS.
                _telemetryState = TelemetryState.Unsampled;
                return;
            }
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // avoid assembly loads
        private void InitializeActivitySources()
        {
            _telemetryState = TelemetryState.TracerInitialized;
            DefaultActivitySource = new MSBuildActivitySource(TelemetryConstants.DefaultActivitySourceNamespace, _sampleRate);
        }

#if NETFRAMEWORK
        /// <summary>
        /// Initializes the OpenTelemetry SDK TracerProvider with VS default exporter settings.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)] // avoid assembly loads
        private void InitializeTracerProvider()
        {
            var exporterSettings = OpenTelemetryExporterSettingsBuilder
                .CreateVSDefault(TelemetryConstants.VSMajorVersion)
                .Build();

            TracerProviderBuilder tracerProviderBuilder = Sdk
                .CreateTracerProviderBuilder()
                                // this adds listeners to ActivitySources with the prefix "Microsoft.VisualStudio.OpenTelemetry."
                                .AddVisualStudioDefaultTraceExporter(exporterSettings);

            _tracerProvider = tracerProviderBuilder.Build();
            _telemetryState = TelemetryState.ExporterInitialized;
        }

        /// <summary>
        /// Initializes the VS OpenTelemetry Collector with VS default settings.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)] // avoid assembly loads
        private void InitializeCollector()
        {
            IOpenTelemetryCollectorSettings collectorSettings = OpenTelemetryCollectorSettingsBuilder
                .CreateVSDefault(TelemetryConstants.VSMajorVersion)
                .Build();

            _collector = OpenTelemetryCollectorProvider.CreateCollector(collectorSettings);
            _collector.StartAsync().GetAwaiter().GetResult();

            _telemetryState = TelemetryState.CollectorInitialized;
        }
#endif
        [MethodImpl(MethodImplOptions.NoInlining)] // avoid assembly loads
        private void ForceFlushInner()
        {
#if NETFRAMEWORK
            _tracerProvider?.ForceFlush();
#endif
        }

        /// <summary>
        /// Flush the telemetry in TracerProvider/Exporter.
        /// </summary>
        public void ForceFlush()
        {
            if (ShouldBeCleanedUp())
            {
                ForceFlushInner();
            }
        }

        // to avoid assembly loading OpenTelemetry in tests
        [MethodImpl(MethodImplOptions.NoInlining)] // avoid assembly loads
        private void ShutdownInner()
        {
#if NETFRAMEWORK
            _tracerProvider?.Shutdown();
            // Dispose stops the collector, with a default drain timeout of 10s
            _collector?.Dispose();
#endif
        }

        /// <summary>
        /// Shuts down the telemetry infrastructure.
        /// </summary>
        public void Shutdown()
        {
            lock (_initializeLock)
            {
                if (ShouldBeCleanedUp())
                {
                    ShutdownInner();
                }

                _telemetryState = TelemetryState.Disposed;
            }
        }

        /// <summary>
        /// Determines if the user has explicitly opted out of telemetry.
        /// </summary>
        private bool IsOptOut() => Traits.Instance.FrameworkTelemetryOptOut || Traits.Instance.SdkTelemetryOptOut || !ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_14);

        /// <summary>
        /// TODO: Temporary until perf of loading OTel is agreed to in VS.
        /// </summary>
        private bool IsOptIn() => !IsOptOut() && (Traits.Instance.TelemetryOptIn || Traits.Instance.TelemetrySampleRateOverride.HasValue);

        /// <summary>
        /// Determines if telemetry should be initialized based on sampling and environment variable overrides.
        /// </summary>
        private bool IsSampled()
        {
            double? overrideRate = Traits.Instance.TelemetrySampleRateOverride;
            if (overrideRate.HasValue)
            {
                _sampleRate = overrideRate.Value;
            }
            else
            {
#if !NETFRAMEWORK
                // In core, OTel infrastructure is not initialized by default.
                return false;
#endif
            }

            // Simple random sampling, this method is called once, no need to save the Random instance.
            Random random = new();
            return random.NextDouble() < _sampleRate;
        }

        private bool ShouldBeCleanedUp() => _telemetryState == TelemetryState.CollectorInitialized || _telemetryState == TelemetryState.ExporterInitialized;

        internal bool IsActive() => _telemetryState == TelemetryState.TracerInitialized || _telemetryState == TelemetryState.CollectorInitialized || _telemetryState == TelemetryState.ExporterInitialized;

        /// <summary>
        /// State of the telemetry infrastructure.
        /// </summary>
        internal enum TelemetryState
        {
            /// <summary>
            /// Initial state.
            /// </summary>
            Uninitialized,

            /// <summary>
            /// Opt out of telemetry.
            /// </summary>
            OptOut,

            /// <summary>
            /// Run not sampled for telemetry.
            /// </summary>
            Unsampled,

            /// <summary>
            /// For core hook, ActivitySource is created.
            /// </summary>
            TracerInitialized,

            /// <summary>
            /// For VS scenario with a collector. ActivitySource, OTel TracerProvider are created.
            /// </summary>
            ExporterInitialized,

            /// <summary>
            /// For standalone, ActivitySource, OTel TracerProvider, VS OpenTelemetry Collector are created.
            /// </summary>
            CollectorInitialized,

            /// <summary>
            /// End state.
            /// </summary>
            Disposed
        }
    }
}
