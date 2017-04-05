using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Cli
{
    public interface ITelemetryLogger
    {
        void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> measurements = null);
    }
}
