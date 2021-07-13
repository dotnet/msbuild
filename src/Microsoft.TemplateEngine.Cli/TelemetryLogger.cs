// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli
{
    public class TelemetryLogger : ITelemetryLogger
    {
        private readonly Action<string, IDictionary<string, string>, IDictionary<string, double>> _trackEvent;
        private readonly bool _writeToConsole;

        public TelemetryLogger(Action<string, IDictionary<string, string>, IDictionary<string, double>> trackEvent, bool writeToConsole = false)
        {
            _trackEvent = trackEvent;
            _writeToConsole = writeToConsole;
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> measurements = null)
        {
            try
            {
                _trackEvent?.Invoke(eventName, properties, measurements);

                if (_writeToConsole)
                {
                    Console.WriteLine($"Telemetry event {eventName}");

                    if (properties != null)
                    {
                        Console.WriteLine("Properties:");
                        foreach (KeyValuePair<string, string> property in properties)
                        {
                            Console.WriteLine($"\t{property.Key} = {property.Value}");
                        }
                    }

                    if (measurements != null)
                    {
                        Console.WriteLine("Measurements:");
                        foreach (KeyValuePair<string, double> measurement in measurements)
                        {
                            Console.WriteLine($"\t{measurement.Key} = {measurement.Value}");
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
