// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation.Context;

/// <summary>
/// Collects the inputs one evaluation consumes. Created only when <see cref="Traits.RecordEvaluationInputs"/> is set,
/// so the rest of the engine pays one null check per seam when recording is off.
/// Once the evaluation is known to be non-cacheable, further observations are skipped: the manifest will not be used.
/// </summary>
internal sealed class EvaluationInputRecorder
{
    // Guarded by its own lock: glob expansion enumerates directories in parallel, every other seam runs on the
    // evaluation thread. SDK-style projects record 150 to 250 paths, so one growth covers them.
    private readonly Dictionary<string, FileDependency> _files = new(128, FileUtilities.PathComparer);
    private NonCacheableReason _nonCacheable;
    private string? _nonCacheableDetail;
    private bool _frozen;

    /// <summary>
    /// False once the evaluation is non-cacheable, since the manifest will not be used, or once <see cref="Freeze"/>
    /// handed the collections out.
    /// </summary>
    private bool IsRecording => !_frozen && _nonCacheable == NonCacheableReason.None;

    internal static EvaluationInputRecorder? CreateIfEnabled() =>
        Traits.Instance.RecordEvaluationInputs ? new EvaluationInputRecorder() : null;

    /// <summary>
    /// Records a path evaluation read or enumerated.
    /// </summary>
    internal void RecordPath(string? path)
    {
        if (path is null || path.Length == 0 || !IsRecording)
        {
            return;
        }

        try
        {
            TryGetOrObserve(Canonicalize(path), out _);
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            MarkNonCacheable(NonCacheableReason.RecorderFailure, ex.Message);
        }
    }

    /// <summary>
    /// Records a project file whose content evaluation consumed as read at <paramref name="lastWriteTimeUtcWhenRead"/>.
    /// A file that changed since it was read, for example behind a cached <c>ProjectRootElement</c>, cannot be reused.
    /// </summary>
    internal void RecordProjectSource(string? fullPath, DateTime lastWriteTimeUtcWhenRead)
    {
        if (fullPath is null || fullPath.Length == 0 || !IsRecording)
        {
            return;
        }

        try
        {
            // A probe may have recorded the file moments earlier; its stat serves as well as a new one.
            string path = Canonicalize(fullPath);
            if (TryGetOrObserve(path, out FileDependency current)
                && (current.Kind != PathKind.File || current.LastWriteTimeUtc != lastWriteTimeUtcWhenRead))
            {
                MarkNonCacheable(NonCacheableReason.ConflictingObservation, path);
            }
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            MarkNonCacheable(NonCacheableReason.RecorderFailure, ex.Message);
        }
    }

    /// <summary>
    /// Records an existence probe. A result that contradicts the recorded state of the same path means
    /// evaluation saw the file system change under it, so the result cannot be reused.
    /// </summary>
    internal void RecordProbe(string? path, ProbeKind kind, bool exists)
    {
        if (path is null || path.Length == 0 || !IsRecording)
        {
            return;
        }

        try
        {
            string fullPath = Canonicalize(path);
            if (TryGetOrObserve(fullPath, out FileDependency recorded) && exists != Satisfies(recorded.Kind, kind))
            {
                MarkNonCacheable(NonCacheableReason.ConflictingObservation, fullPath);
            }
        }
        catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
        {
            MarkNonCacheable(NonCacheableReason.RecorderFailure, ex.Message);
        }
    }

