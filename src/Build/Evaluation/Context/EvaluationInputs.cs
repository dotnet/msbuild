// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

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
    UnsupportedRegistryValue,
    ItemTimestampMetadata,
    LazyWildcards,
    InMemoryProject,
    PartialEvaluation,
    RecorderFailure,
    ConflictingObservation,
    Link,
    HostFileSystem,
    ProcessWideCache,
    RegistryRead,
    FailedSdkResolution,
    EvaluationDiagnostics,
}

/// <summary>
/// State of a path as evaluation observed it. A cache validates it by comparing against a fresh stat.
/// </summary>
internal readonly record struct FileDependency(PathKind Kind, DateTime LastWriteTimeUtc, long Length);

/// <summary>
/// An SDK resolution evaluation consumed, including the context required to repeat it.
/// </summary>
internal sealed record SdkDependency(
    SdkReference Reference,
    SdkResultSnapshot Result,
    SdkResolutionContext Context);

/// <summary>
/// Context required to repeat an SDK resolution before accepting a cached evaluation.
/// </summary>
internal sealed record SdkResolutionContext(
    ElementLocation ReferenceLocation,
    string? SolutionPath,
    string ProjectPath,
    bool Interactive,
    bool IsRunningInVisualStudio,
    bool FailOnUnresolvedSdk)
{
    internal bool Matches(SdkResolutionContext other) =>
        FileUtilities.PathComparer.Equals(ReferenceLocation.File, other.ReferenceLocation.File)
        && ReferenceLocation.Line == other.ReferenceLocation.Line
        && ReferenceLocation.Column == other.ReferenceLocation.Column
        && FileUtilities.PathComparer.Equals(SolutionPath, other.SolutionPath)
        && FileUtilities.PathComparer.Equals(ProjectPath, other.ProjectPath)
        && Interactive == other.Interactive
        && IsRunningInVisualStudio == other.IsRunningInVisualStudio
        && FailOnUnresolvedSdk == other.FailOnUnresolvedSdk;
}

/// <summary>
/// Immutable, cache-owned representation of an SDK resolver result.
/// </summary>
internal sealed class SdkResultSnapshot
{
    private SdkResultSnapshot(
        bool success,
        SdkReference? resolvedReference,
        string? path,
        string? version,
        ImmutableArray<string> additionalPaths,
        ImmutableDictionary<string, string> propertiesToAdd,
        ImmutableDictionary<string, SdkResultItemSnapshot> itemsToAdd,
        ImmutableDictionary<string, string> environmentVariablesToAdd,
        ImmutableArray<string> warnings,
        ImmutableArray<string> errors)
    {
        Success = success;
        ResolvedReference = resolvedReference;
        Path = path;
        Version = version;
        AdditionalPaths = additionalPaths;
        PropertiesToAdd = propertiesToAdd;
        ItemsToAdd = itemsToAdd;
        EnvironmentVariablesToAdd = environmentVariablesToAdd;
        Warnings = warnings;
        Errors = errors;
    }

    internal bool Success { get; }
    internal SdkReference? ResolvedReference { get; }
    internal string? Path { get; }
    internal string? Version { get; }
    internal ImmutableArray<string> AdditionalPaths { get; }
    internal ImmutableDictionary<string, string> PropertiesToAdd { get; }
    internal ImmutableDictionary<string, SdkResultItemSnapshot> ItemsToAdd { get; }
    internal ImmutableDictionary<string, string> EnvironmentVariablesToAdd { get; }
    internal ImmutableArray<string> Warnings { get; }
    internal ImmutableArray<string> Errors { get; }

