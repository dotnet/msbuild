using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;

namespace Microsoft.DotNet.Cli
{
    public class Telemetry : ITelemetry
    {
        private bool _isInitialized = false;
        private bool _isCollectingTelemetry = false;
        private TelemetryClient _client = null;

        private Dictionary<string, string> _commonProperties = null;
        private Dictionary<string, double> _commonMeasurements = null;
        private Task _trackEventTask = null;

        private int _sampleRate = 1;
        private bool _isTestMachine = false;

        private const int ReciprocalSampleRateValue = 1;
        private const int ReciprocalSampleRateValueForTest = 1;
        private const string InstrumentationKey = "74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";
        private const string TelemetryOptout = "DOTNET_CLI_TELEMETRY_OPTOUT";
        private const string TestMachineFlag = "TEST_MACHINE";
        private const string TestMachine = "Test Machine";
        private const string OSVersion = "OS Version";
        private const string OSPlatform = "OS Platform";
        private const string RuntimeId = "Runtime Id";
        private const string ProductVersion = "Product Version";
        private const string ReciprocalSampleRate = "Reciprocal SampleRate";

        public bool Enabled { get; }

        public Telemetry()
        {
            Enabled = !Env.GetEnvironmentVariableAsBool(TelemetryOptout);

            if (!Enabled)
            {
                return;
            }

            _sampleRate = ReciprocalSampleRateValue;
            _isTestMachine = Env.GetEnvironmentVariableAsBool(TestMachineFlag);

            if(_isTestMachine)
            {
                _sampleRate = ReciprocalSampleRateValueForTest;
            }

            _isCollectingTelemetry = (Environment.TickCount % _sampleRate == 0);
            if(!_isCollectingTelemetry)
            {
                return;
            }

            try
            {
                using (PerfTrace.Current.CaptureTiming())
                {
                    //initialize in task to offload to parallel thread
                    _trackEventTask = Task.Factory.StartNew(() => InitializeTelemetry());
                }
            }
            catch(Exception)
            {
                Debug.Fail("Exception during telemetry task initialization");
            }
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements)
        {
            if (!Enabled)
            {
                return;
            }
            if (!_isCollectingTelemetry)
            {
                return;
            }
            try
            {
                using (PerfTrace.Current.CaptureTiming())
                {
                    //continue task in existing parallel thread
                    _trackEventTask = _trackEventTask.ContinueWith(
                        x => TrackEventTask(eventName, properties, measurements)
                    );
                }
            }
            catch(Exception)
            {
                Debug.Fail("Exception during telemetry task continuation");
            }
        }

        private void InitializeTelemetry()
        {
            try
            {
                _client = new TelemetryClient();
                _client.InstrumentationKey = InstrumentationKey;
                _client.Context.Session.Id = Guid.NewGuid().ToString();

                _client.Context.Device.OperatingSystem = RuntimeEnvironment.OperatingSystem;

                _commonProperties = new Dictionary<string, string>();
                _commonProperties.Add(OSVersion, RuntimeEnvironment.OperatingSystemVersion);
                _commonProperties.Add(OSPlatform, RuntimeEnvironment.OperatingSystemPlatform.ToString());
                _commonProperties.Add(RuntimeId, RuntimeEnvironment.GetRuntimeIdentifier());
                _commonProperties.Add(ProductVersion, Product.Version);
                _commonProperties.Add(TestMachine, _isTestMachine.ToString());
                _commonProperties.Add(ReciprocalSampleRate, _sampleRate.ToString());
                _commonMeasurements = new Dictionary<string, double>();
                _isInitialized = true;
            }
            catch(Exception)
            {
                _isInitialized = false;
                // we dont want to fail the tool if telemetry fails.
                Debug.Fail("Exception during telemetry initialization");
            }
        }

        private void TrackEventTask(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements)
        {
            if(!_isInitialized)
            {
                return;
            }

            try
            {
                var eventProperties = GetEventProperties(properties);
                var eventMeasurements = GetEventMeasures(measurements);

                _client.TrackEvent(eventName, eventProperties, eventMeasurements);
                _client.Flush();
            }
            catch (Exception)
            {
                Debug.Fail("Exception during TrackEventTask");
            }
        }

        private Dictionary<string, double> GetEventMeasures(IDictionary<string, double> measurements)
        {
            Dictionary<string, double> eventMeasurements = new Dictionary<string, double>(_commonMeasurements);
            if (measurements != null)
            {
                foreach (var measurement in measurements)
                {
                    if (eventMeasurements.ContainsKey(measurement.Key))
                    {
                        eventMeasurements[measurement.Key] = measurement.Value;
                    }
                    else
                    {
                        eventMeasurements.Add(measurement.Key, measurement.Value);
                    }
                }
            }
            return eventMeasurements;
        }

        private Dictionary<string, string> GetEventProperties(IDictionary<string, string> properties)
        {
            if (properties != null)
            {
                var eventProperties = new Dictionary<string, string>(_commonProperties);
                foreach (var property in properties)
                {
                    if (eventProperties.ContainsKey(property.Key))
                    {
                        eventProperties[property.Key] = property.Value;
                    }
                    else
                    {
                        eventProperties.Add(property.Key, property.Value);
                    }
                }
                return eventProperties;
            }
            else
            {
                return _commonProperties;
            }
        }
    }
}
