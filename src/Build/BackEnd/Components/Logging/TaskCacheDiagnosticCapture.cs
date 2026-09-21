// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd.Logging;

/// <summary>
/// Watches the whole invocation while snapshotting only warnings from the phase
/// replaced by a hit. Setup, cleanup, raw errors, and unsupported warnings block caching.
/// </summary>
internal sealed class TaskCacheDiagnosticCapture : IDisposable
{
    private readonly LoggingService _source;
    private readonly BuildEventContext _context;
    private readonly object _gate = new();
    private List<TaskCacheWarning>? _warnings;
    private bool _disposed;
    private bool _replayable;
    private bool _hasDiagnostics;
    private bool _blocking;
    private int _warningBytes = sizeof(int);

    internal TaskCacheDiagnosticCapture(LoggingService source, BuildEventContext context)
    {
        _source = source;
        _context = context;
    }

    public bool HasDiagnostics
    {
        get
        {
            lock (_gate)
            {
                return _hasDiagnostics;
            }
        }
    }

    internal bool HasBlockingDiagnostics
    {
        get
        {
            lock (_gate)
            {
                return _blocking;
            }
        }
    }

    internal void BeginReplayablePhase()
    {
        lock (_gate)
        {
            _replayable = !_disposed;
        }
    }

    internal void EndReplayablePhase()
    {
        lock (_gate)
        {
            _replayable = false;
        }
    }

    internal TaskCacheWarning[] GetWarnings()
    {
        lock (_gate)
        {
            return _warnings?.ToArray() ?? [];
        }
    }

    internal void RecordRawError(BuildEventContext context)
    {
        if (context.NodeId == _context.NodeId && context.ProjectContextId == _context.ProjectContextId
            && context.TargetId == _context.TargetId && context.TaskId == _context.TaskId)
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    _hasDiagnostics = true;
                    _blocking = true;
                }
            }
        }
    }

    internal void RecordDiagnostic(BuildEventArgs buildEvent, BuildEventContext context)
    {
        // Task IDs alone are not unique across concurrent projects and nodes.
        if (context.NodeId == _context.NodeId
            && context.ProjectContextId == _context.ProjectContextId
            && context.TargetId == _context.TargetId
            && context.TaskId == _context.TaskId)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }
                _hasDiagnostics = true;
                if (!_replayable || buildEvent is not BuildWarningEventArgs warning || _blocking)
                {
                    _blocking = true;
                    return;
                }
                try
                {
                    TaskCacheWarning? snapshot = TaskCacheWarning.Snapshot(warning, out int size);
                    if (snapshot is null || (_warnings?.Count ?? 0) >= TaskCacheWarning.MaximumCount
                        || size > TaskCacheWarning.MaximumPayloadBytes - _warningBytes)
                    {
                        _blocking = true;
                        return;
                    }
                    (_warnings ??= []).Add(snapshot);
                    _warningBytes += size;
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    // Logging must still handle the original event normally.
                    _blocking = true;
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _replayable = false;
        }
        _source.RemoveTaskDiagnosticCapture(this);
    }
}
