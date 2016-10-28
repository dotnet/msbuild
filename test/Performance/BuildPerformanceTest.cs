// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Xunit.Performance;
using Microsoft.DotNet.TestFramework;
using System.IO;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class BuildPerformanceTest : TestBase
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

        public void BuildSingleProject(TestAssetInstance instance)
        {
            foreach (var iteration in Benchmark.Iterations)
            {
                var buildCommand = new BuildCommand()
                    .WithProjectDirectory(instance.Root);

                using (iteration.StartMeasurement())
                {
                    buildCommand.Execute()
                                .Should().Pass();
                }

                TouchSource(instance.Root);
            }
        }

        [Benchmark]
        public void IncrementalSkipSingleProject_SingleTargetApp() => IncrementalSkipSingleProject(CreateTestInstance(SingleTargetApp));

        [Benchmark]
        public void IncrementalSkipSingleProject_TwoTargetApp() => IncrementalSkipSingleProject(CreateTestInstance(TwoTargetApp));

        public void IncrementalSkipSingleProject(TestAssetInstance instance)
        {
            new BuildCommand()
                   .WithProjectDirectory(instance.Root)
                   .Execute()
                   .Should().Pass();

            foreach (var iteration in Benchmark.Iterations)
            {
                var buildCommand = new BuildCommand()
                                          .WithProjectDirectory(instance.Root);

                using (iteration.StartMeasurement())
                {
                    buildCommand
                        .Execute()
                        .Should().Pass();
                }
            }
        }

        [Benchmark]
        public void BuildAllInGraph_SingleTargetGraph() => BuildAllInGraph(CreateTestInstances(SingleTargetGraph));

        [Benchmark]
        public void BuildAllInGraph_TwoTargetGraph() => BuildAllInGraph(CreateTestInstances(TwoTargetGraph));

        [Benchmark]
        public void BuildAllInGraph_TwoTargetGraphLarge() => BuildAllInGraph(CreateTestInstances(TwoTargetGraphLarge));

        public void BuildAllInGraph(TestAssetInstance[] instances)
        {
            var instance = instances[0];

            foreach (var iteration in Benchmark.Iterations)
            {
                var buildCommand = new BuildCommand()
                                          .WithProjectDirectory(instance.Root);

                using (iteration.StartMeasurement())
                {
                    buildCommand
                        .Execute()
                        .Should().Pass();
                }

                foreach (var i in instances)
                {
                    TouchSource(i.Root);
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

        public void IncrementalSkipAllInGraph(TestAssetInstance[] instances)
        {
            var instance = instances[0];

            new BuildCommand()
                   .WithProjectDirectory(instance.Root)
                   .Execute()
                   .Should().Pass();

            foreach (var iteration in Benchmark.Iterations)
            {
                var buildCommand = new BuildCommand()
                                          .WithProjectDirectory(instance.Root);

                using (iteration.StartMeasurement())
                {
                    buildCommand
                        .Execute()
                        .Should().Pass();
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

        public void IncrementalRebuildWithRootChangedInGraph(TestAssetInstance[] instances)
        {
            var instance = instances[0];
            new BuildCommand()
                   .WithProjectDirectory(instance.Root)
                   .Execute()
                   .Should().Pass();

            foreach (var iteration in Benchmark.Iterations)
            {
                var buildCommand = new BuildCommand()
                                          .WithProjectDirectory(instance.Root);
                                          
                using (iteration.StartMeasurement())
                {
                    buildCommand
                        .Execute()
                        .Should().Pass();
                }

                TouchSource(instance.Root);
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

        public void IncrementalRebuildWithLastChangedInGraph(TestAssetInstance[] instances)
        {
            var instance = instances[0];

            new BuildCommand()
                   .WithProjectDirectory(instance.Root)
                   .Execute()
                   .Should().Pass();

            foreach (var iteration in Benchmark.Iterations)
            {
                var buildCommand = new BuildCommand()
                                          .WithProjectDirectory(instance.Root);

                using (iteration.StartMeasurement())
                {
                    buildCommand
                        .Execute()
                        .Should().Pass();
                }

                TouchSource(instances.Last().Root);
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

        public void IncrementalSkipAllNoDependenciesInGraph(TestAssetInstance[] instances)
        {
            var instance = instances[0];

            new BuildCommand()
                   .WithProjectDirectory(instance.Root)
                   .Execute()
                   .Should().Pass();

            foreach (var iteration in Benchmark.Iterations)
            {
                var commands = new List<BuildCommand>();

                foreach (var i in instances.Reverse())
                {
                    var buildCommand = new BuildCommand()
                                              .WithProjectDirectory(instance.Root)
                                              .WithFramework(NuGet.Frameworks.FrameworkConstants.CommonFrameworks.NetCoreApp10)
                                              .WithNoDependencies();

                    commands.Add(buildCommand);
                }

                using (iteration.StartMeasurement())
                {
                    foreach (var buildCommand in commands)
                    {
                        buildCommand
                            .Execute()
                            .Should().Pass();
                    }
                }
            }
        }
        [Benchmark]
        public void BuildAllNoDependenciesInGraph_SingleTargetGraph() =>
          BuildAllNoDependenciesInGraph(CreateTestInstances(SingleTargetGraph));

        [Benchmark]
        public void BuildAllNoDependenciesInGraph_TwoTargetGraph() =>
            BuildAllNoDependenciesInGraph(CreateTestInstances(TwoTargetGraph));

        [Benchmark]
        public void BuildAllNoDependenciesInGraph_TwoTargetGraphLarge() =>
            BuildAllNoDependenciesInGraph(CreateTestInstances(TwoTargetGraphLarge));

        public void BuildAllNoDependenciesInGraph(TestAssetInstance[] instances)
        {
            foreach (var iteration in Benchmark.Iterations)
            {
                var commands = new List<BuildCommand>();

                foreach (var i in instances.Reverse())
                {
                    var buildCommand = new BuildCommand()
                                              .WithProjectDirectory(i.Root)
                                              .WithFramework(NuGet.Frameworks.FrameworkConstants.CommonFrameworks.NetCoreApp10)
                                              .WithNoDependencies();
                    
                    commands.Add(buildCommand);
                }

                using (iteration.StartMeasurement())
                {
                    foreach (var buildCommand in commands)
                    {
                        buildCommand.Execute().Should().Pass();
                    }
                }

                foreach (var instance in instances)
                {
                    TouchSource(instance.Root);
                }
            }
        }

        protected void TouchSource(DirectoryInfo projectDir)
        {
            var sourceFile = projectDir.GetFiles("*.cs", SearchOption.AllDirectories).FirstOrDefault();

            if (sourceFile == null)
            {
                throw new InvalidOperationException($"'.cs' files not found in {projectDir.FullName}");
            }

            sourceFile.LastWriteTime = DateTime.Now;
        }

        protected TestAssetInstance[] CreateTestInstances(string[] testProjectNames, [CallerMemberName] string callingMethod = "")
        {
            return testProjectNames.Select(testProjectName =>
            {
                return CreateTestInstance(testProjectName, callingMethod);
            }).ToArray();
        }

        protected TestAssetInstance CreateTestInstance(string testProjectName, [CallerMemberName] string callingMethod = "")
        {
            return TestAssets.Get(Path.Combine("PerformanceTestProjects", testProjectName))
                             .CreateInstance(callingMethod)
                             .WithSourceFiles()
                             .WithRestoreFiles();
        }
    }
}
