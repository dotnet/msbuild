// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Build.FileSystem;

namespace MSBuild.Benchmarks.Analysis;

/// <summary>
/// The kind of file system operation an evaluation performed.
/// </summary>
internal enum FileOperationKind
{
    FileExists,
    DirectoryExists,
    FileOrDirectoryExists,
    GetAttributes,
    GetLastWriteTimeUtc,
    EnumerateFiles,
    EnumerateDirectories,
    EnumerateFileSystemEntries,
    ReadFile,
    ReadFileAllText,
    ReadFileAllBytes,
    GetFileStream,
}

/// <summary>
/// Aggregated statistics for a single <see cref="FileOperationKind"/>.
/// </summary>
internal sealed class FileOperationStats
{
    private static readonly double TicksPerStopwatchTick = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

    private long _elapsedTicks;

    public long LogicalCalls;
    public long CacheHits;
    public long RealCalls;
    public long PositiveResults;

    public TimeSpan Elapsed => TimeSpan.FromTicks((long)(_elapsedTicks * TicksPerStopwatchTick));

    public void AddElapsed(long stopwatchTicks) => Interlocked.Add(ref _elapsedTicks, stopwatchTicks);
}

/// <summary>
/// An <see cref="MSBuildFileSystemBase"/> that reproduces the caching semantics of MSBuild's internal
/// <c>CachingFileSystemWrapper</c> while recording how much file system work an evaluation performs.
/// </summary>
/// <remarks>
/// <para>
/// The default <see cref="Microsoft.Build.Evaluation.Context.EvaluationContext"/> wraps the real file system in a
/// caching wrapper. Injecting a custom file system replaces that wrapper entirely, so this type re-implements the
/// same caching to keep the measured evaluation behaviorally identical to an uninstrumented one. The upside is that
/// it can report <em>logical</em> calls (what the evaluator asked for) separately from <em>real</em> calls (what
/// actually reached the operating system), which is what makes redundant probing visible.
/// </para>
/// <para>
/// <strong>This does not capture every read an evaluation performs.</strong> Project XML is read through
/// <c>XmlReaderExtension</c>, which opens a <see cref="FileStream"/> directly and never goes through
/// <see cref="MSBuildFileSystemBase"/>. Use kernel ETW file I/O tracing for a complete syscall-level picture.
/// </para>
/// </remarks>
internal sealed class CountingFileSystem : MSBuildFileSystemBase
{
    private readonly MSBuildFileSystemBase _inner;
    private readonly ConcurrentDictionary<string, bool> _directoryExistenceCache = new();
    private readonly ConcurrentDictionary<string, bool> _fileExistenceCache = new();
    private readonly ConcurrentDictionary<string, bool> _fileOrDirectoryExistenceCache = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastWriteTimeCache = new();
    private readonly ConcurrentDictionary<FileOperationKind, FileOperationStats> _stats = new();
    private readonly ConcurrentDictionary<string, int> _pathProbeCounts = new(StringComparer.OrdinalIgnoreCase);

    public CountingFileSystem(MSBuildFileSystemBase? inner = null) => _inner = inner ?? new PassthroughFileSystem();

    public IReadOnlyDictionary<FileOperationKind, FileOperationStats> Stats => _stats;

    /// <summary>
    /// How many times each distinct path was probed. A value greater than one means the evaluator asked about the
    /// same path repeatedly and only the cache prevented another syscall.
    /// </summary>
    public IReadOnlyDictionary<string, int> PathProbeCounts => _pathProbeCounts;

    public long TotalLogicalCalls => _stats.Values.Sum(s => s.LogicalCalls);

    public long TotalRealCalls => _stats.Values.Sum(s => s.RealCalls);

    public TimeSpan TotalElapsed => TimeSpan.FromTicks(_stats.Values.Sum(s => s.Elapsed.Ticks));

    private FileOperationStats StatsFor(FileOperationKind kind) => _stats.GetOrAdd(kind, static _ => new FileOperationStats());

    /// <summary>
    /// Runs <paramref name="operation"/>, recording it as a real (uncached) call of the given kind.
    /// </summary>
    private T Measure<T>(FileOperationKind kind, string path, Func<string, T> operation)
    {
        FileOperationStats stats = StatsFor(kind);
        long start = Stopwatch.GetTimestamp();
        T result = operation(path);
        stats.AddElapsed(Stopwatch.GetTimestamp() - start);

        Interlocked.Increment(ref stats.RealCalls);

        if (result is bool and true)
        {
            Interlocked.Increment(ref stats.PositiveResults);
        }

        return result;
    }

    private void RecordLogical(FileOperationKind kind, string path, bool wasCached)
    {
        FileOperationStats stats = StatsFor(kind);
        Interlocked.Increment(ref stats.LogicalCalls);

        if (wasCached)
        {
            Interlocked.Increment(ref stats.CacheHits);
        }

        _pathProbeCounts.AddOrUpdate(path, 1, static (_, count) => count + 1);
    }