    /// <summary>
    /// Records what a property function read from the file system, environment, or registry, or marks the evaluation
    /// non-cacheable when the function is volatile or not classified.
    /// </summary>
    internal void RecordPropertyFunction(Type receiverType, string member, bool isInstance, object?[] arguments, object? result)
    {
        if (!IsRecording)
        {
            return;
        }

        PropertyFunctionEffect effect = PropertyFunctionEffects.Classify(receiverType, member, isInstance, arguments.Length);
        if (effect == PropertyFunctionEffect.Pure)
        {
            return;
        }

        string? firstArgument = arguments.Length > 0 ? arguments[0] as string : null;
        switch (effect)
        {
            case PropertyFunctionEffect.ProbeFile when firstArgument is not null:
                RecordProbe(firstArgument, ProbeKind.File, result is true);
                break;
            case PropertyFunctionEffect.ProbeDirectory when firstArgument is not null:
                RecordProbe(firstArgument, ProbeKind.Directory, result is true);
                break;
            case PropertyFunctionEffect.ProbePath when firstArgument is not null:
                RecordProbe(firstArgument, ProbeKind.FileOrDirectory, result is true);
                break;
            case PropertyFunctionEffect.ReadFile when firstArgument is not null:
                RecordPath(firstArgument);
                break;
            case PropertyFunctionEffect.ReadDirectory when firstArgument is not null:
                RecordPath(firstArgument);
                break;
            case PropertyFunctionEffect.PureUnlessPathArgument when !HasPathArgument(arguments):
                break;
            case PropertyFunctionEffect.Volatile:
                MarkNonCacheable(NonCacheableReason.VolatilePropertyFunction, $"{receiverType.FullName}::{member}");
                break;
            default:
                MarkNonCacheable(NonCacheableReason.UnclassifiedPropertyFunction, $"{receiverType.FullName}::{member}");
                break;
        }
    }

    /// <summary>
    /// Marks the evaluation non-cacheable when an expression reads item timestamps, as <c>%(ModifiedTime)</c> or
    /// <c>%(Compile.CreatedTime)</c>. Those reads happen deep inside static expansion code, so the expression is
    /// checked instead of the read.
    /// </summary>
    internal void RecordMetadataExpression(string expression)
    {
        if (IsRecording
            && ExpressionShredder.IndexOfMetadataMarker(expression) >= 0
            && (ReferencesModifier(expression, ItemSpecModifiers.ModifiedTime)
                || ReferencesModifier(expression, ItemSpecModifiers.CreatedTime)
                || ReferencesModifier(expression, ItemSpecModifiers.AccessedTime)))
        {
            MarkNonCacheable(NonCacheableReason.ItemTimestampMetadata, expression);
        }
    }

    /// <summary>
    /// Marks the evaluation non-cacheable when an item function or a metadata name reads timestamps, as
    /// <c>@(Compile->ModifiedTime())</c>, <c>@(Compile->Metadata('ModifiedTime'))</c>, or <c>MatchOnMetadata="ModifiedTime"</c>.
    /// </summary>
    internal void RecordMetadataName(string name)
    {
        if (IsRecording
            && ItemSpecModifiers.TryGetModifierKind(name, out ItemSpecModifierKind kind)
            && kind is ItemSpecModifierKind.ModifiedTime or ItemSpecModifierKind.CreatedTime or ItemSpecModifierKind.AccessedTime)
        {
            MarkNonCacheable(NonCacheableReason.ItemTimestampMetadata, name);
        }
    }

    internal void MarkNonCacheable(NonCacheableReason reason, string? detail = null)
    {
        if (IsRecording)
        {
            _nonCacheable = reason;
            _nonCacheableDetail = detail;
        }
    }

    /// <summary>
    /// Ends recording and hands the collected inputs over without copying them.
    /// </summary>
    internal EvaluationInputs Freeze(EvaluationInputKey key)
    {
        _frozen = true;
        return new EvaluationInputs(
            key,
            new ReadOnlyDictionary<string, FileDependency>(_files),
            _nonCacheable,
            _nonCacheableDetail);
    }

    /// <summary>
    /// Reads the current state of a path with one system call. Returns false for a symbolic link or junction: its own
    /// timestamp does not change when its target does, so it cannot be validated.
    /// </summary>
    internal static bool TryStat(string fullPath, out FileDependency dependency)
    {
        if (!NativeMethodsShared.TryGetFileSystemEntry(fullPath, out bool isDirectory, out bool isReparsePoint, out DateTime lastWriteTimeUtc, out long length))
        {
            dependency = default;
            return true;
        }

        if (isReparsePoint && IsLink(fullPath, isDirectory))
        {
            dependency = default;
            return false;
        }

        dependency = new FileDependency(isDirectory ? PathKind.Directory : PathKind.File, lastWriteTimeUtc, length);
        return true;
    }

