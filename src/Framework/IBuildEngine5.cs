using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends IBuildEngine to log telemetry.
    /// </summary>
    public interface IBuildEngine5 : IBuildEngine4
    {
        /// <summary>
        /// Logs telemetry.
        /// </summary>
        /// <param name="eventName">The event name.</param>
        /// <param name="properties">The event properties.</param>
        void LogTelemetry(string eventName, IDictionary<string, string> properties);
    }
}