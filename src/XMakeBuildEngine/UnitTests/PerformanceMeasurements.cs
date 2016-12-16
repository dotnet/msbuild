// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class PerformanceMeasurements
    {
        private readonly IEnumerable<MsBuildSolution> _testSolutions = new[]
        {
            new GitProject(@"https://github.com/dotnet/corefx.git", "9c0ec0195b1ffbf63e133826856d51b90849532f", ProjectType.csproj),
            //new GitProject(@"https://github.com/dotnet/coreclr.git", "81c42cecca5e1b0b802d4df980280750d2e1419e", ProjectType.csproj), // Remove=""
            new GitProject(@"https://github.com/dotnet/roslyn.git", "45c60edef9ddc10dd2e15c668ea4ab4cec22141b", ProjectType.csproj),
            new GitProject(@"https://github.com/dotnet/roslyn.git", "45c60edef9ddc10dd2e15c668ea4ab4cec22141b", ProjectType.vbproj),
            new GitProject(@"https://github.com/Microsoft/DirectX-Graphics-Samples.git", "bcf6117b73e03f13c5946a2f80f778575dc2c56c", ProjectType.vcxproj),
            new MsBuildSolution("Generated", @"D:\projects\Domino\Out\Bin\debug\TestProject", ProjectType.csproj)
        };

        [Fact]
        [Trait("category", "performance")]
        public void MeasureEvaluation()
        {
            var metrics = new List<IMetrics>();

            foreach (var solution in _testSolutions)
            {
                Console.WriteLine($"\n-----------{solution.Name} : {solution.ProjectType}-----------");
                solution.Materialize();

                var repoMetrics = new RepositoryMetrics(solution.Name, solution.ProjectType, solution.ProjectFiles.Count());

                var evaluationMetrics = MeasureEvaluationTimeWithRepeats(solution, 4);

                metrics.Add(
                    new CompositeMetrics()
                    .WithMetrics(repoMetrics)
                    .WithMetrics(evaluationMetrics));
            }

            var csvPath = Path.Combine(AssemblyDirectory(), $"{nameof(MeasureEvaluation)}.csv");
            CSV.FromMetrics(metrics).WriteToFile(csvPath);

            Console.WriteLine($"Wrote measurements in {csvPath}");
        }

        private IMetrics MeasureEvaluationTimeWithRepeats(MsBuildSolution solution, int repeats)
        {
            Console.WriteLine($"Measuring evaluation time for {solution.Name}");

            SolutionEvaluationMetrics templateMetrics = null;

            var projectPaths = solution.ProjectFiles.ToArray();
            var averageProjectLoadingTimes = new List<TimeSpan>(repeats);
            var totalProjectLoadingTimes = new List<TimeSpan>(repeats);

            Debug.Assert(projectPaths.Any(), $"{solution.Name} should have more than zero {solution.ProjectType} files");

            for (var i = 0; i < repeats; i++)
            {
                var projectCollection = new ProjectCollection();
                var solutionEvaluationMetrics = LoadAllProjects(projectPaths, projectCollection);
                projectCollection.UnloadAllProjects();

                averageProjectLoadingTimes.Add(solutionEvaluationMetrics.EvaluationTimes.AverageEvaluationTime.Value);
                totalProjectLoadingTimes.Add(solutionEvaluationMetrics.EvaluationTimes.TotalEvaluationTime.Value);

                if (i == 0)
                {
                    templateMetrics = solutionEvaluationMetrics;
                }
            }

            Debug.Assert(templateMetrics != null);

            templateMetrics.EvaluationTimes.AverageEvaluationTime.Value = TimespanAverage(averageProjectLoadingTimes);
            templateMetrics.EvaluationTimes.TotalEvaluationTime.Value = TimespanAverage(totalProjectLoadingTimes);

            return templateMetrics;
        }

        private SolutionEvaluationMetrics LoadAllProjects(IList<string> projectPaths, ProjectCollection projectCollection)
        {
            var structureMetrics = new ProjectStructureMetrics();
            var projectsLoadTimes = new List<TimeSpan>();

            var succsesfulEvaluations = 0;

            foreach (var projecPath in projectPaths)
            {
                try
                {
                    var watch = Stopwatch.StartNew();

                    var project = new Project(
                        projecPath,
                        new Dictionary<string, string>(),
                        MSBuildConstants.CurrentToolsVersion, projectCollection,
                        ProjectLoadSettings.IgnoreMissingImports);

                    watch.Stop();

                    succsesfulEvaluations++;

                    projectsLoadTimes.Add(watch.Elapsed);

                    structureMetrics.Properties.Value += project.Properties.Count;
                    structureMetrics.Items.Value += project.Items.Count;
                    structureMetrics.ItemsWithGlobs.Value +=
                        project.Items
                            .Select(i => i.Xml)
                            .Distinct()
                            .Count(
                                pie =>
                                    !string.IsNullOrEmpty(pie.Include) && pie.Include.IndexOfAny(new[] {'?', '*'}) != -1);
                    structureMetrics.Imports.Value += project.Imports.Count;
                    structureMetrics.Targets.Value += project.Targets.Count;
                    structureMetrics.ItemTypes.Value += project.ItemTypes.Count;
                    structureMetrics.ItemDefinitions.Value += project.ItemDefinitions.Count;
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine($"\tException thrown while evaluating project {projecPath}");
                    Console.WriteLine(e.Message);
                }
            }

            var solutionEvaluationTimes = new SolutionEvaluationTimes(TimespanAverage(projectsLoadTimes), TimespanTotal(projectsLoadTimes));

            return new SolutionEvaluationMetrics(structureMetrics, solutionEvaluationTimes, succsesfulEvaluations);
        }

        private static TimeSpan TimespanAverage(List<TimeSpan> timespans)
        {
            Debug.Assert(timespans.Any());

            return new TimeSpan(TimespanTotal(timespans).Ticks / timespans.Count);
        }

        private static TimeSpan TimespanTotal(IEnumerable<TimeSpan> timespans)
        {
            return timespans.Aggregate((t1, t2) => t1.Add(t2));
        }

        private static string AssemblyDirectory()
        {
            var currentAssembly = AssemblyUtilities.GetAssemblyLocation(typeof(PerformanceMeasurements).GetTypeInfo().Assembly);
            return Path.GetDirectoryName(currentAssembly);
        }

        internal enum ProjectType
        {
            csproj,
            vbproj,
            vcxproj
        }

        private class CSV
        {
            private ICollection<string[]> _lines;
            private ICollection<string> _header;

            public void WithHeader(ICollection<string> header)
            {
                if (_header != null)
                {
                    Debug.Assert(header.Count() == _header.Count(), "Header should have the same number of elements as the lines have");
                }

                if (_lines != null && _lines.Any())
                {
                    Debug.Assert(_lines.Last().Length == _header.Count);
                }

                _header = header;
            }

            public void WithLine(string[] line)
            {
                if (_header != null)
                {
                    Debug.Assert(line.Length == _header.Count);
                }

                if (_lines == null)
                {
                    _lines = new List<string[]>();
                }
                else if (_lines.Any())
                {
                    Debug.Assert(_lines.Last().Length == line.Length, "Can't add lines of different lenghts");
                }

                _lines.Add(line);
            }

            public void WriteToFile(string filePath)
            {
                var sb = new StringBuilder();

                sb.AppendLine(string.Join(",", _header));

                foreach (var line in _lines)
                {
                    sb.AppendLine(string.Join(",", line));
                }

                File.WriteAllText(filePath, sb.ToString());
            }

            public static CSV FromMetrics(List<IMetrics> metrics)
            {
                Debug.Assert(metrics.Any());

                var csv = new CSV();
                csv.WithHeader(metrics.First().GetMetrics().Select(m => m.GetName()).ToArray());

                foreach (var metricLine in metrics)
                {
                    csv.WithLine(metricLine.GetMetrics().Select(m => m.ToString()).ToArray());
                }

                return csv;
            }
        }

        internal interface IMsBuildSolution: IDisposable
        {
            string Name { get; }
            string RootDirectory { get; }
            IList<string> ProjectFiles { get; }
            void Materialize();
        }

        internal class MsBuildSolution : IMsBuildSolution
        {
            protected static readonly string TempMsBuildTestProjectsRoot = Path.Combine(System.IO.Path.GetTempPath(), "MSBuildTestProjects");

            protected bool Materialized;
            protected bool Disposed;

            public string Name { get; }

            public string RootDirectory { get; }

            public virtual IList<string> ProjectFiles => Directory.EnumerateFiles(
                RootDirectory,
                $"*.{ProjectType.ToString().ToLower()}",
                SearchOption.AllDirectories).ToList();

            public ProjectType ProjectType { get; }

            public MsBuildSolution(string name, string rootDirectory, ProjectType projectType)
            {
                Name = name;
                RootDirectory = rootDirectory;
                ProjectType = projectType;
            }

            public virtual void Materialize()
            {
                Console.WriteLine($"Using solution from {RootDirectory}");
            }

            public void Dispose()
            {
                if (Disposed)
                {
                    return;
                }

                if (Directory.Exists(RootDirectory))
                {
                    FileUtilities.DeleteDirectoryNoThrow(RootDirectory, true);
                }

                Disposed = true;
            }
        }

        internal interface IMetric
        {
            string GetName();
            object GetValue();
        }

        private class GitProject : MsBuildSolution
        {
            private readonly string _commit;

            private readonly string _repoAddress;
            private readonly string _repoRoot;

            public GitProject(string repoAddress, string commit, ProjectType projectType, string repoRoot)
                : base(GetRepoName(repoAddress), System.IO.Path.Combine(repoRoot, GetRepoName(repoAddress)), projectType)
            {
                _repoAddress = repoAddress;
                _commit = commit;
                _repoRoot = repoRoot;
            }

            public GitProject(string repoAddress, string commit, ProjectType projectType) :
                this(
                repoAddress,
                commit,
                projectType,
                TempMsBuildTestProjectsRoot)
            {
            }

            private static string GetRepoName(string repoAddress)
            {
                var indexOfGit = repoAddress.LastIndexOf(".git", StringComparison.Ordinal);
                var lastSlash = repoAddress.LastIndexOf("/", StringComparison.Ordinal);

                return indexOfGit >= 0
                    ? repoAddress.Substring(lastSlash + 1, indexOfGit - lastSlash - 1)
                    : repoAddress.Substring(lastSlash + 1);
            }

            public override void Materialize()
            {
                CloneIfNecessary();
                CheckoutCommit();
            }

            private void CloneIfNecessary()
            {
                if (Directory.Exists(System.IO.Path.Combine(RootDirectory, ".git")))
                {
                    Console.WriteLine($"Reused project from {RootDirectory}");

                    // fetch in case the requested commit is newer than what the current repository has
                    Fetch();
                    return;
                }

                Clone();
            }

            private void Fetch()
            {
                ShellExec("git.exe", $"fetch", RootDirectory);
            }

            private void CheckoutCommit()
            {
                ShellExec("git.exe", $"checkout {_commit}", RootDirectory);
            }

            private void Clone()
            {
                FileUtilities.DeleteDirectoryNoThrow(RootDirectory, true);
                ShellExec("git.exe", $"clone {_repoAddress} {RootDirectory}", _repoRoot);
            }

            private static void ShellExec(string filename, string args, string workingDirectory)
            {
                Console.WriteLine($"{workingDirectory}: {filename} {args}");

                var process = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = true,
                        FileName = filename,
                        Arguments = args,
                        WorkingDirectory = workingDirectory,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();

                Assert.Equal(0, process.ExitCode);
            }
        }

        internal class Metric<T> : IMetric
        {
            public T Value { get; set; }
            private readonly string _metricName;

            public Metric(string name)
            {
                _metricName = name;
            }

            public string GetName() => _metricName;

            public object GetValue() => Value;

            public override string ToString()
            {
                return Value.ToString();
            }
        }

        internal class TimeSpanMetric : Metric<TimeSpan>
        {
            private readonly TimeUnit _timeUnit;

            public TimeSpanMetric(string name, TimeUnit timeUnit) : base(name)
            {
                _timeUnit = timeUnit;
            }

            public override string ToString()
            {
                switch (_timeUnit)
                {
                    case TimeUnit.ms:
                        return $"{Value.TotalMilliseconds} ms";
                    case TimeUnit.s:
                        return $"{Value.TotalSeconds} s";
                    case TimeUnit.m:
                        return $"{Value.TotalMinutes} m";
                    case TimeUnit.h:
                        return $"{Value.TotalHours} h";

                    default:
                        throw new NotImplementedException();
                }
            }

            public enum TimeUnit
            {
                ms, s, m, h
            }
        }

        private interface IMetrics
        {
            IEnumerable<IMetric> GetMetrics();
        }

        private class CompositeMetrics : IMetrics
        {
            private IList<IMetrics> _metrics;

            public IEnumerable<IMetric> GetMetrics()
            {
                var metrics = _metrics
                    .Select(m => m.GetMetrics())
                    .Aggregate((m1, m2) => m1.Concat(m2));

                return metrics;
            }

            public CompositeMetrics WithMetrics(IMetrics metrics)
            {
                if (_metrics == null)
                {
                    _metrics = new List<IMetrics>();
                }

                _metrics.Add(metrics);

                return this;
            }
        }

        private class SolutionEvaluationTimes : IMetrics
        {
            public TimeSpanMetric AverageEvaluationTime { get; } = new TimeSpanMetric(nameof(AverageEvaluationTime), TimeSpanMetric.TimeUnit.ms);

            public TimeSpanMetric TotalEvaluationTime { get; } = new TimeSpanMetric(nameof(TotalEvaluationTime), TimeSpanMetric.TimeUnit.ms);

            public SolutionEvaluationTimes(TimeSpan average, TimeSpan total)
            {
                AverageEvaluationTime.Value = average;
                TotalEvaluationTime.Value = total;
            }

            public IEnumerable<IMetric> GetMetrics()
            {
                yield return AverageEvaluationTime;
                yield return TotalEvaluationTime;
            }
        }

        private class ProjectStructureMetrics : IMetrics
        {
            public Metric<int> Imports { get; } = new Metric<int>(nameof(Imports));
            public Metric<int> Targets { get; } = new Metric<int>(nameof(Targets));
            public Metric<int> Properties { get; } = new Metric<int>(nameof(Properties));
            public Metric<int> ItemDefinitions { get; } = new Metric<int>(nameof(ItemDefinitions));
            public Metric<int> ItemTypes { get; } = new Metric<int>(nameof(ItemTypes));
            public Metric<int> Items { get; } = new Metric<int>(nameof(Items));
            public Metric<int> ItemsWithGlobs { get; } = new Metric<int>(nameof(ItemsWithGlobs));

            public IEnumerable<IMetric> GetMetrics()
            {
                yield return Imports;
                yield return Targets;
                yield return Properties;
                yield return ItemDefinitions;
                yield return ItemTypes;
                yield return Items;
                yield return ItemsWithGlobs;
            }
        }

        private class SolutionEvaluationMetrics : IMetrics
        {
            public Metric<int> SuccsessfulEvaluations { get; } = new Metric<int>(nameof(SuccsessfulEvaluations));
            public ProjectStructureMetrics Structure { get; }
            public SolutionEvaluationTimes EvaluationTimes { get; }

            public SolutionEvaluationMetrics(ProjectStructureMetrics structure, SolutionEvaluationTimes times, int succsessfullEvaluations)
            {
                Structure = structure;
                EvaluationTimes = times;
                SuccsessfulEvaluations.Value = succsessfullEvaluations;
            }

            public IEnumerable<IMetric> GetMetrics()
            {
                return 
                    new[] {SuccsessfulEvaluations}
                    .Concat(Structure.GetMetrics()
                    .Concat(EvaluationTimes.GetMetrics()));
            }
        }

        private class RepositoryMetrics : IMetrics
        {
            public Metric<string> Name { get; } = new Metric<string>(nameof(Name));

            public Metric<ProjectType> Type { get; } = new Metric<ProjectType>(nameof(Type));
            public Metric<int> ProjectFileCount { get; } = new Metric<int>(nameof(ProjectFileCount));

            public RepositoryMetrics(string repoName, ProjectType projectType, int projectCount)
            {
                Name.Value = repoName;
                Type.Value = projectType;
                ProjectFileCount.Value = projectCount;
            }

            public IEnumerable<IMetric> GetMetrics()
            {
                yield return Name;
                yield return Type;
                yield return ProjectFileCount;
            }
        }
    }
}