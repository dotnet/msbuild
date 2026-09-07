// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// What evaluation found at a recorded path.
/// </summary>
internal enum PathKind
{
    Missing,
    File,
    Directory,
}

/// <summary>
/// What an existence probe asked about.
/// </summary>
internal enum ProbeKind
{
    File,
    Directory,
    FileOrDirectory,
}

/// <summary>
/// Why an evaluation result cannot be reused from a cache.
/// </summary>
internal enum NonCacheableReason
{
    None,
    VolatilePropertyFunction,
    UnclassifiedPropertyFunction,
    AllPropertyFunctionsEnabled,
    ItemTimestampMetadata,
    LazyWildcards,
    InMemoryProject,
    PartialEvaluation,
    RecorderFailure,
    ConflictingObservation,
    Link,
    HostFileSystem,
    ProcessWideCache,
}

/// <summary>
/// State of a path as evaluation observed it. A cache validates it by comparing against a fresh stat.
/// </summary>
internal readonly record struct FileDependency(PathKind Kind, DateTime LastWriteTimeUtc, long Length);

/// <summary>
/// Values known before evaluation starts. Two evaluations with different keys never share a cache entry.
/// <paramref name="GlobalProperties"/> lists the global properties sorted by name as <c>name=value</c> pairs, each ended by a
/// NUL character, so the key compares by value. <paramref name="ToolsetFingerprint"/> covers what a toolset adds beyond its
/// version and path, so two toolsets registered under one version do not collide.
/// </summary>
internal sealed record EvaluationInputKey(
    string ProjectFullPath,
    string GlobalProperties,
    string ToolsVersion,
    string ToolsPath,
    string? SubToolsetVersion,
    long ToolsetFingerprint,
    ProjectEvaluationStage Stage,
    ProjectLoadSettings LoadSettings,
    bool Interactive,
    int MaxNodeCount,
    string StartupDirectory,
    string WorkingDirectory,
    string Culture,
    string UICulture,
    string EngineVersion,
    string? DisabledChangeWave,
    long EnvironmentFingerprint,
    long ParserConfigurationFingerprint);

/// <summary>
/// The inputs one evaluation consumed, frozen when the evaluation completed.
/// </summary>
/// <param name="Key">Values that select a cache entry.</param>
/// <param name="Files">
/// Files and directories evaluation read, probed, or enumerated, keyed by full path.
/// Missing paths matter as much as existing ones: their appearance changes the result.
/// </param>
/// <param name="EnvironmentReads">Environment variables read through property functions; variables imported as properties are part of the key.</param>
/// <param name="NonCacheable">Why the result must never be reused, or <see cref="NonCacheableReason.None"/>.</param>
/// <param name="NonCacheableDetail">The input that made the evaluation non-cacheable, for diagnostics.</param>
internal sealed record EvaluationInputs(
    EvaluationInputKey Key,
    IReadOnlyDictionary<string, FileDependency> Files,
    IReadOnlyDictionary<string, string?> EnvironmentReads,
    NonCacheableReason NonCacheable,
    string? NonCacheableDetail)
{
    internal bool IsCacheable => NonCacheable == NonCacheableReason.None;
}
