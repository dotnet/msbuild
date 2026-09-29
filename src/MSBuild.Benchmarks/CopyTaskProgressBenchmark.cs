// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures what task progress reporting costs the <c>Copy</c> task, the highest-volume adopter.
/// </summary>
/// <remarks>
/// The copy itself is replaced by a delegate that does no file system work, so the measurement
/// isolates the task's own per-file overhead. This is the worst case for progress reporting: with
/// real file I/O the same overhead is diluted by the cost of the copy, so any regression visible
/// here is an upper bound on the regression a real build can see. Both the single-threaded and the
/// parallel copy paths are measured because only the parallel path contends on the reporter lock.
/// </remarks>
[MemoryDiagnoser]
public class CopyTaskProgressBenchmark
{
    private const int FileCount = 1000;

    private ITaskItem[] _sourceFiles = null!;
    private ITaskItem _destinationFolder = null!;
    private IBuildEngine _engineWithoutProgress = null!;
    private IBuildEngine _engineWithProgress = null!;

    /// <summary>
    /// Whether the host supports progress reporting. <see langword="false"/> is the behavior of
    /// every host that predates the protocol and is the baseline the adoption must not regress.
    /// </summary>
    [Params(false, true)]
    public bool ProgressSupported { get; set; }

    /// <summary>
    /// Whether the parallel copy path is used, which reports from several threads at once.
    /// </summary>
    [Params(false, true)]
    public bool CopyInParallel { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string sourceRoot = Path.Combine(Path.GetTempPath(), "MSBuild.Benchmarks.CopyProgress");
        _sourceFiles = new ITaskItem[FileCount];
        for (int i = 0; i < FileCount; i++)
        {
            _sourceFiles[i] = new TaskItemStub(Path.Combine(sourceRoot, $"file{i}.txt"));
        }

        _destinationFolder = new TaskItemStub(Path.Combine(sourceRoot, "out"));
        _engineWithoutProgress = new BenchmarkBuildEngine(supportsProgress: false);
        _engineWithProgress = new BenchmarkBuildEngine(supportsProgress: true);
    }

    [Benchmark]
    public bool Copy_Files()
    {
        Copy copy = new()
        {
            BuildEngine = ProgressSupported ? _engineWithProgress : _engineWithoutProgress,
            SourceFiles = _sourceFiles,
            DestinationFolder = _destinationFolder,
            RetryDelayMilliseconds = 0,
        };

        return copy.Execute(static (_, _) => true, CopyInParallel);
    }
}
