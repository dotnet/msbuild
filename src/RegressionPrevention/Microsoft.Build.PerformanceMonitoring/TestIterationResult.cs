using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.PerformanceMonitoring
{
    public readonly struct TestIterationResult
    {
        public ImmutableDictionary<string, long> Metrics { get; }

        public TestIterationResult(ImmutableDictionary<string, long> metrics)
        {
            Metrics = metrics;
        }
    }
}
