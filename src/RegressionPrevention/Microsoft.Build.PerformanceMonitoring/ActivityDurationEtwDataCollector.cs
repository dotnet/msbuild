using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.PerformanceMonitoring
{
    public class ActivityDurationEtwDataCollector : EtwDataCollector
    {
        private readonly string startEventName;
        private readonly string stopEventName;
        private long value;
        private DateTime startEventTimestamp;

        public override long Value => value;

        public ActivityDurationEtwDataCollector(string eventSourceName, string activityName) 
            : base(eventSourceName)
        {
            startEventName = activityName + "/Start";
            stopEventName = activityName + "/Stop";
        }

        public override void EventLogged(TraceEvent traceEvent)
        {
            if (traceEvent.EventName == startEventName)
            {
                startEventTimestamp = traceEvent.TimeStamp;
            }
            else if (traceEvent.EventName == stopEventName)
            {
                if (startEventTimestamp == DateTime.MinValue)
                {
                    throw new InvalidOperationException("Stop event preceeds start event");
                }

                value = (long)(traceEvent.TimeStamp - startEventTimestamp).TotalMilliseconds;
            }

        }
    }
}
