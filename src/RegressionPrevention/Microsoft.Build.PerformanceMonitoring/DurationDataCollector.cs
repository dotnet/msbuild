using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.PerformanceMonitoring
{
    public class DurationDataCollector : DataCollector
    {
        private Stopwatch stopwatch = new Stopwatch();

        public override long Value => stopwatch.ElapsedMilliseconds;

        public override void Start()
        {
            stopwatch.Start();
        }

        public override void Stop()
        {
            stopwatch.Stop();
        }
    }
}