    private bool TryObserve(string fullPath, out FileDependency dependency)
    {
        if (TryStat(fullPath, out dependency))
        {
            return true;
        }

        MarkNonCacheable(NonCacheableReason.Link, fullPath);
        return false;
    }

    /// <summary>
    /// Returns the recorded state of a path, observing it first when it is new. The stat runs outside the lock so
    /// parallel glob enumeration does not serialize on it.
    /// </summary>
    private bool TryGetOrObserve(string fullPath, out FileDependency recorded)
    {
        lock (_files)
        {
            if (_files.TryGetValue(fullPath, out recorded))
            {
                return true;
            }
        }

        if (!TryObserve(fullPath, out FileDependency current))
        {
            return false;
        }

        lock (_files)
        {
            if (!_files.TryGetValue(fullPath, out recorded))
            {
                _files.Add(fullPath, current);
                recorded = current;
            }
        }

        return true;
    }

    /// <summary>
    /// Only symbolic links and junctions redirect to another path; cloud placeholders and other reparse points are ordinary entries.
    /// </summary>
    private static bool IsLink(string fullPath, bool isDirectory)
    {
        if (NativeMethodsShared.IsWindows)
        {
            return NativeMethodsShared.IsSymbolicLinkOrJunction(fullPath);
        }

#if NET
        FileSystemInfo entry = isDirectory ? new DirectoryInfo(fullPath) : new FileInfo(fullPath);
        return entry.LinkTarget is not null;
#else
        return true;
#endif
    }

    private static bool Satisfies(PathKind recorded, ProbeKind probed) =>
        probed switch
        {
            ProbeKind.File => recorded == PathKind.File,
            ProbeKind.Directory => recorded == PathKind.Directory,
            _ => recorded != PathKind.Missing,
        };

    /// <summary>
    /// True when the modifier name appears as a metadata reference, not as part of a longer name. The reference grammar
    /// allows whitespace around the name, as in <c>%( ModifiedTime )</c>.
    /// </summary>
    private static bool ReferencesModifier(string expression, string modifier)
    {
        int index = expression.IndexOf(modifier, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            int before = index - 1;
            while (before >= 0 && char.IsWhiteSpace(expression[before]))
            {
                before--;
            }

            int after = index + modifier.Length;
            while (after < expression.Length && char.IsWhiteSpace(expression[after]))
            {
                after++;
            }

            if (before >= 0 && expression[before] is '(' or '.' && after < expression.Length && expression[after] == ')')
            {
                return true;
            }

            index = expression.IndexOf(modifier, index + modifier.Length, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// True when an argument names a location: a rooted path, or a relative one with a directory separator, which the
    /// callee resolves against the working directory. Identifiers and versions contain neither.
    /// </summary>
    private static bool HasPathArgument(object?[] arguments)
    {
        foreach (object? argument in arguments)
        {
            if (argument is string path && path.Length > 0 && (path.IndexOfAny(s_directorySeparators) >= 0 || IsRooted(path)))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly char[] s_directorySeparators = ['/', '\\'];

    private static bool IsRooted(string path)
    {
        try
        {
            return Path.IsPathRooted(path);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Full path without a trailing separator, so a directory is one entry however it was spelled. The result is
    /// interned so manifests of different projects share one string per SDK file instead of each holding its own.
    /// </summary>
    private static string Canonicalize(string path)
    {
        string fullPath = FileUtilities.NormalizePath(path);
        int last = fullPath.Length - 1;
        bool endsWithSeparator = fullPath[last] == Path.DirectorySeparatorChar || fullPath[last] == Path.AltDirectorySeparatorChar;
        if (endsWithSeparator && !string.Equals(Path.GetPathRoot(fullPath), fullPath, StringComparison.Ordinal))
        {
            fullPath = fullPath.Substring(0, last);
        }

        return Strings.WeakIntern(fullPath);
    }
}
