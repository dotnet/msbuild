// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

public class BuildCheckBinaryLogReplaySourcerWrapper : IBinaryLogReplaySource
{
    private readonly BinaryLogReplayEventSource _replayEventSource;
    private readonly IBuildEventHandler _buildCheckEventHandler;

    public BuildCheckBinaryLogReplaySourcerWrapper(
        BinaryLogReplayEventSource replayEventSource,
        IBuildEventHandler buildCheckEventHandler)
    {
        _replayEventSource = replayEventSource;
        _buildCheckEventHandler = buildCheckEventHandler;

        InitializeEventHandlers();
    }

    public void Replay(string sourceFilePath, CancellationToken cancellationToken)
        => _replayEventSource.Replay(sourceFilePath, cancellationToken, Dispatch);

    private void Dispatch(BuildEventArgs buildEvent)
    {
        _replayEventSource.Dispatch(buildEvent);

        _buildCheckEventHandler.HandleBuildEvent(buildEvent);
    }

    #region Events

    public event BuildMessageEventHandler? MessageRaised;
    public event BuildErrorEventHandler? ErrorRaised;
    public event BuildWarningEventHandler? WarningRaised;
    public event BuildStartedEventHandler? BuildStarted;
    public event BuildFinishedEventHandler? BuildFinished;
    public event ProjectStartedEventHandler? ProjectStarted;
    public event ProjectFinishedEventHandler? ProjectFinished;
    public event TargetStartedEventHandler? TargetStarted;
    public event TargetFinishedEventHandler? TargetFinished;
    public event TaskStartedEventHandler? TaskStarted;
    public event TaskFinishedEventHandler? TaskFinished;
    public event CustomBuildEventHandler? CustomEventRaised;
    public event BuildStatusEventHandler? StatusEventRaised;
    public event AnyEventHandler? AnyEventRaised;

    private void InitializeEventHandlers()
    {
        _replayEventSource.MessageRaised += (sender, e) => MessageRaised?.Invoke(sender, e);
        _replayEventSource.ErrorRaised += (sender, e) => ErrorRaised?.Invoke(sender, e);
        _replayEventSource.WarningRaised += (sender, e) => WarningRaised?.Invoke(sender, e);
        _replayEventSource.BuildStarted += (sender, e) => BuildStarted?.Invoke(sender, e);
        _replayEventSource.BuildFinished += (sender, e) => BuildFinished?.Invoke(sender, e);
        _replayEventSource.ProjectStarted += (sender, e) => ProjectStarted?.Invoke(sender, e);
        _replayEventSource.ProjectFinished += (sender, e) => ProjectFinished?.Invoke(sender, e);
        _replayEventSource.TargetStarted += (sender, e) => TargetStarted?.Invoke(sender, e);
        _replayEventSource.TargetFinished += (sender, e) => TargetFinished?.Invoke(sender, e);
        _replayEventSource.TaskStarted += (sender, e) => TaskStarted?.Invoke(sender, e);
        _replayEventSource.TaskFinished += (sender, e) => TaskFinished?.Invoke(sender, e);
        _replayEventSource.CustomEventRaised += (sender, e) => CustomEventRaised?.Invoke(sender, e);
        _replayEventSource.StatusEventRaised += (sender, e) => StatusEventRaised?.Invoke(sender, e);
        _replayEventSource.AnyEventRaised += (sender, e) => AnyEventRaised?.Invoke(sender, e);
    }

    #endregion
}