    internal long RetainedSizeBytes
    {
        get
        {
            long size = 128;
            size = RetainedSizeEstimator.AddString(size, ResolvedReference?.Name);
            size = RetainedSizeEstimator.AddString(size, ResolvedReference?.Version);
            size = RetainedSizeEstimator.AddString(size, ResolvedReference?.MinimumVersion);
            size = RetainedSizeEstimator.AddString(size, Path);
            size = RetainedSizeEstimator.AddString(size, Version);
            size = RetainedSizeEstimator.AddStrings(size, AdditionalPaths);
            size = RetainedSizeEstimator.AddDictionary(size, PropertiesToAdd);
            size = RetainedSizeEstimator.AddDictionary(size, EnvironmentVariablesToAdd);
            size = RetainedSizeEstimator.AddStrings(size, Warnings);
            size = RetainedSizeEstimator.AddStrings(size, Errors);
            foreach (KeyValuePair<string, SdkResultItemSnapshot> item in ItemsToAdd)
            {
                size = RetainedSizeEstimator.AddString(RetainedSizeEstimator.Add(size, 64), item.Key);
                size = RetainedSizeEstimator.Add(size, item.Value.RetainedSizeBytes);
            }

            return size;
        }
    }

    internal static SdkResultSnapshot Create(BackEnd.SdkResolution.SdkResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var items = ImmutableDictionary.CreateBuilder<string, SdkResultItemSnapshot>(
            StringComparer.OrdinalIgnoreCase);
        if (result.ItemsToAdd is not null)
        {
            foreach (KeyValuePair<string, SdkResultItem> item in result.ItemsToAdd)
            {
                items[item.Key] = SdkResultItemSnapshot.Create(item.Value);
            }
        }

        return new SdkResultSnapshot(
            result.Success,
            result.SdkReference is null
                ? null
                : new SdkReference(
                    result.SdkReference.Name,
                    result.SdkReference.Version,
                    result.SdkReference.MinimumVersion),
            result.Path,
            result.Version,
            result.AdditionalPaths is null ? [] : [.. result.AdditionalPaths],
            CopyDictionary(result.PropertiesToAdd, StringComparer.OrdinalIgnoreCase),
            items.ToImmutable(),
            CopyDictionary(result.EnvironmentVariablesToAdd, CommunicationsUtilities.EnvironmentVariableComparer),
            result.Warnings is null ? [] : [.. result.Warnings],
            result.Errors is null ? [] : [.. result.Errors]);
    }

    internal bool Matches(BackEnd.SdkResolution.SdkResult result) =>
        result is not null
        && Success == result.Success
        && EqualityComparer<SdkReference?>.Default.Equals(ResolvedReference, result.SdkReference)
        && FileUtilities.PathComparer.Equals(Path, result.Path)
        && StringComparer.OrdinalIgnoreCase.Equals(Version, result.Version)
        && SequenceEqual(AdditionalPaths, result.AdditionalPaths, FileUtilities.PathComparer)
        && DictionaryEqual(PropertiesToAdd, result.PropertiesToAdd, StringComparer.Ordinal)
        && ItemDictionaryEqual(ItemsToAdd, result.ItemsToAdd)
        && DictionaryEqual(EnvironmentVariablesToAdd, result.EnvironmentVariablesToAdd, StringComparer.Ordinal)
        && SequenceEqual(Warnings, result.Warnings, StringComparer.Ordinal)
        && SequenceEqual(Errors, result.Errors, StringComparer.Ordinal);

    private static ImmutableDictionary<string, string> CopyDictionary(
        IDictionary<string, string>? source,
        IEqualityComparer<string> comparer)
    {
        var copy = ImmutableDictionary.CreateBuilder<string, string>(comparer);
        if (source is not null)
        {
            foreach (KeyValuePair<string, string> item in source)
            {
                copy[item.Key] = item.Value;
            }
        }

        return copy.ToImmutable();
    }

