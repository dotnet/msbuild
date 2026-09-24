// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

#nullable enable

namespace Microsoft.Build.BackEnd;

/// <summary>
/// The result of validating a project instance snapshot for reuse.
/// </summary>
internal enum ProjectInstanceSnapshotValidationResult
{
    /// <summary>
    /// The snapshot must not be reused.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// The snapshot may be reused.
    /// </summary>
    Valid,
}

/// <summary>
/// Validates whether a cached project instance snapshot may be reused.
/// </summary>
internal interface IProjectInstanceSnapshotValidator
{
    /// <summary>
    /// Determines whether the snapshot may be reused for the specified evaluation request.
    /// </summary>
    /// <remarks>
    /// Implementations must fail closed and return <see cref="ProjectInstanceSnapshotValidationResult.Invalid"/>
    /// when project or import contents, file enumeration, relevant environment values, SDK and toolset
    /// resolution state, or any other evaluation input not represented by the key cannot be verified.
    /// </remarks>
    ProjectInstanceSnapshotValidationResult Validate(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry entry);
}

/// <summary>
/// The production default validator, which rejects every snapshot until invalidation is implemented.
/// </summary>
internal sealed class RejectingProjectInstanceSnapshotValidator : IProjectInstanceSnapshotValidator
{
    internal static RejectingProjectInstanceSnapshotValidator Instance { get; } = new();

    private RejectingProjectInstanceSnapshotValidator()
    {
    }

    public ProjectInstanceSnapshotValidationResult Validate(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entry);
        return ProjectInstanceSnapshotValidationResult.Invalid;
    }
}

internal sealed class UnsafeProjectInstanceSnapshotValidator : IProjectInstanceSnapshotValidator
{
    internal static UnsafeProjectInstanceSnapshotValidator Instance { get; } = new();

    private UnsafeProjectInstanceSnapshotValidator()
    {
    }

    public ProjectInstanceSnapshotValidationResult Validate(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entry);
        return ProjectInstanceSnapshotValidationResult.Valid;
    }
}

internal sealed class FileSystemProjectInstanceSnapshotValidator : IProjectInstanceSnapshotValidator
{
    internal static FileSystemProjectInstanceSnapshotValidator Instance { get; } = new();

    private FileSystemProjectInstanceSnapshotValidator()
    {
    }

    public ProjectInstanceSnapshotValidationResult Validate(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry entry)
        => Validate(key, entry, validationContext: null);

    internal ProjectInstanceSnapshotValidationResult Validate(
        ProjectInstanceSnapshotCacheKey key,
        ProjectInstanceSnapshotCacheEntry entry,
        ProjectInstanceSnapshotValidationContext? validationContext)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.ValidationData is not EvaluationInputsSnapshotValidationData data
            || !key.Matches(data.Inputs.Key)
            || !EvaluationInputValidator.IsFileSystemCurrent(data.Inputs, out _))
        {
            return ProjectInstanceSnapshotValidationResult.Invalid;
        }

        if (data.Inputs.SdkResolutions.Length > 0 && validationContext is null)
        {
            return ProjectInstanceSnapshotValidationResult.Invalid;
        }

        foreach (SdkDependency sdk in data.Inputs.SdkResolutions)
        {
            if (!validationContext!.Validate(sdk))
            {
                return ProjectInstanceSnapshotValidationResult.Invalid;
            }
        }

        return ProjectInstanceSnapshotValidationResult.Valid;
    }
}

/// <summary>
/// Request-scoped services used for SDK validation. Resolved results are retained only long enough to
/// feed an immediate fresh fallback, preventing a second resolver invocation and replaying deferred diagnostics once.
/// </summary>
internal sealed class ProjectInstanceSnapshotValidationContext
{
    private readonly ISdkResolverService _sdkResolverService;
    private readonly ILoggingService _loggingService;
    private readonly BuildEventContext _buildEventContext;
    private readonly int _submissionId;
    private readonly LockType _lock = new();
    private readonly List<PreResolvedSdkResult> _resolved = [];

    internal ProjectInstanceSnapshotValidationContext(
        ISdkResolverService sdkResolverService,
        ILoggingService loggingService,
        BuildEventContext buildEventContext,
        int submissionId)
    {
        ArgumentNullException.ThrowIfNull(sdkResolverService);
        ArgumentNullException.ThrowIfNull(loggingService);
        ArgumentNullException.ThrowIfNull(buildEventContext);
        _sdkResolverService = sdkResolverService;
        _loggingService = loggingService;
        _buildEventContext = buildEventContext;
        _submissionId = submissionId;
    }

