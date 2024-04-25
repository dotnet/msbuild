// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Acquisition;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using static Microsoft.Build.BuildCheck.Infrastructure.BuildCheckManagerProvider;

namespace Microsoft.Build.BuildCheck.Infrastructure;

internal sealed class BuildCheckConnectorLogger : ILogger
{
    private readonly Dictionary<Type, Action<BuildEventArgs>> _eventHandlers;
    private readonly IBuildCheckManager _buildCheckManager;
    private readonly IBuildAnalysisLoggingContextFactory _loggingContextFactory;

    internal BuildCheckConnectorLogger(
        IBuildAnalysisLoggingContextFactory loggingContextFactory,
        IBuildCheckManager buildCheckManager)
    {
        _buildCheckManager = buildCheckManager;
        _loggingContextFactory = loggingContextFactory;
        _eventHandlers = GetBuildEventHandlers();
    }

    public LoggerVerbosity Verbosity { get; set; }

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
        eventSource.AnyEventRaised += EventSource_AnyEventRaised;
        eventSource.BuildFinished += EventSource_BuildFinished;

        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
        }
    }

    public void Shutdown()
    {
    }

    private void HandleProjectEvaluationFinishedEvent(ProjectEvaluationFinishedEventArgs eventArgs)
    {
        if (!IsMetaProjFile(eventArgs.ProjectFile))
        {
            _buildCheckManager.ProcessEvaluationFinishedEventArgs(
                _loggingContextFactory.CreateLoggingContext(eventArgs.BuildEventContext!),
                eventArgs);

            _buildCheckManager.EndProjectEvaluation(BuildCheckDataSource.EventArgs, eventArgs.BuildEventContext!);
        }
    }

    private void HandleProjectEvaluationStartedEvent(ProjectEvaluationStartedEventArgs eventArgs)
    {
        if (!IsMetaProjFile(eventArgs.ProjectFile))
        {
            _buildCheckManager.StartProjectEvaluation(BuildCheckDataSource.EventArgs, eventArgs.BuildEventContext!, eventArgs.ProjectFile!);
        }
    }

    private bool IsMetaProjFile(string? projectFile) => !string.IsNullOrEmpty(projectFile) && projectFile!.EndsWith(".metaproj", StringComparison.OrdinalIgnoreCase);

    private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
    {
        if (_eventHandlers.TryGetValue(e.GetType(), out Action<BuildEventArgs>? handler))
        {
            handler(e);
        }
    }

    private readonly Dictionary<string, TimeSpan> _stats = new Dictionary<string, TimeSpan>();

    private void EventSource_BuildFinished(object sender, BuildFinishedEventArgs e)
    {
        _stats.Merge(_buildCheckManager.CreateTracingStats(), (span1, span2) => span1 + span2);
        string msg = string.Join(Environment.NewLine, _stats.Select(a => a.Key + ": " + a.Value));

        BuildEventContext buildEventContext = e.BuildEventContext
            ?? new BuildEventContext(
                BuildEventContext.InvalidNodeId,
                BuildEventContext.InvalidTargetId,
                BuildEventContext.InvalidProjectContextId,
                BuildEventContext.InvalidTaskId);

        LoggingContext loggingContext = _loggingContextFactory.CreateLoggingContext(buildEventContext);

        // Tracing: https://github.com/dotnet/msbuild/issues/9629
        loggingContext.LogCommentFromText(MessageImportance.High, msg);
    }

    private Dictionary<Type, Action<BuildEventArgs>> GetBuildEventHandlers() => new()
    {
        { typeof(ProjectEvaluationFinishedEventArgs), (BuildEventArgs e) => HandleProjectEvaluationFinishedEvent((ProjectEvaluationFinishedEventArgs) e) },
        { typeof(ProjectEvaluationStartedEventArgs), (BuildEventArgs e) => HandleProjectEvaluationStartedEvent((ProjectEvaluationStartedEventArgs) e) },
        { typeof(ProjectStartedEventArgs), (BuildEventArgs e) => _buildCheckManager.StartProjectRequest(BuildCheckDataSource.EventArgs, e.BuildEventContext!) },
        { typeof(ProjectFinishedEventArgs), (BuildEventArgs e) => _buildCheckManager.EndProjectRequest(BuildCheckDataSource.EventArgs, e.BuildEventContext!) },
        { typeof(BuildCheckTracingEventArgs), (BuildEventArgs e) => _stats.Merge(((BuildCheckTracingEventArgs)e).TracingData, (span1, span2) => span1 + span2) },
        { typeof(BuildCheckAcquisitionEventArgs), (BuildEventArgs e) => _buildCheckManager.ProcessAnalyzerAcquisition(((BuildCheckAcquisitionEventArgs)e).ToAnalyzerAcquisitionData(), e.BuildEventContext!) },
    };
}
