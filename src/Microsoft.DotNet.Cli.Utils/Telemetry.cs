using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    public class Telemetry
    {
        private class Variables
        {
            public static readonly string InstrumentationKey = "74cc1c9e-3e6e-4d05-b3fc-dde9101d0254";
            private static readonly string Prefix = "DOTNET_CLI_TELEMETRY_";
            public static readonly string Optout = Prefix + "OPTOUT";
        }

        private class Properties
        {
            public static readonly string OSVersion = "OS Version";
            public static readonly string OSPlatform = "OS Platform";
            public static readonly string RuntimeId = "Runtime Id";
            public static readonly string ProductVersion = "Product Version";
        }

        private static bool _isInitialized = false;
        private static TelemetryClient _client = null;

        private static Dictionary<string, string> _commonProperties = null;
        private static Dictionary<string, double> _commonMeasurements = null;

        static Telemetry()
        {
            if (_isInitialized)
                return;

            bool Optout = Env.GetBool(Variables.Optout);

            if (Optout)
                return;

            try
            {
                _client = new TelemetryClient();
                _client.InstrumentationKey = Variables.InstrumentationKey;
                _client.Context.Session.Id = Guid.NewGuid().ToString();

                var runtimeEnvironment = PlatformServices.Default.Runtime;
                _client.Context.Device.OperatingSystem = runtimeEnvironment.OperatingSystem;

                _commonProperties = new Dictionary<string, string>();
                _commonProperties.Add(Properties.OSVersion, runtimeEnvironment.OperatingSystemVersion);
                _commonProperties.Add(Properties.OSPlatform, runtimeEnvironment.OperatingSystemPlatform.ToString());
                _commonProperties.Add(Properties.RuntimeId, runtimeEnvironment.GetRuntimeIdentifier());
                _commonProperties.Add(Properties.ProductVersion, Product.Version);
                _commonMeasurements = new Dictionary<string, double>();

                _isInitialized = true;
            }
            catch (Exception) { }
        }

        public static void TrackCommand(string command, IDictionary<string, string> properties = null, IDictionary<string, double> measurements = null)
        {
            if (!_isInitialized)
                return;

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

            try
            {
                _client.TrackEvent(command, eventProperties, eventMeasurements);
                _client.Flush();
            }
            catch (Exception) { }
        }
    }
}
