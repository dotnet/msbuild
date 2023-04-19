using Microsoft.Diagnostics.Tracing.Session;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Microsoft.Build.PerformanceMonitoring
{
    public class PerformanceTest
    {
        private readonly string useCaseName;
        private readonly string componentName;
        private readonly string testName;

        private readonly Dictionary<string, Func<DataCollector>> dataCollectorFactories = new();
        private readonly Dictionary<string, Func<EtwDataCollector>> etwDataCollectorFactories = new();

        public PerformanceTest(string useCaseName, string componentName, string testName)
        {
            this.useCaseName = useCaseName;
            this.componentName = componentName;
            this.testName = testName;
        }

        public PerformanceTest AddCollector<T>(string metricName) where T : DataCollector, new()
        {
            dataCollectorFactories[metricName] = () => new T();
            return this;
        }

        public PerformanceTest AddCollector(string metricName, Func<DataCollector> collectorFactory)
        {
            dataCollectorFactories[metricName] = collectorFactory;
            return this;
        }

        public PerformanceTest AddEtwCollector<T>(string metricName) where T : EtwDataCollector, new()
        {
            etwDataCollectorFactories[metricName] = () => new T();
            return this;
        }

        public PerformanceTest AddEtwCollector(string metricName, Func<EtwDataCollector> collectorFactory)
        {
            etwDataCollectorFactories[metricName] = collectorFactory;
            return this;
        }

        public async Task<PerformanceTestResult> RunProcessAsync(string fileName, string arguments, IterationController iterationController)
        {
            List<TestIterationResult> iterationResults = new List<TestIterationResult>();

            do
            {
                TestIterationResult iterationResult = await RunProcessCoreAsync(fileName, arguments);
                iterationResults.Add(iterationResult);

                iterationController.IterationFinished(iterationResult);
            }
            while (!iterationController.IsTestCompleted());

            PerformanceTestResult result = new PerformanceTestResult(useCaseName, componentName, testName, iterationResults.ToImmutableList());

            return result;
        }

        private async Task<TestIterationResult> RunProcessCoreAsync(string fileName, string arguments)
        {
            Dictionary<string, DataCollector> dataCollectors = new();
            Dictionary<string, EtwDataCollector> etwDataCollectors = new();

            StringBuilder stdOut = new StringBuilder();
            int processId = 0;

            using (TraceEventSession traceSession = new TraceEventSession("PerfTestSession"))
            {
                foreach (var kvp in etwDataCollectorFactories)
                {
                    EtwDataCollector etwCollector = kvp.Value();
                    etwDataCollectors[kvp.Key] = etwCollector;

                    traceSession.EnableProvider(etwCollector.EventSourceName);
                    traceSession.Source.Dynamic.All += e =>
                    {
                        if (e.ProcessID == processId)
                        {
                            etwCollector.EventLogged(e);
                        }
                    };
                }

                _ = Task.Run(() =>
                {
                    // Blocking call. Will end when session is disposed
                    traceSession.Source.Process();
                });

                foreach (var kvp in dataCollectorFactories)
                {
                    DataCollector collector = kvp.Value();
                    dataCollectors[kvp.Key] = collector;

                    collector.Start();
                }

                ProcessStartInfo processStartInfo = new ProcessStartInfo(fileName, arguments);
                processStartInfo.UseShellExecute = false;
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.CreateNoWindow = true;

                Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException();

                processId = process.Id;
                process.OutputDataReceived += (sender, e) => stdOut.AppendLine(e.Data);
                process.BeginOutputReadLine();

                await process.WaitForExitAsync();

                foreach (DataCollector collector in dataCollectors.Values)
                {
                    collector.Stop();
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("Build failed.");
                }
            }

            ImmutableDictionary<string, long> metrics = dataCollectors
                .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.Value))
                .Concat(etwDataCollectors
                    .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.Value)))
                .ToImmutableDictionary();

            TestIterationResult result = new TestIterationResult(metrics);

            return result;
        }
    }
}
