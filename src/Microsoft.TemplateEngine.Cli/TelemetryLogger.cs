using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli
{
    public class TelemetryLogger : ITelemetryLogger
    {
        private readonly Action<string, IDictionary<string, string>, IDictionary<string, double>> _trackEvent;

        public TelemetryLogger(Action<string, IDictionary<string, string>, IDictionary<string, double>> trackEvent)
        {
            _trackEvent = trackEvent;
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> measurements = null)
        {
            try
            {
                _trackEvent?.Invoke(eventName, properties, measurements);
            }
            catch
            {
            }
        }
    }
}
