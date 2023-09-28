// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using CLIRuntimeEnvironment = Microsoft.DotNet.Cli.Utils.RuntimeEnvironment;

namespace Microsoft.DotNet.Cli.Telemetry
{
    public class Telemetry : ITelemetry
    {
        internal static string CurrentSessionId = null;
        internal static bool DisabledForTests = false;
        private readonly int _senderCount;
        private TelemetryClient _client = null;
        private Dictionary<string, string> _commonProperties = null;
        private Dictionary<string, double> _commonMeasurements = null;
        private Task _trackEventTask = null;

        private const string ConnectionString = "InstrumentationKey=74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";

        public bool Enabled { get; }

        public Telemetry() : this(null) { }

        public Telemetry(IFirstTimeUseNoticeSentinel sentinel) : this(sentinel, null) { }

        public Telemetry(
            IFirstTimeUseNoticeSentinel sentinel,
            string sessionId,
            bool blockThreadInitialization = false,
            IEnvironmentProvider environmentProvider = null,
            int senderCount = 3)
        {

            if (DisabledForTests)
            {
                return;
            }

            if (environmentProvider == null)
            {
                environmentProvider = new EnvironmentProvider();
            }

            Enabled = !environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, defaultValue: CompileOptions.TelemetryOptOutDefault)
                        && PermissionExists(sentinel);

            if (!Enabled)
            {
                return;
            }

            // Store the session ID in a static field so that it can be reused
            CurrentSessionId = sessionId ?? Guid.NewGuid().ToString();
            _senderCount = senderCount;
            if (blockThreadInitialization)
            {
                InitializeTelemetry();
            }
            else
            {
                //initialize in task to offload to parallel thread
                _trackEventTask = Task.Run(() => InitializeTelemetry());
            }
        }

        internal static void DisableForTests()
        {
            DisabledForTests = true;
            CurrentSessionId = null;
        }

        internal static void EnableForTests()
        {
            DisabledForTests = false;
        }

        private bool PermissionExists(IFirstTimeUseNoticeSentinel sentinel)
        {
            if (sentinel == null)
            {
                return false;
            }

            return sentinel.Exists();
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties,
            IDictionary<string, double> measurements)
        {
            if (!Enabled)
            {
                return;
            }

            //continue the task in different threads
            _trackEventTask = _trackEventTask.ContinueWith(
                x => TrackEventTask(eventName, properties, measurements)
            );
        }

        public void Flush()
        {
            if (!Enabled || _trackEventTask == null)
            {
                return;
            }

            _trackEventTask.Wait();
        }

        // Adding dispose on graceful shutdown per https://github.com/microsoft/ApplicationInsights-dotnet/issues/1152#issuecomment-518742922
        public void Dispose()
        {
            if (_client != null)
            {
                _client.TelemetryConfiguration.Dispose();
                _client = null;
            }
        }

        public void ThreadBlockingTrackEvent(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements)
        {
            if (!Enabled)
            {
                return;
            }
            TrackEventTask(eventName, properties, measurements);
        }

        private void InitializeTelemetry()
        {
            try
            {
                var persistenceChannel = new PersistenceChannel.PersistenceChannel(sendersCount: _senderCount)
                {
                    SendingInterval = TimeSpan.FromMilliseconds(1)
                };

                var config = TelemetryConfiguration.CreateDefault();
                config.TelemetryChannel = persistenceChannel;
                config.ConnectionString = ConnectionString;
                _client = new TelemetryClient(config);
                _client.Context.Session.Id = CurrentSessionId;
                _client.Context.Device.OperatingSystem = CLIRuntimeEnvironment.OperatingSystem;

                _commonProperties = new TelemetryCommonProperties().GetTelemetryCommonProperties();
                _commonMeasurements = new Dictionary<string, double>();
            }
            catch (Exception e)
            {
                _client = null;
                // we dont want to fail the tool if telemetry fails.
                Debug.Fail(e.ToString());
            }
        }

        private void TrackEventTask(
            string eventName,
            IDictionary<string, string> properties,
            IDictionary<string, double> measurements)
        {
            if (_client == null)
            {
                return;
            }

            try
            {
                Dictionary<string, string> eventProperties = GetEventProperties(properties);
                Dictionary<string, double> eventMeasurements = GetEventMeasures(measurements);

                eventProperties.Add("event id", Guid.NewGuid().ToString());

                _client.TrackEvent(PrependProducerNamespace(eventName), eventProperties, eventMeasurements);
            }
            catch (Exception e)
            {
                Debug.Fail(e.ToString());
            }
        }

        private static string PrependProducerNamespace(string eventName)
        {
            return "dotnet/cli/" + eventName;
        }

        private Dictionary<string, double> GetEventMeasures(IDictionary<string, double> measurements)
        {
            Dictionary<string, double> eventMeasurements = new(_commonMeasurements);
            if (measurements != null)
            {
                foreach (KeyValuePair<string, double> measurement in measurements)
                {
                    eventMeasurements[measurement.Key] = measurement.Value;
                }
            }
            return eventMeasurements;
        }

        private Dictionary<string, string> GetEventProperties(IDictionary<string, string> properties)
        {
            var eventProperties = new Dictionary<string, string>(_commonProperties);
            if (properties != null)
            {
                foreach (KeyValuePair<string, string> property in properties)
                {
                    eventProperties[property.Key] = property.Value;
                }
            }

            return eventProperties;
        }
    }
}
