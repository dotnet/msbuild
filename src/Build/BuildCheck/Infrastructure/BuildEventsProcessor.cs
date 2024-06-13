// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Components.Caching;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Analyzers;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal class BuildEventsProcessor(BuildCheckCentralContext buildCheckCentralContext)
{
    /// <summary>
    /// Represents a task currently being executed.
    /// </summary>
    /// <remarks>
    /// <see cref="TaskParameters"/> is stored in its own field typed as a mutable dictionary because <see cref="AnalysisData"/>
    /// is immutable.
    /// </remarks>
    private struct ExecutingTaskData
    {
        public TaskInvocationAnalysisData AnalysisData;
        public Dictionary<string, TaskInvocationAnalysisData.TaskParameter> TaskParameters;
    }

    /// <summary>
    /// Uniquely identifies a task.
    /// </summary>
    private record struct TaskKey(int ProjectContextId, int TargetId, int TaskId)
    {
        public TaskKey(BuildEventContext context)
            : this(context.ProjectContextId, context.TargetId, context.TaskId)
        { }
    }

    private readonly SimpleProjectRootElementCache _cache = new SimpleProjectRootElementCache();
    private readonly BuildCheckCentralContext _buildCheckCentralContext = buildCheckCentralContext;

    /// <summary>
    /// Keeps track of in-flight tasks. Keyed by task ID as passed in <see cref="BuildEventContext.TaskId"/>.
    /// </summary>
    private readonly Dictionary<TaskKey, ExecutingTaskData> _tasksBeingExecuted = [];

    // This requires MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION set to 1
    internal void ProcessEvaluationFinishedEventArgs(
        IAnalysisContext analysisContext,
        ProjectEvaluationFinishedEventArgs evaluationFinishedEventArgs)
    {
        Dictionary<string, string> propertiesLookup = new Dictionary<string, string>();
        Internal.Utilities.EnumerateProperties(evaluationFinishedEventArgs.Properties, propertiesLookup,
            static (dict, kvp) => dict.Add(kvp.Key, kvp.Value));

        EvaluatedPropertiesAnalysisData analysisData =
            new(evaluationFinishedEventArgs.ProjectFile!, propertiesLookup);

        _buildCheckCentralContext.RunEvaluatedPropertiesActions(analysisData, analysisContext, ReportResult);

        if (_buildCheckCentralContext.HasParsedItemsActions)
        {
            ProjectRootElement xml = ProjectRootElement.OpenProjectOrSolution(
                evaluationFinishedEventArgs.ProjectFile!, /*unused*/
                null, /*unused*/null, _cache, false /*Not explicitly loaded - unused*/);

            ParsedItemsAnalysisData itemsAnalysisData = new(evaluationFinishedEventArgs.ProjectFile!,
                new ItemsHolder(xml.Items, xml.ItemGroups));

            _buildCheckCentralContext.RunParsedItemsActions(itemsAnalysisData, analysisContext, ReportResult);
        }
    }

    internal void ProcessTaskStartedEventArgs(
        IAnalysisContext analysisContext,
        TaskStartedEventArgs taskStartedEventArgs)
    {
        if (!_buildCheckCentralContext.HasTaskInvocationActions)
        {
            // No analyzer is interested in task invocation actions -> nothing to do.
            return;
        }

        if (taskStartedEventArgs.BuildEventContext is not null)
        {
            ElementLocation invocationLocation = ElementLocation.Create(
                taskStartedEventArgs.TaskFile,
                taskStartedEventArgs.LineNumber,
                taskStartedEventArgs.ColumnNumber);

            // Add a new entry to _tasksBeingExecuted. TaskParameters are initialized empty and will be recorded
            // based on TaskParameterEventArgs we receive later.
            Dictionary<string, TaskInvocationAnalysisData.TaskParameter> taskParameters = new();

            ExecutingTaskData taskData = new()
            {
                TaskParameters = taskParameters,
                AnalysisData = new(
                    projectFilePath: taskStartedEventArgs.ProjectFile!,
                    taskInvocationLocation: invocationLocation,
                    taskName: taskStartedEventArgs.TaskName,
                    taskAssemblyLocation: taskStartedEventArgs.TaskAssemblyLocation,
                    parameters: taskParameters),
            };

            _tasksBeingExecuted.Add(new TaskKey(taskStartedEventArgs.BuildEventContext), taskData);
        }
    }

    internal void ProcessTaskFinishedEventArgs(
        IAnalysisContext analysisContext,
        TaskFinishedEventArgs taskFinishedEventArgs)
    {
        if (!_buildCheckCentralContext.HasTaskInvocationActions)
        {
            // No analyzer is interested in task invocation actions -> nothing to do.
            return;
        }

        if (taskFinishedEventArgs?.BuildEventContext is not null)
        {
            TaskKey taskKey = new TaskKey(taskFinishedEventArgs.BuildEventContext);
            if (_tasksBeingExecuted.TryGetValue(taskKey, out ExecutingTaskData taskData))
            {
                // All task parameters have been recorded by now so remove the task from the dictionary and fire the registered build check actions.
                _tasksBeingExecuted.Remove(taskKey);
                _buildCheckCentralContext.RunTaskInvocationActions(taskData.AnalysisData, analysisContext, ReportResult);
            }
        }
    }

    internal void ProcessTaskParameterEventArgs(
        IAnalysisContext analysisContext,
        TaskParameterEventArgs taskParameterEventArgs)
    {
        if (!_buildCheckCentralContext.HasTaskInvocationActions)
        {
            // No analyzer is interested in task invocation actions -> nothing to do.
            return;
        }

        bool isOutput;
        switch (taskParameterEventArgs.Kind)
        {
            case TaskParameterMessageKind.TaskInput: isOutput = false; break;
            case TaskParameterMessageKind.TaskOutput: isOutput = true; break;
            default: return;
        }

        if (taskParameterEventArgs.BuildEventContext is not null &&
            _tasksBeingExecuted.TryGetValue(new TaskKey(taskParameterEventArgs.BuildEventContext), out ExecutingTaskData taskData))
        {
            // Add the parameter name and value to the matching entry in _tasksBeingExecuted. Parameters come typed as IList
            // but it's more natural to pass them as scalar values so we unwrap one-element lists.
            string parameterName = taskParameterEventArgs.ParameterName;
            object? parameterValue = taskParameterEventArgs.Items?.Count switch
            {
                1 => taskParameterEventArgs.Items[0],
                _ => taskParameterEventArgs.Items,
            };

            taskData.TaskParameters[parameterName] = new TaskInvocationAnalysisData.TaskParameter(parameterValue, isOutput);
        }
    }

    private static void ReportResult(
        BuildAnalyzerWrapper analyzerWrapper,
        IAnalysisContext analysisContext,
        BuildAnalyzerConfigurationInternal[] configPerRule,
        BuildCheckResult result)
    {
        if (!analyzerWrapper.BuildAnalyzer.SupportedRules.Contains(result.BuildAnalyzerRule))
        {
            analysisContext.DispatchAsErrorFromText(null, null, null,
                BuildEventFileInfo.Empty,
                $"The analyzer '{analyzerWrapper.BuildAnalyzer.FriendlyName}' reported a result for a rule '{result.BuildAnalyzerRule.Id}' that it does not support.");
            return;
        }

        BuildAnalyzerConfigurationInternal config = configPerRule.Length == 1
            ? configPerRule[0]
            : configPerRule.First(r =>
                r.RuleId.Equals(result.BuildAnalyzerRule.Id, StringComparison.CurrentCultureIgnoreCase));

        if (!config.IsEnabled)
        {
            return;
        }

        BuildEventArgs eventArgs = result.ToEventArgs(config.Severity);

        // TODO: This is a workaround for https://github.com/dotnet/msbuild/issues/10176
        // eventArgs.BuildEventContext = loggingContext.BuildEventContext;
        eventArgs.BuildEventContext = BuildEventContext.Invalid;

        analysisContext.DispatchBuildEvent(eventArgs);
    }
}
