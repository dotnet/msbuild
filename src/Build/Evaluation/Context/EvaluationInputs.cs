// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

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
/// State of a path as evaluation observed it. Probes require only the path kind to remain unchanged;
/// reads additionally require the recorded metadata. Glob traversal timestamps signal when matching results
/// must be checked again, rather than invalidating on unrelated directory changes.
/// </summary>
internal readonly record struct FileDependency(
    PathKind Kind,
    DateTime LastWriteTimeUtc,
    long Length,
    bool RequiresMetadata = true,
    bool RequiresGlobValidation = false);

/// <summary>
/// The wildcard expansion evaluation consumed, detached from mutable matcher inputs and returned arrays.
/// </summary>
internal sealed class GlobDependency
{
    private readonly List<string>? _excludes;
    private readonly string[] _files;
    private readonly FileMatcherDriver _driver;
    private readonly FileMatcherCaseFolding _caseFolding;
    private readonly bool _usesFileSystemEntryCache;
    private readonly string _culture;

    internal GlobDependency(FileMatcher.GlobResultObservation observation)
    {
        ProjectDirectory = observation.ProjectDirectory;
        Filespec = observation.Filespec;
        FromCache = observation.FromCache;
        _driver = observation.Driver;
        _caseFolding = observation.CaseFolding;
        _usesFileSystemEntryCache = observation.UsesFileSystemEntryCache;
        _culture = CultureInfo.CurrentCulture.Name;
        _excludes = observation.Excludes is null ? null : [.. observation.Excludes];
        _files = (string[])observation.Files.Clone();
        // Match the ordering consumed by EngineFileUtilities.GetFileList, before it escapes the paths.
        Array.Sort(_files, StringComparer.OrdinalIgnoreCase);
    }

    internal string ProjectDirectory { get; }
    internal string Filespec { get; }
    internal FileMatcherDriver Driver => _driver;
    internal bool UsesFileSystemEntryCache => _usesFileSystemEntryCache;
    /// <summary>Whether the cached result may predate this manifest's directory observations.</summary>
    internal bool FromCache { get; }
    internal ReadOnlySpan<string> Files => _files;

    internal long RetainedSizeBytes
    {
        get
        {
            long size = RetainedSizeEstimator.AddString(96, ProjectDirectory);
            size = RetainedSizeEstimator.AddString(size, Filespec);
            size = RetainedSizeEstimator.AddString(size, _culture);
            size = RetainedSizeEstimator.AddStrings(RetainedSizeEstimator.Add(size, 24), _files);
            return _excludes is null
                ? size
                : RetainedSizeEstimator.AddStrings(RetainedSizeEstimator.Add(size, 56), _excludes);
        }
    }

    /// <param name="sharedEntryCache">
    /// An optional directory-listing cache shared by the globs of one entry's validation, so overlapping directory
    /// trees are listed once. It must not outlive that validation: a file added later has to be visible.
    /// </param>
    internal bool IsCurrent(ConcurrentDictionary<string, IReadOnlyList<string>>? sharedEntryCache = null)
    {
        if (_caseFolding == FileMatcherCaseFolding.LegacyCurrentCulture
            && !string.Equals(_culture, CultureInfo.CurrentCulture.Name, StringComparison.Ordinal))
        {
            return false;
        }

        var current = FileMatcher.GetFilesForValidation(
            ProjectDirectory, Filespec, _excludes, _driver, _caseFolding, _usesFileSystemEntryCache, sharedEntryCache);
        if (current.GlobFailure is not null
            || current.Action is FileMatcher.SearchAction.ReturnFileSpec
                or FileMatcher.SearchAction.FailOnDriveEnumeratingWildcard
                or FileMatcher.SearchAction.LogDriveEnumeratingWildcard
            || current.FileList.Length != _files.Length)
        {
            return false;
        }

        Array.Sort(current.FileList, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _files.Length; i++)
        {
            if (!string.Equals(_files[i], current.FileList[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}

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
/// <param name="Globs">Wildcard expansions whose results must remain unchanged when traversed directories change.</param>
internal sealed record EvaluationInputs(
    EvaluationInputKey Key,
    IReadOnlyDictionary<string, FileDependency> Files,
    IReadOnlyDictionary<string, string?> EnvironmentReads,
    ImmutableArray<SdkDependency> SdkResolutions,
    ImmutableArray<RegistryRead> RegistryReads,
    NonCacheableReason NonCacheable,
    string? NonCacheableDetail,
    ImmutableArray<GlobDependency> Globs = default)
{
    private ConcurrentDictionary<string, FileDependency>? _validatedGlobDirectories;

    internal bool IsCacheable => NonCacheable == NonCacheableReason.None;

    /// <summary>
    /// The state of a glob-traversed directory at which every glob of this entry was last replayed and matched,
    /// when that differs from the state evaluation recorded. Only a directory whose timestamp alone signals a replay
    /// can have one, because a changed timestamp is the only thing that makes the entry replay.
    /// </summary>
    internal bool TryGetValidatedGlobDirectory(string path, out FileDependency validated)
    {
        ConcurrentDictionary<string, FileDependency>? directories = Volatile.Read(ref _validatedGlobDirectories);
        if (directories is not null)
        {
            return directories.TryGetValue(path, out validated);
        }

        validated = default;
        return false;
    }

    /// <summary>
    /// Remembers the directory states observed before a replay in which every glob matched, so an unchanged state is not
    /// replayed again. The manifest itself stays immutable: a later change to a directory produces a state that differs
    /// from the remembered one, and a replay that does not match remembers nothing.
    /// </summary>
    internal void RememberValidatedGlobDirectories(IEnumerable<KeyValuePair<string, FileDependency>> directories)
    {
        ConcurrentDictionary<string, FileDependency>? remembered = Volatile.Read(ref _validatedGlobDirectories);
        if (remembered is null)
        {
            Interlocked.CompareExchange(ref _validatedGlobDirectories, new(FileUtilities.PathComparer), null);
            remembered = _validatedGlobDirectories!;
        }

        foreach (KeyValuePair<string, FileDependency> directory in directories)
        {
            remembered[directory.Key] = directory.Value;
        }
    }
}
