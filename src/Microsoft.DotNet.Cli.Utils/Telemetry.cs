using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Telemetry
    {
        private static bool _isInitialized = false;
        private static TelemetryClient _client = null;

        private static Dictionary<string, string> _commonProperties = null;
        private static Dictionary<string, double> _commonMeasurements = null;

        //readonly instead of const to to avoid inlining in case we need to change the instrumentation key
        private static readonly string InstrumentationKey = "74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";

        private const string TelemetryOptout = "DOTNET_CLI_TELEMETRY_OPTOUT";
        private const string OSVersion = "OS Version";
        private const string OSPlatform = "OS Platform";
        private const string RuntimeId = "Runtime Id";
        private const string ProductVersion = "Product Version";

        public Telemetry()
        {
            if (_isInitialized)
                return;

            bool optout = Env.GetEnvironmentVariableAsBool(TelemetryOptout);

            if (optout)
                return;

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
            catch (Exception) { }
        }

        public void TrackCommand(string command, IDictionary<string, string> properties = null, IDictionary<string, double> measurements = null)
        {
            if (!_isInitialized)
                return;

            Dictionary<string, double> eventMeasurements = GetEventMeasures(measurements);
            Dictionary<string, string> eventProperties = GetEventProperties(properties);

            try
            {
                _client.TrackEvent(command, eventProperties, eventMeasurements);
                _client.Flush();
            }
            catch (Exception) { }
        }

        private Dictionary<string, double> GetEventMeasures(IDictionary<string, double> measurements)
        {
            Dictionary<string, double> eventMeasurements = new Dictionary<string, double>(_commonMeasurements);
            if (measurements != null)
            {
                foreach (var m in measurements)
                {
                    if (eventMeasurements.ContainsKey(m.Key))
                        eventMeasurements[m.Key] = m.Value;
                    else
                        eventMeasurements.Add(m.Key, m.Value);
                }
            }
            return eventMeasurements;
        }

        private Dictionary<string, string> GetEventProperties(IDictionary<string, string> properties)
        {
            Dictionary<string, string> eventProperties = new Dictionary<string, string>(_commonProperties);
            if (properties != null)
            {
                foreach (var p in properties)
                {
                    if (eventProperties.ContainsKey(p.Key))
                        eventProperties[p.Key] = p.Value;
                    else
                        eventProperties.Add(p.Key, p.Value);
                }
            }
            return eventProperties;
        }
    }
}