    internal bool Validate(SdkDependency dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        LoggingContext deferredLogging = LoggingContext.CreateDeferred(
            _loggingService,
            _buildEventContext);
        SdkResolutionContext context = dependency.Context;
        SdkResult result;
        try
        {
            result = _sdkResolverService.ResolveSdk(
                _submissionId,
                dependency.Reference,
                deferredLogging,
                context.ReferenceLocation,
                context.SolutionPath,
                context.ProjectPath,
                context.Interactive,
                context.IsRunningInVisualStudio,
                context.FailOnUnresolvedSdk);
        }
        catch (Exception ex) when (FindNonRecoverableException(ex) is Exception)
        {
            ExceptionDispatchInfo.Capture(FindNonRecoverableException(ex)!).Throw();
            throw;
        }
        catch (Exception)
        {
            return false;
        }

        if (result is null)
        {
            return false;
        }

        bool hasResultDiagnostics = HasDiagnostics(result.Warnings) || HasDiagnostics(result.Errors);
        bool hasDeferredDiagnostics = deferredLogging.HasDeferredEvaluationDiagnostics;
        if (!hasResultDiagnostics || hasDeferredDiagnostics)
        {
            lock (_lock)
            {
                _resolved.Add(new PreResolvedSdkResult(
                    dependency.Reference,
                    context,
                    result,
                    deferredLogging));
            }
        }

        return !hasDeferredDiagnostics
            && !hasResultDiagnostics
            && dependency.Result.Matches(result);
    }

    internal ISdkResolverService GetResolverForFreshEvaluation()
    {
        lock (_lock)
        {
            return _resolved.Count == 0
                ? _sdkResolverService
                : new PreResolvedSdkResolverService(
                    _sdkResolverService,
                    _submissionId,
                    [.. _resolved]);
        }
    }

    private static bool HasDiagnostics(IEnumerable<string>? diagnostics)
    {
        if (diagnostics is not null)
        {
            using IEnumerator<string> enumerator = diagnostics.GetEnumerator();
            return enumerator.MoveNext();
        }

        return false;
    }

    private static Exception? FindNonRecoverableException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (Microsoft.Build.Framework.ExceptionHandling.IsCriticalException(current)
                || current is OperationCanceledException
                || current is BuildAbortedException)
            {
                return current;
            }
        }

        return null;
    }
}

internal sealed record PreResolvedSdkResult(
    SdkReference Reference,
    SdkResolutionContext Context,
    SdkResult Result,
    LoggingContext DeferredLogging)
{
    private readonly LockType _lock = new();
    private bool _diagnosticsReplayed;

    internal void ReplayDiagnostics(LoggingContext loggingContext)
    {
        lock (_lock)
        {
            if (_diagnosticsReplayed)
            {
                return;
            }

            DeferredLogging.ReplayDeferredEvents(loggingContext);
            _diagnosticsReplayed = true;
        }
    }
}

internal sealed class PreResolvedSdkResolverService : ISdkResolverService
{
    private readonly ISdkResolverService _wrapped;
    private readonly int _submissionId;
    private readonly PreResolvedSdkResult[] _resolved;

    internal PreResolvedSdkResolverService(
        ISdkResolverService wrapped,
        int submissionId,
        PreResolvedSdkResult[] resolved)
    {
        ArgumentNullException.ThrowIfNull(wrapped);
        ArgumentNullException.ThrowIfNull(resolved);
        _wrapped = wrapped;
        _submissionId = submissionId;
        _resolved = resolved;
    }

    public Action<INodePacket> SendPacket => _wrapped.SendPacket;

    public bool IsNodeShutDown
    {
        get => _wrapped.IsNodeShutDown;
        set => _wrapped.IsNodeShutDown = value;
    }

    public void ClearCache(int submissionId) => _wrapped.ClearCache(submissionId);

    public void ClearCaches() => _wrapped.ClearCaches();

    public SdkResult ResolveSdk(
        int submissionId,
        SdkReference sdk,
        LoggingContext loggingContext,
        Construction.ElementLocation sdkReferenceLocation,
        string solutionPath,
        string projectPath,
        bool interactive,
        bool isRunningInVisualStudio,
        bool failOnUnresolvedSdk)
    {
        var requestedContext = new SdkResolutionContext(
            sdkReferenceLocation,
            solutionPath,
            projectPath,
            interactive,
            isRunningInVisualStudio,
            failOnUnresolvedSdk);
        if (submissionId == _submissionId)
        {
            foreach (PreResolvedSdkResult resolution in _resolved)
            {
                if (resolution.Reference.Equals(sdk)
                    && resolution.Context.Matches(requestedContext))
                {
                    resolution.ReplayDiagnostics(loggingContext);
                    return resolution.Result;
                }
            }
        }

        return _wrapped.ResolveSdk(
            submissionId,
            sdk,
            loggingContext,
            sdkReferenceLocation,
            solutionPath,
            projectPath,
            interactive,
            isRunningInVisualStudio,
            failOnUnresolvedSdk);
    }
}