    public override bool FileExists(string path)
    {
        bool cached = _fileExistenceCache.TryGetValue(path, out bool exists);
        RecordLogical(FileOperationKind.FileExists, path, cached);

        return cached
            ? exists
            : _fileExistenceCache.GetOrAdd(path, p => Measure(FileOperationKind.FileExists, p, _inner.FileExists));
    }

    public override bool DirectoryExists(string path)
    {
        bool cached = _directoryExistenceCache.TryGetValue(path, out bool exists);
        RecordLogical(FileOperationKind.DirectoryExists, path, cached);

        return cached
            ? exists
            : _directoryExistenceCache.GetOrAdd(path, p => Measure(FileOperationKind.DirectoryExists, p, _inner.DirectoryExists));
    }

    public override bool FileOrDirectoryExists(string path)
    {
        // Mirrors CachingFileSystemWrapper: a positive hit in either specific cache short-circuits the stat.
        if ((_fileExistenceCache.TryGetValue(path, out bool fileExists) && fileExists) ||
            (_directoryExistenceCache.TryGetValue(path, out bool directoryExists) && directoryExists))
        {
            RecordLogical(FileOperationKind.FileOrDirectoryExists, path, wasCached: true);
            return true;
        }

        bool cached = _fileOrDirectoryExistenceCache.ContainsKey(path);
        RecordLogical(FileOperationKind.FileOrDirectoryExists, path, cached);

        return _fileOrDirectoryExistenceCache.GetOrAdd(path, p => Measure(FileOperationKind.FileOrDirectoryExists, p, _inner.FileOrDirectoryExists));
    }

    public override DateTime GetLastWriteTimeUtc(string path)
    {
        bool cached = _lastWriteTimeCache.ContainsKey(path);
        RecordLogical(FileOperationKind.GetLastWriteTimeUtc, path, cached);

        return _lastWriteTimeCache.GetOrAdd(path, p => Measure(FileOperationKind.GetLastWriteTimeUtc, p, _inner.GetLastWriteTimeUtc));
    }

    public override FileAttributes GetAttributes(string path)
    {
        RecordLogical(FileOperationKind.GetAttributes, path, wasCached: false);
        return Measure(FileOperationKind.GetAttributes, path, _inner.GetAttributes);
    }

    public override IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => TimeEnumeration(FileOperationKind.EnumerateFiles, path, _inner.EnumerateFiles(path, searchPattern, searchOption));

    public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => TimeEnumeration(FileOperationKind.EnumerateDirectories, path, _inner.EnumerateDirectories(path, searchPattern, searchOption));

    public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => TimeEnumeration(FileOperationKind.EnumerateFileSystemEntries, path, _inner.EnumerateFileSystemEntries(path, searchPattern, searchOption));

    public override TextReader ReadFile(string path)
    {
        RecordLogical(FileOperationKind.ReadFile, path, wasCached: false);
        return Measure(FileOperationKind.ReadFile, path, _inner.ReadFile);
    }

    public override string ReadFileAllText(string path)
    {
        RecordLogical(FileOperationKind.ReadFileAllText, path, wasCached: false);
        return Measure(FileOperationKind.ReadFileAllText, path, _inner.ReadFileAllText);
    }

    public override byte[] ReadFileAllBytes(string path)
    {
        RecordLogical(FileOperationKind.ReadFileAllBytes, path, wasCached: false);
        return Measure(FileOperationKind.ReadFileAllBytes, path, _inner.ReadFileAllBytes);
    }

    public override Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
    {
        RecordLogical(FileOperationKind.GetFileStream, path, wasCached: false);
        return Measure(FileOperationKind.GetFileStream, path, p => _inner.GetFileStream(p, mode, access, share));
    }

    /// <summary>
    /// Directory enumeration is lazy, so the cost lands in <c>MoveNext</c> rather than in the call itself.
    /// This wrapper attributes the enumeration time to the originating operation.
    /// </summary>
    private IEnumerable<string> TimeEnumeration(FileOperationKind kind, string path, IEnumerable<string> source)
    {
        RecordLogical(kind, path, wasCached: false);
        FileOperationStats stats = StatsFor(kind);
        Interlocked.Increment(ref stats.RealCalls);

        return Enumerate();

        IEnumerable<string> Enumerate()
        {
            using IEnumerator<string> enumerator = source.GetEnumerator();

            while (true)
            {
                long start = Stopwatch.GetTimestamp();
                bool moved = enumerator.MoveNext();
                stats.AddElapsed(Stopwatch.GetTimestamp() - start);

                if (!moved)
                {
                    yield break;
                }

                Interlocked.Increment(ref stats.PositiveResults);
                yield return enumerator.Current;
            }
        }
    }

    /// <summary>
    /// The default <see cref="MSBuildFileSystemBase"/> implementation already forwards to the real file system.
    /// </summary>
    private sealed class PassthroughFileSystem : MSBuildFileSystemBase;
}
