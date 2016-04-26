// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Xunit.Performance;
using Microsoft.DotNet.TestFramework;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class BuildPerformanceTest : PerformanceTestBase
    {
        private static string SingleTargetApp = "SingleTargetApp";
        private static string TwoTargetApp = "TwoTargetApp";

        private static string[] SingleTargetGraph = new[]
        {
            "SingleTargetGraph/SingleTargetP0",
            "SingleTargetGraph/SingleTargetP1",
            "SingleTargetGraph/SingleTargetP2"
        };

        private static string[] TwoTargetGraph = new[]
        {
            "TwoTargetGraph/TwoTargetP0",
            "TwoTargetGraph/TwoTargetP1",
            "TwoTargetGraph/TwoTargetP2"
        };

        private static string[] TwoTargetGraphLarge = new[]
        {
            "TwoTargetGraphLarge/TwoTargetLargeP0",
            "TwoTargetGraphLarge/TwoTargetLargeP1",
            "TwoTargetGraphLarge/TwoTargetLargeP2",
            "TwoTargetGraphLarge/TwoTargetLargeP3",
            "TwoTargetGraphLarge/TwoTargetLargeP4",
            "TwoTargetGraphLarge/TwoTargetLargeP5",
            "TwoTargetGraphLarge/TwoTargetLargeP6"
        };

        [Benchmark]
        public void BuildSingleProject_SingleTargetApp() => BuildSingleProject(CreateTestInstance(SingleTargetApp));
        [Benchmark]
        public void BuildSingleProject_TwoTargetApp() => BuildSingleProject(CreateTestInstance(TwoTargetApp));

        public void BuildSingleProject(TestInstance instance)
        {
            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    Build(instance.TestRoot);
                }
                RemoveBin(instance.TestRoot);
            }
        }

        [Benchmark]
        public void IncrementalSkipSingleProject_SingleTargetApp() => IncrementalSkipSingleProject(CreateTestInstance(SingleTargetApp));
        [Benchmark]
        public void IncrementalSkipSingleProject_TwoTargetApp() => IncrementalSkipSingleProject(CreateTestInstance(TwoTargetApp));

        public void IncrementalSkipSingleProject(TestInstance instance)
        {
            Build(instance.TestRoot);

            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    Build(instance.TestRoot);
                }
            }
        }

        [Benchmark]
        public void BuildAllInGraph_SingleTargetGraph() => BuildAllInGraph(CreateTestInstances(SingleTargetGraph));
        [Benchmark]
        public void BuildAllInGraph_TwoTargetGraph() => BuildAllInGraph(CreateTestInstances(TwoTargetGraph));
        [Benchmark]
        public void BuildAllInGraph_TwoTargetGraphLarge() => BuildAllInGraph(CreateTestInstances(TwoTargetGraphLarge));

        public void BuildAllInGraph(TestInstance[] instances)
        {
            var instance = instances[0];

            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    Build(instance.TestRoot);
                }
                foreach (var i in instances)
                {
                    RemoveBin(i.TestRoot);
                }
            }
        }

        [Benchmark]
        public void IncrementalSkipAllInGraph_SingleTargetGraph() =>
            IncrementalSkipAllInGraph(CreateTestInstances(SingleTargetGraph));

        [Benchmark]
        public void IncrementalSkipAllInGraph_TwoTargetGraph() =>
            IncrementalSkipAllInGraph(CreateTestInstances(TwoTargetGraph));

        [Benchmark]
        public void IncrementalSkipAllInGraphh_TwoTargetGraphLarge() =>
            IncrementalSkipAllInGraph(CreateTestInstances(TwoTargetGraphLarge));

        public void IncrementalSkipAllInGraph(TestInstance[] instances)
        {
            var instance = instances[0];

            Build(instance.TestRoot);

            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    Build(instance.TestRoot);
                }
            }
        }

        [Benchmark]
        public void IncrementalRebuildWithRootChangedInGraph_SingleTargetGraph() =>
            IncrementalRebuildWithRootChangedInGraph(CreateTestInstances(SingleTargetGraph));

        [Benchmark]
        public void IncrementalRebuildWithRootChangedInGraph_TwoTargetGraph() =>
            IncrementalRebuildWithRootChangedInGraph(CreateTestInstances(TwoTargetGraph));

        [Benchmark]
        public void IncrementalRebuildWithRootChangedInGraph_TwoTargetGraphLarge() =>
            IncrementalRebuildWithRootChangedInGraph(CreateTestInstances(TwoTargetGraphLarge));

        public void IncrementalRebuildWithRootChangedInGraph(TestInstance[] instances)
        {
            var instance = instances[0];

            Build(instance.TestRoot);

            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    Build(instance.TestRoot);
                }
                RemoveBin(instance.TestRoot);
            }
        }

        [Benchmark]
        public void IncrementalRebuildWithLastChangedInGraph_SingleTargetGraph() =>
            IncrementalRebuildWithLastChangedInGraph(CreateTestInstances(SingleTargetGraph));

        [Benchmark]
        public void IncrementalRebuildWithLastChangedInGraph_TwoTargetGraph() =>
            IncrementalRebuildWithLastChangedInGraph(CreateTestInstances(TwoTargetGraph));

        [Benchmark]
        public void IncrementalRebuildWithLastChangedInGraph_TwoTargetGraphLarge() =>
            IncrementalRebuildWithLastChangedInGraph(CreateTestInstances(TwoTargetGraphLarge));

        public void IncrementalRebuildWithLastChangedInGraph(TestInstance[] instances)
        {
            var instance = instances[0];

            Build(instance.TestRoot);

            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    Build(instance.TestRoot);
                }
                RemoveBin(instances.Last().TestRoot);
            }
        }


        [Benchmark]
        public void IncrementalSkipAllNoDependenciesInGraph_SingleTargetGraph() =>
           IncrementalSkipAllNoDependenciesInGraph(CreateTestInstances(SingleTargetGraph));

        [Benchmark]
        public void IncrementalSkipAllNoDependenciesInGraph_TwoTargetGraph() =>
            IncrementalSkipAllNoDependenciesInGraph(CreateTestInstances(TwoTargetGraph));

        [Benchmark]
        public void IncrementalSkipAllNoDependenciesInGraph_TwoTargetGraphLarge() =>
            IncrementalSkipAllNoDependenciesInGraph(CreateTestInstances(TwoTargetGraphLarge));

        public void IncrementalSkipAllNoDependenciesInGraph(TestInstance[] instances)
        {
            var instance = instances[0];

            Build(instance.TestRoot);

            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    foreach (var i in instances)
                    {
                        Run(new BuildCommand(i.TestRoot,
                            framework: DefaultFramework,
                            noDependencies: true,
                            buildProfile: false));
                    }
                }
            }
        }
        [Benchmark]
        public void BuildAllNoDependenciesInGraphh_SingleTargetGraph() =>
          BuildAllNoDependenciesInGraph(CreateTestInstances(SingleTargetGraph));

        [Benchmark]
        public void BuildAllNoDependenciesInGraph_TwoTargetGraph() =>
            BuildAllNoDependenciesInGraph(CreateTestInstances(TwoTargetGraph));

        [Benchmark]
        public void BuildAllNoDependenciesInGraph_TwoTargetGraphLarge() =>
            BuildAllNoDependenciesInGraph(CreateTestInstances(TwoTargetGraphLarge));

        public void BuildAllNoDependenciesInGraph(TestInstance[] instances)
        {
            foreach (var iteration in Benchmark.Iterations)
            {
                using (iteration.StartMeasurement())
                {
                    foreach (var i in instances.Reverse())
                    {
                        Run(new BuildCommand(i.TestRoot,
                            framework: DefaultFramework,
                            noDependencies: true,
                            buildProfile: false));
                    }
                }
                foreach (var instance in instances)
                {
                    RemoveBin(instance.TestRoot);
                }
            }
        }
    }
}
