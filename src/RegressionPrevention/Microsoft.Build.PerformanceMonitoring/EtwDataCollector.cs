using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.PerformanceMonitoring
{
    public abstract class EtwDataCollector
    {
        public string EventSourceName { get; }

        public abstract long Value { get; }

        public EtwDataCollector(string eventSourceName) 
        {
            EventSourceName = eventSourceName;
        }

        public abstract void EventLogged(TraceEvent traceRvent);
    }
}
