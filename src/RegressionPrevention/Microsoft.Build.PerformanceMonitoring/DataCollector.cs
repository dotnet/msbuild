using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.PerformanceMonitoring
{
    public abstract class DataCollector
    {
        public abstract long Value { get; }

        public abstract void Start();

        public abstract void Stop();
    }
}
