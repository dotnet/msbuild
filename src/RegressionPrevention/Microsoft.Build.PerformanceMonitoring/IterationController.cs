using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.PerformanceMonitoring
{
    public abstract class IterationController
    {
        public abstract bool IsTestCompleted();

        public abstract void IterationFinished(TestIterationResult result);
    }
}
