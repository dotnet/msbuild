// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal class BuildEventsProcessor(BuildCheckCentralContext buildCheckCentralContext)
{
    /// <summary>
    /// Represents a task currently being executed.
    /// </summary>
    /// <remarks>
    /// <see cref="TaskParameters"/> is stored in its own field typed as a mutable dictionary because <see cref="CheckData"/>
    /// is immutable.
    /// </remarks>
    private struct ExecutingTaskData
    {
        public TaskInvocationCheckData CheckData;
        public Dictionary<string, TaskInvocationCheckData.TaskParameter> TaskParameters;
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
    private Dictionary<string, (string EnvVarValue, IMSBuildElementLocation Location)> _evaluatedEnvironmentVariables = new();

    /// <summary>
    /// Keeps track of in-flight tasks. Keyed by task ID as passed in <see cref="BuildEventContext.TaskId"/>.
    /// </summary>
    private readonly Dictionary<TaskKey, ExecutingTaskData> _tasksBeingExecuted = [];

    internal static Dictionary<string, string> ExtractPropertiesLookup(ProjectEvaluationFinishedEventArgs evaluationFinishedEventArgs)
    {
        Dictionary<string, string> propertiesLookup = new Dictionary<string, string>();
        Internal.Utilities.EnumerateProperties(evaluationFinishedEventArgs.Properties, propertiesLookup,
            static (dict, kvp) => dict.Add(kvp.Key, kvp.Value));

        return propertiesLookup;
    }

    // This requires MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION set to 1
    internal void ProcessEvaluationFinishedEventArgs(
        ICheckContext checkContext,
        ProjectEvaluationFinishedEventArgs evaluationFinishedEventArgs,
        Dictionary<string, string>? propertiesLookup)
    {
        if (_buildCheckCentralContext.HasEvaluatedPropertiesActions)
        {
            propertiesLookup ??= ExtractPropertiesLookup(evaluationFinishedEventArgs);

            EvaluatedPropertiesCheckData checkData =
                new(evaluationFinishedEventArgs.ProjectFile!,
                    evaluationFinishedEventArgs.BuildEventContext?.ProjectInstanceId,
                    propertiesLookup!);

            _buildCheckCentralContext.RunEvaluatedPropertiesActions(checkData, checkContext, ReportResult);
        }

        if (_buildCheckCentralContext.HasParsedItemsActions)
        {
            ProjectRootElement xml = ProjectRootElement.OpenProjectOrSolution(
                evaluationFinishedEventArgs.ProjectFile!, /*unused*/
                null, /*unused*/null, _cache, false /*Not explicitly loaded - unused*/);

            ParsedItemsCheckData itemsCheckData = new(
                evaluationFinishedEventArgs.ProjectFile!,
                evaluationFinishedEventArgs.BuildEventContext?.ProjectInstanceId,
                new ItemsHolder(xml.Items, xml.ItemGroups));

            _buildCheckCentralContext.RunParsedItemsActions(itemsCheckData, checkContext, ReportResult);
        }
    }

    /// <summary>
    /// The method collects events associated with the used environment variables in projects.
    /// </summary>
    internal void ProcessEnvironmentVariableReadEventArgs(ICheckContext checkContext, string envVarName, string envVarValue, string file, int line, int column)
    {
        if (!_evaluatedEnvironmentVariables.ContainsKey(envVarName))
        {
            _evaluatedEnvironmentVariables.Add(envVarName, (envVarValue, ElementLocation.Create(file, line, column)));

            EnvironmentVariableCheckData checkData =
               new(file,
                   checkContext.BuildEventContext?.ProjectInstanceId,
                   _evaluatedEnvironmentVariables);

            _buildCheckCentralContext.RunEnvironmentVariableActions(checkData, checkContext, ReportResult);
        }
    }

    internal void ProcessBuildDone(ICheckContext checkContext)
    {
        if (!_buildCheckCentralContext.HasBuildFinishedActions)
        {
            // No analyzer is interested in the event -> nothing to do.
            return;
        }

        _buildCheckCentralContext.RunBuildFinishedActions(new BuildFinishedCheckData(), checkContext, ReportResult);
    }

    internal void ProcessTaskStartedEventArgs(
        ICheckContext checkContext,
        TaskStartedEventArgs taskStartedEventArgs)
    {
        if (!_buildCheckCentralContext.HasTaskInvocationActions)
        {
            // No check is interested in task invocation actions -> nothing to do.
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
            Dictionary<string, TaskInvocationCheckData.TaskParameter> taskParameters = new();

            ExecutingTaskData taskData = new()
            {
                TaskParameters = taskParameters,
                CheckData = new(
                    projectFilePath: taskStartedEventArgs.ProjectFile!,
                    projectConfigurationId: taskStartedEventArgs.BuildEventContext.ProjectInstanceId,
                    taskInvocationLocation: invocationLocation,
                    taskName: taskStartedEventArgs.TaskName,
                    taskAssemblyLocation: taskStartedEventArgs.TaskAssemblyLocation,
                    parameters: taskParameters),
            };

            _tasksBeingExecuted.Add(new TaskKey(taskStartedEventArgs.BuildEventContext), taskData);
        }
    }

    internal void ProcessTaskFinishedEventArgs(
        ICheckContext checkContext,
        TaskFinishedEventArgs taskFinishedEventArgs)
    {
        if (!_buildCheckCentralContext.HasTaskInvocationActions)
        {
            // No check is interested in task invocation actions -> nothing to do.
            return;
        }

        if (taskFinishedEventArgs?.BuildEventContext is not null)
        {
            TaskKey taskKey = new TaskKey(taskFinishedEventArgs.BuildEventContext);
            if (_tasksBeingExecuted.TryGetValue(taskKey, out ExecutingTaskData taskData))
            {
                // All task parameters have been recorded by now so remove the task from the dictionary and fire the registered build check actions.
                _tasksBeingExecuted.Remove(taskKey);
                _buildCheckCentralContext.RunTaskInvocationActions(taskData.CheckData, checkContext, ReportResult);
            }
        }
    }

    internal void ProcessTaskParameterEventArgs(
        ICheckContext checkContext,
        TaskParameterEventArgs taskParameterEventArgs)
    {
        if (!_buildCheckCentralContext.HasTaskInvocationActions)
        {
            // No check is interested in task invocation actions -> nothing to do.
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

            taskData.TaskParameters[parameterName] = new TaskInvocationCheckData.TaskParameter(parameterValue, isOutput);
        }
    }

    public void ProcessPropertyRead(PropertyReadData propertyReadData, CheckLoggingContext checkContext)
        => _buildCheckCentralContext.RunPropertyReadActions(
                propertyReadData,
                checkContext,
                ReportResult);

    public void ProcessPropertyWrite(PropertyWriteData propertyWriteData, CheckLoggingContext checkContext)
        => _buildCheckCentralContext.RunPropertyWriteActions(
                propertyWriteData,
                checkContext,
                ReportResult);

    public void ProcessProjectDone(ICheckContext checkContext, string projectFullPath)
        => _buildCheckCentralContext.RunProjectProcessingDoneActions(
                new ProjectRequestProcessingDoneData(projectFullPath, checkContext.BuildEventContext.ProjectInstanceId),
                checkContext,
                ReportResult);

    private static void ReportResult(
        CheckWrapper checkWrapper,
        ICheckContext checkContext,
        CheckConfigurationEffective[] configPerRule,
        BuildCheckResult result)
    {
        if (!checkWrapper.Check.SupportedRules.Contains(result.CheckRule))
        {
            checkContext.DispatchAsErrorFromText(null, null, null,
                BuildEventFileInfo.Empty,
                $"The check '{checkWrapper.Check.FriendlyName}' reported a result for a rule '{result.CheckRule.Id}' that it does not support.");
            return;
        }

        CheckConfigurationEffective config = configPerRule.Length == 1
            ? configPerRule[0]
            : configPerRule.First(r =>
                r.RuleId.Equals(result.CheckRule.Id, StringComparison.CurrentCultureIgnoreCase));

        if (!config.IsEnabled)
        {
            return;
        }

        BuildEventArgs eventArgs = result.ToEventArgs(config.Severity);

        eventArgs.BuildEventContext = checkContext.BuildEventContext;

        checkContext.DispatchBuildEvent(eventArgs);
    }
}