    internal static bool DictionaryEqual(
        IReadOnlyDictionary<string, string> expected,
        IDictionary<string, string>? actual,
        StringComparer valueComparer)
    {
        if (expected.Count != (actual?.Count ?? 0))
        {
            return false;
        }

        if (actual is null)
        {
            return true;
        }

        foreach (KeyValuePair<string, string> item in actual)
        {
            if (!expected.TryGetValue(item.Key, out string? value)
                || !valueComparer.Equals(value, item.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ItemDictionaryEqual(
        IReadOnlyDictionary<string, SdkResultItemSnapshot> expected,
        IDictionary<string, SdkResultItem>? actual)
    {
        if (expected.Count != (actual?.Count ?? 0))
        {
            return false;
        }

        if (actual is null)
        {
            return true;
        }

        foreach (KeyValuePair<string, SdkResultItem> item in actual)
        {
            if (!expected.TryGetValue(item.Key, out SdkResultItemSnapshot? value)
                || !value.Matches(item.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SequenceEqual(
        ImmutableArray<string> expected,
        IEnumerable<string>? actual,
        IEqualityComparer<string> comparer)
    {
        if (actual is null)
        {
            return expected.IsEmpty;
        }

        using IEnumerator<string> enumerator = actual.GetEnumerator();
        foreach (string expectedValue in expected)
        {
            if (!enumerator.MoveNext() || !comparer.Equals(expectedValue, enumerator.Current))
            {
                return false;
            }
        }

        return !enumerator.MoveNext();
    }
}

internal sealed class SdkResultItemSnapshot
{
    private SdkResultItemSnapshot(
        string itemSpec,
        ImmutableDictionary<string, string> metadata)
    {
        ItemSpec = itemSpec;
        Metadata = metadata;
    }

    internal string ItemSpec { get; }
    internal ImmutableDictionary<string, string> Metadata { get; }
    internal long RetainedSizeBytes =>
        RetainedSizeEstimator.AddDictionary(
            RetainedSizeEstimator.AddString(64, ItemSpec),
            Metadata);

    internal static SdkResultItemSnapshot Create(SdkResultItem item)
    {
        var metadata = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.OrdinalIgnoreCase);
        if (item.Metadata is not null)
        {
            foreach (KeyValuePair<string, string> value in item.Metadata)
            {
                metadata[value.Key] = value.Value;
            }
        }

        return new SdkResultItemSnapshot(item.ItemSpec, metadata.ToImmutable());
    }

    internal bool Matches(SdkResultItem item) =>
        string.Equals(ItemSpec, item.ItemSpec, StringComparison.Ordinal)
        && SdkResultSnapshot.DictionaryEqual(Metadata, item.Metadata, StringComparer.Ordinal);
}

internal static class RetainedSizeEstimator
{
    internal static long Add(long size, long addition) =>
        addition < 0 || size > long.MaxValue - addition ? long.MaxValue : size + addition;

    internal static long AddString(long size, string? value) =>
        Add(size, value is null ? 0 : 24L + (value.Length * 2L));

    internal static long AddStrings(long size, IEnumerable<string> values)
    {
        foreach (string value in values)
        {
            size = AddString(Add(size, 8), value);
        }

        return size;
    }

    internal static long AddDictionary(
        long size,
        IEnumerable<KeyValuePair<string, string>> values)
    {
        foreach (KeyValuePair<string, string> value in values)
        {
            size = AddString(Add(size, 48), value.Key);
            size = AddString(size, value.Value);
        }

        return size;
    }
}

/// <summary>
/// A registry request and the value it returned, before MSBuild string conversion or escaping.
/// Views are string tokens the intrinsic considers; ignored non-string arguments are not included.
/// An empty list does not identify a winning view. A null key is retained for accepted no-view requests.
/// Binary, multi-string, and character arrays are copied to immutable arrays. Registry-dependent evaluations are
/// recorded completely but are not admitted to checked reuse.
/// </summary>
internal sealed record RegistryRead(
    string? KeyName,
    string ValueName,
    object? Value,
    ImmutableArray<string> RequestedViews);

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
    bool ExplicitToolsVersionSpecified,
    string CommandLinePropertyNames,
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
/// <param name="SdkResolutions">SDK references resolved during evaluation and their results.</param>
/// <param name="RegistryReads">Registry keys, value names, requested views, and returned values, in observation order.</param>
/// <param name="NonCacheable">Why the result must never be reused, or <see cref="NonCacheableReason.None"/>.</param>
/// <param name="NonCacheableDetail">The input that made the evaluation non-cacheable, for diagnostics.</param>
internal sealed record EvaluationInputs(
    EvaluationInputKey Key,
    IReadOnlyDictionary<string, FileDependency> Files,
    IReadOnlyDictionary<string, string?> EnvironmentReads,
    ImmutableArray<SdkDependency> SdkResolutions,
    ImmutableArray<RegistryRead> RegistryReads,
    NonCacheableReason NonCacheable,
    string? NonCacheableDetail)
{
    internal bool IsCacheable => NonCacheable == NonCacheableReason.None;
}
