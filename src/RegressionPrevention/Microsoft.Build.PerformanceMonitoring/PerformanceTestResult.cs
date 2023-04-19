using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.PerformanceMonitoring
{
    public class PerformanceTestResult
    {
        public string UseCaseName { get; }
        
        public string ComponentName { get; }

        public string TestName { get; }

        public ImmutableList<TestIterationResult> IterationResults { get; }

        internal PerformanceTestResult(string useCaseName, string componentName, string testName, ImmutableList<TestIterationResult> iterationResults)
        {
            UseCaseName = useCaseName;
            ComponentName = componentName;
            TestName = testName;
            IterationResults = iterationResults;
        }
    }
}
