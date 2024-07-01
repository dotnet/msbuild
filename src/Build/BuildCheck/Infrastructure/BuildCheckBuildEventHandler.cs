// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
using Microsoft.Build.Experimental.BuildCheck.Utilities;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal class BuildCheckBuildEventHandler
{
    private readonly IBuildCheckManager _buildCheckManager;
    private readonly IAnalysisContextFactory _analyzerContextFactory;

    private readonly Dictionary<Type, Action<BuildEventArgs>> _eventHandlers;

    internal BuildCheckBuildEventHandler(
        IAnalysisContextFactory analyzerContextFactory,
        IBuildCheckManager buildCheckManager)
    {
        _buildCheckManager = buildCheckManager;
        _analyzerContextFactory = analyzerContextFactory;

        _eventHandlers = new()
        {
            { typeof(ProjectEvaluationFinishedEventArgs), (BuildEventArgs e) => HandleProjectEvaluationFinishedEvent((ProjectEvaluationFinishedEventArgs)e) },
            { typeof(ProjectEvaluationStartedEventArgs), (BuildEventArgs e) => HandleProjectEvaluationStartedEvent((ProjectEvaluationStartedEventArgs)e) },
            { typeof(ProjectStartedEventArgs), (BuildEventArgs e) => _buildCheckManager.StartProjectRequest(BuildCheckDataSource.EventArgs, e.BuildEventContext!) },
            { typeof(ProjectFinishedEventArgs), (BuildEventArgs e) => _buildCheckManager.EndProjectRequest(BuildCheckDataSource.EventArgs, e.BuildEventContext!) },
            { typeof(BuildCheckTracingEventArgs), (BuildEventArgs e) => HandleBuildCheckTracingEvent((BuildCheckTracingEventArgs)e) },
            { typeof(BuildCheckAcquisitionEventArgs), (BuildEventArgs e) => HandleBuildCheckAcquisitionEvent((BuildCheckAcquisitionEventArgs)e) },
            { typeof(TaskStartedEventArgs), (BuildEventArgs e) => HandleTaskStartedEvent((TaskStartedEventArgs)e) },
            { typeof(TaskFinishedEventArgs), (BuildEventArgs e) => HandleTaskFinishedEvent((TaskFinishedEventArgs)e) },
            { typeof(TaskParameterEventArgs), (BuildEventArgs e) => HandleTaskParameterEvent((TaskParameterEventArgs)e) },
            { typeof(BuildFinishedEventArgs), (BuildEventArgs e) => HandleBuildFinishedEvent((BuildFinishedEventArgs)e) },
        };
    }

    public void HandleBuildEvent(BuildEventArgs e)
    {
        if (_eventHandlers.TryGetValue(e.GetType(), out Action<BuildEventArgs>? handler))
        {
            handler(e);
        }
    }

    private void HandleProjectEvaluationFinishedEvent(ProjectEvaluationFinishedEventArgs eventArgs)
    {
        if (!IsMetaProjFile(eventArgs.ProjectFile))
        {
            _buildCheckManager.ProcessEvaluationFinishedEventArgs(
                _analyzerContextFactory.CreateAnalysisContext(eventArgs.BuildEventContext!),
                eventArgs);

            _buildCheckManager.EndProjectEvaluation(BuildCheckDataSource.EventArgs, eventArgs.BuildEventContext!);
        }
    }

    private void HandleProjectEvaluationStartedEvent(ProjectEvaluationStartedEventArgs eventArgs)
    {
        if (!IsMetaProjFile(eventArgs.ProjectFile))
        {
            _buildCheckManager.StartProjectEvaluation(
                BuildCheckDataSource.EventArgs,
                _analyzerContextFactory.CreateAnalysisContext(eventArgs.BuildEventContext!),
                eventArgs.ProjectFile!);
        }
    }

    private void HandleBuildCheckTracingEvent(BuildCheckTracingEventArgs eventArgs)
    {
        if (!eventArgs.IsAggregatedGlobalReport)
        {
            _stats.Merge(eventArgs.TracingData, (span1, span2) => span1 + span2);
        }
    }

    private void HandleTaskStartedEvent(TaskStartedEventArgs eventArgs)
        => _buildCheckManager.ProcessTaskStartedEventArgs(
                _analyzerContextFactory.CreateAnalysisContext(eventArgs.BuildEventContext!),
                eventArgs);

    private void HandleTaskFinishedEvent(TaskFinishedEventArgs eventArgs)
        => _buildCheckManager.ProcessTaskFinishedEventArgs(
                _analyzerContextFactory.CreateAnalysisContext(eventArgs.BuildEventContext!),
                eventArgs);

    private void HandleTaskParameterEvent(TaskParameterEventArgs eventArgs)
        => _buildCheckManager.ProcessTaskParameterEventArgs(
                _analyzerContextFactory.CreateAnalysisContext(eventArgs.BuildEventContext!),
                eventArgs);

    private void HandleBuildCheckAcquisitionEvent(BuildCheckAcquisitionEventArgs eventArgs)
        => _buildCheckManager.ProcessAnalyzerAcquisition(
                eventArgs.ToAnalyzerAcquisitionData(),
                _analyzerContextFactory.CreateAnalysisContext(GetBuildEventContext(eventArgs)));

    private bool IsMetaProjFile(string? projectFile) => projectFile?.EndsWith(".metaproj", StringComparison.OrdinalIgnoreCase) == true;

    private readonly Dictionary<string, TimeSpan> _stats = new Dictionary<string, TimeSpan>();

    private void HandleBuildFinishedEvent(BuildFinishedEventArgs eventArgs)
    {
        _stats.Merge(_buildCheckManager.CreateAnalyzerTracingStats(), (span1, span2) => span1 + span2);

        LogAnalyzerStats(_analyzerContextFactory.CreateAnalysisContext(GetBuildEventContext(eventArgs)));
    }

    private void LogAnalyzerStats(IAnalysisContext analysisContext)
    {
        Dictionary<string, TimeSpan> infraStats = new Dictionary<string, TimeSpan>();
        Dictionary<string, TimeSpan> analyzerStats = new Dictionary<string, TimeSpan>();

        foreach (var stat in _stats)
        {
            if (stat.Key.StartsWith(BuildCheckConstants.infraStatPrefix))
            {
                string newKey = stat.Key.Substring(BuildCheckConstants.infraStatPrefix.Length);
                infraStats[newKey] = stat.Value;
            }
            else
            {
                analyzerStats[stat.Key] = stat.Value;
            }
        }

        BuildCheckTracingEventArgs statEvent = new BuildCheckTracingEventArgs(_stats, true)
        { BuildEventContext = analysisContext.BuildEventContext };

        analysisContext.DispatchBuildEvent(statEvent);

        analysisContext.DispatchAsCommentFromText(MessageImportance.Low, $"BuildCheck run times{Environment.NewLine}");
        string infraData = BuildCsvString("Infrastructure run times", infraStats);
        analysisContext.DispatchAsCommentFromText(MessageImportance.Low, infraData);
        string analyzerData = BuildCsvString("Analyzer run times", analyzerStats);
        analysisContext.DispatchAsCommentFromText(MessageImportance.Low, analyzerData);
    }

    private string BuildCsvString(string title, Dictionary<string, TimeSpan> rowData)
        => title + Environment.NewLine + String.Join(Environment.NewLine, rowData.Select(a => $"{a.Key},{a.Value}")) + Environment.NewLine;

    private BuildEventContext GetBuildEventContext(BuildEventArgs e) => e.BuildEventContext
        ?? new BuildEventContext(
                BuildEventContext.InvalidNodeId,
                BuildEventContext.InvalidTargetId,
                BuildEventContext.InvalidProjectContextId,
                BuildEventContext.InvalidTaskId);
}
