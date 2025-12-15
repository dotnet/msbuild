// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal class BuildCheckBuildEventHandler
{
    private readonly IBuildCheckManager _buildCheckManager;
    private readonly ICheckContextFactory _checkContextFactory;

    private Dictionary<Type, Action<BuildEventArgs>> _eventHandlers;
    private readonly Dictionary<Type, Action<BuildEventArgs>> _eventHandlersFull;
    private readonly Dictionary<Type, Action<BuildEventArgs>> _eventHandlersRestore;

    internal BuildCheckBuildEventHandler(
        ICheckContextFactory checkContextFactory,
        IBuildCheckManager buildCheckManager)
    {
        _buildCheckManager = buildCheckManager;
        _checkContextFactory = checkContextFactory;

        _eventHandlersFull = new()
        {
            { typeof(BuildSubmissionStartedEventArgs), (e) => HandleBuildSubmissionStartedEvent((BuildSubmissionStartedEventArgs)e) },
            { typeof(ProjectEvaluationFinishedEventArgs), (e) => HandleProjectEvaluationFinishedEvent((ProjectEvaluationFinishedEventArgs)e) },
            { typeof(ProjectEvaluationStartedEventArgs), (e) => HandleProjectEvaluationStartedEvent((ProjectEvaluationStartedEventArgs)e) },
            { typeof(EnvironmentVariableReadEventArgs), (e) => HandleEnvironmentVariableReadEvent((EnvironmentVariableReadEventArgs)e) },
            { typeof(ProjectStartedEventArgs), (e) => HandleProjectStartedRequest((ProjectStartedEventArgs)e) },
            { typeof(ProjectFinishedEventArgs), (e) => HandleProjectFinishedRequest((ProjectFinishedEventArgs)e) },
            { typeof(BuildCheckTracingEventArgs), (e) => HandleBuildCheckTracingEvent((BuildCheckTracingEventArgs)e) },
            { typeof(BuildCheckAcquisitionEventArgs), (e) => HandleBuildCheckAcquisitionEvent((BuildCheckAcquisitionEventArgs)e) },
            { typeof(TaskStartedEventArgs), (e) => HandleTaskStartedEvent((TaskStartedEventArgs)e) },
            { typeof(TaskFinishedEventArgs), (e) => HandleTaskFinishedEvent((TaskFinishedEventArgs)e) },
            { typeof(TaskParameterEventArgs), (e) => HandleTaskParameterEvent((TaskParameterEventArgs)e) },
            { typeof(BuildFinishedEventArgs), (e) => HandleBuildFinishedEvent((BuildFinishedEventArgs)e) },
            { typeof(ProjectImportedEventArgs), (e) => HandleProjectImportedEvent((ProjectImportedEventArgs)e) },
        };

        // During restore we'll wait only for restore to be done.
        _eventHandlersRestore = new()
        {
            { typeof(BuildSubmissionStartedEventArgs), (e) => HandleBuildSubmissionStartedEvent((BuildSubmissionStartedEventArgs)e) },
        };

        _eventHandlers = _eventHandlersFull;
    }

    public void HandleBuildEvent(BuildEventArgs e)
    {
        if (_eventHandlers.TryGetValue(e.GetType(), out Action<BuildEventArgs>? handler))
        {
            handler(e);
        }
    }

    private void HandleBuildSubmissionStartedEvent(BuildSubmissionStartedEventArgs eventArgs)
    {
        eventArgs.GlobalProperties.TryGetValue(MSBuildConstants.MSBuildIsRestoring, out string? restoreProperty);
        bool isRestoring = restoreProperty is not null && Convert.ToBoolean(restoreProperty);

        _eventHandlers = isRestoring ? _eventHandlersRestore : _eventHandlersFull;
    }

    private void HandleProjectEvaluationFinishedEvent(ProjectEvaluationFinishedEventArgs eventArgs)
    {
        if (!IsMetaProjFile(eventArgs.ProjectFile))
        {
            _buildCheckManager.ProcessEvaluationFinishedEventArgs(
                _checkContextFactory.CreateCheckContext(eventArgs.BuildEventContext!),
                eventArgs);

            _buildCheckManager.EndProjectEvaluation(eventArgs.BuildEventContext!);
        }
    }

    private void HandleProjectEvaluationStartedEvent(ProjectEvaluationStartedEventArgs eventArgs)
    {
        if (!IsMetaProjFile(eventArgs.ProjectFile))
        {
            var checkContext = _checkContextFactory.CreateCheckContext(eventArgs.BuildEventContext!);
            _buildCheckManager.ProjectFirstEncountered(
                BuildCheckDataSource.EventArgs,
                checkContext,
                eventArgs.ProjectFile!);

            _buildCheckManager.ProcessProjectEvaluationStarted(
                BuildCheckDataSource.EventArgs,
                checkContext,
                eventArgs.ProjectFile!);
        }
    }

    private void HandleProjectStartedRequest(ProjectStartedEventArgs eventArgs)
        => _buildCheckManager.StartProjectRequest(
            _checkContextFactory.CreateCheckContext(eventArgs.BuildEventContext!),
            eventArgs!.ProjectFile!);

    private void HandleProjectFinishedRequest(ProjectFinishedEventArgs eventArgs)
        => _buildCheckManager.EndProjectRequest(
                _checkContextFactory.CreateCheckContext(eventArgs.BuildEventContext!),
                eventArgs!.ProjectFile!);

    private void HandleBuildCheckTracingEvent(BuildCheckTracingEventArgs eventArgs)
    {
        if (!eventArgs.IsAggregatedGlobalReport)
        {
            _tracingData.MergeIn(eventArgs.TracingData);
        }
    }

    private void HandleTaskStartedEvent(TaskStartedEventArgs eventArgs)
        => _buildCheckManager.ProcessTaskStartedEventArgs(
                _checkContextFactory.CreateCheckContext(eventArgs.BuildEventContext!),
                eventArgs);

    private void HandleTaskFinishedEvent(TaskFinishedEventArgs eventArgs)
        => _buildCheckManager.ProcessTaskFinishedEventArgs(
                _checkContextFactory.CreateCheckContext(eventArgs.BuildEventContext!),
                eventArgs);

    private void HandleTaskParameterEvent(TaskParameterEventArgs eventArgs)
        => _buildCheckManager.ProcessTaskParameterEventArgs(
                _checkContextFactory.CreateCheckContext(eventArgs.BuildEventContext!),
                eventArgs);

    private void HandleBuildCheckAcquisitionEvent(BuildCheckAcquisitionEventArgs eventArgs)
        => _buildCheckManager.ProcessCheckAcquisition(
                eventArgs.ToCheckAcquisitionData(),
                _checkContextFactory.CreateCheckContext(GetBuildEventContext(eventArgs)));

    private void HandleEnvironmentVariableReadEvent(EnvironmentVariableReadEventArgs eventArgs)
        => _buildCheckManager.ProcessEnvironmentVariableReadEventArgs(
                _checkContextFactory.CreateCheckContext(GetBuildEventContext(eventArgs)),
                eventArgs);

    private void HandleProjectImportedEvent(ProjectImportedEventArgs eventArgs)
        => _buildCheckManager.ProcessProjectImportedEventArgs(
                _checkContextFactory.CreateCheckContext(GetBuildEventContext(eventArgs)),
                eventArgs);

    private bool IsMetaProjFile(string? projectFile) => projectFile?.EndsWith(".metaproj", StringComparison.OrdinalIgnoreCase) == true;

    private readonly BuildCheckTracingData _tracingData = new BuildCheckTracingData();

    private void HandleBuildFinishedEvent(BuildFinishedEventArgs eventArgs)
    {
        _buildCheckManager.ProcessBuildFinished(_checkContextFactory.CreateCheckContext(eventArgs.BuildEventContext!));

        _tracingData.MergeIn(_buildCheckManager.CreateCheckTracingStats());

        LogCheckStats(_checkContextFactory.CreateCheckContext(GetBuildEventContext(eventArgs)));
    }

    private void LogCheckStats(ICheckContext checkContext)
    {
        Dictionary<string, TimeSpan> infraStats = _tracingData.InfrastructureTracingData;
        // Stats are per rule, while runtime is per check - and check can have multiple rules.
        // In case of multi-rule check, the runtime stats are duplicated for each rule.
        Dictionary<string, TimeSpan> checkStats = _tracingData.ExtractCheckStats();

        BuildCheckTracingEventArgs statEvent = new BuildCheckTracingEventArgs(_tracingData, true)
        { BuildEventContext = checkContext.BuildEventContext };

        checkContext.DispatchBuildEvent(statEvent);

        checkContext.DispatchAsCommentFromText(MessageImportance.Low, $"BuildCheck run times{Environment.NewLine}");
        string infraData = BuildCsvString("Infrastructure run times", infraStats);
        checkContext.DispatchAsCommentFromText(MessageImportance.Low, infraData);
        string checkData = BuildCsvString("Checks run times", checkStats);
        checkContext.DispatchAsCommentFromText(MessageImportance.Low, checkData);
        checkContext.DispatchTelemetry(_tracingData);
    }

    private string BuildCsvString(string title, Dictionary<string, TimeSpan> rowData)
        => title + Environment.NewLine + String.Join(Environment.NewLine, rowData.Select(a => $"{a.Key},{a.Value}")) + Environment.NewLine;

    private BuildEventContext GetBuildEventContext(BuildEventArgs e) => e.BuildEventContext
        ?? BuildEventContext.Invalid;
}
