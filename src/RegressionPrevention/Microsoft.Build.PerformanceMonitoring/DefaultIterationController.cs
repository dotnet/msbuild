using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.PerformanceMonitoring
{
    public class DefaultIterationController : IterationController
    {
        private int finishedIterations;

        public int IterationCount { get; }

        public DefaultIterationController(int iterationCount)
        {
            IterationCount = iterationCount;
        }

        public override bool IsTestCompleted()
        {
            return finishedIterations >= IterationCount;
        }

        public override void IterationFinished(TestIterationResult resault)
        {
            finishedIterations++;
        }
    }
}
