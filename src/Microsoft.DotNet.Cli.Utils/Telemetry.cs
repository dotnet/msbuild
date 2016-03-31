using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.PlatformAbstractions;
using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Telemetry : ITelemetry
    {
        private static bool _isInitialized = false;
        private static TelemetryClient _client = null;

        private static Dictionary<string, string> _commonProperties = null;
        private static Dictionary<string, double> _commonMeasurements = null;

        private const string InstrumentationKey = "74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";
        private const string TelemetryOptout = "DOTNET_CLI_TELEMETRY_OPTOUT";
        private const string OSVersion = "OS Version";
        private const string OSPlatform = "OS Platform";
        private const string RuntimeId = "Runtime Id";
        private const string ProductVersion = "Product Version";

        public Telemetry()
        {
            bool optout = Env.GetEnvironmentVariableAsBool(TelemetryOptout);

            if (optout)
            {
                return;
            }

            try
            {
                _client = new TelemetryClient();
                _client.InstrumentationKey = InstrumentationKey;
                _client.Context.Session.Id = Guid.NewGuid().ToString();

                var runtimeEnvironment = PlatformServices.Default.Runtime;
                _client.Context.Device.OperatingSystem = runtimeEnvironment.OperatingSystem;

                _commonProperties = new Dictionary<string, string>();
                _commonProperties.Add(OSVersion, runtimeEnvironment.OperatingSystemVersion);
                _commonProperties.Add(OSPlatform, runtimeEnvironment.OperatingSystemPlatform.ToString());
                _commonProperties.Add(RuntimeId, runtimeEnvironment.GetRuntimeIdentifier());
                _commonProperties.Add(ProductVersion, Product.Version);
                _commonMeasurements = new Dictionary<string, double>();

                _isInitialized = true;
            }
            catch (Exception)
            {
                // we dont want to fail the tool if telemetry fais. We should be able to detect abnormalities from data 
                // at the server end
            }
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements)
        {
            if (!_isInitialized)
            {
                return;
            }

            Dictionary<string, double> eventMeasurements = GetEventMeasures(measurements);
            Dictionary<string, string> eventProperties = GetEventProperties(properties);

            try
            {
                _client.TrackEvent(eventName, eventProperties, eventMeasurements);
                _client.Flush();
            }
            catch (Exception) { }
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
            Dictionary<string, string> eventProperties = new Dictionary<string, string>(_commonProperties);
            if (properties != null)
            {
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
            }
            return eventProperties;
        }
    }
}
