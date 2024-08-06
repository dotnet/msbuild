// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

/// <summary>
/// A manager of the runs of the checks - deciding based on configuration of what to run and what to postfilter.
/// </summary>
internal sealed class BuildCheckCentralContext
{
    private readonly ConfigurationProvider _configurationProvider;

    internal BuildCheckCentralContext(ConfigurationProvider configurationProvider)
        => _configurationProvider = configurationProvider;

    private record CallbackRegistry(
        List<(BuildExecutionCheckWrapper, Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>>)> EvaluatedPropertiesActions,
        List<(BuildExecutionCheckWrapper, Action<BuildCheckDataContext<ParsedItemsCheckData>>)> ParsedItemsActions,
        List<(BuildExecutionCheckWrapper, Action<BuildCheckDataContext<TaskInvocationCheckData>>)> TaskInvocationActions,
        List<(BuildExecutionCheckWrapper, Action<BuildCheckDataContext<PropertyReadData>>)> PropertyReadActions,
        List<(BuildExecutionCheckWrapper, Action<BuildCheckDataContext<PropertyWriteData>>)> PropertyWriteActions,
        List<(BuildExecutionCheckWrapper, Action<BuildCheckDataContext<ProjectProcessingDoneData>>)> ProjectProcessingDoneActions)
    {
        public CallbackRegistry() : this([], [], [], [], [], []) { }

        internal void DeregisterCheck(BuildExecutionCheckWrapper check)
        {
            EvaluatedPropertiesActions.RemoveAll(a => a.Item1 == check);
            ParsedItemsActions.RemoveAll(a => a.Item1 == check);
            PropertyReadActions.RemoveAll(a => a.Item1 == check);
            PropertyWriteActions.RemoveAll(a => a.Item1 == check);
            ProjectProcessingDoneActions.RemoveAll(a => a.Item1 == check);
        }
    }

    // In a future we can have callbacks per project as well
    private readonly CallbackRegistry _globalCallbacks = new();

    // This we can potentially use to subscribe for receiving evaluated props in the
    //  build event args. However - this needs to be done early on, when checks might not be known yet
    internal bool HasEvaluatedPropertiesActions => _globalCallbacks.EvaluatedPropertiesActions.Count > 0;

    internal bool HasParsedItemsActions => _globalCallbacks.ParsedItemsActions.Count > 0;

    internal bool HasTaskInvocationActions => _globalCallbacks.TaskInvocationActions.Count > 0;
    internal bool HasPropertyReadActions => _globalCallbacks.PropertyReadActions.Count > 0;
    internal bool HasPropertyWriteActions => _globalCallbacks.PropertyWriteActions.Count > 0;

    internal void RegisterEvaluatedPropertiesAction(BuildExecutionCheckWrapper check, Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>> evaluatedPropertiesAction)
        // Here we might want to communicate to node that props need to be sent.
        //  (it was being communicated via MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION)
        => RegisterAction(check, evaluatedPropertiesAction, _globalCallbacks.EvaluatedPropertiesActions);

    internal void RegisterParsedItemsAction(BuildExecutionCheckWrapper check, Action<BuildCheckDataContext<ParsedItemsCheckData>> parsedItemsAction)
        => RegisterAction(check, parsedItemsAction, _globalCallbacks.ParsedItemsActions);

    internal void RegisterTaskInvocationAction(BuildExecutionCheckWrapper check, Action<BuildCheckDataContext<TaskInvocationCheckData>> taskInvocationAction)
        => RegisterAction(check, taskInvocationAction, _globalCallbacks.TaskInvocationActions);

    internal void RegisterPropertyReadAction(BuildExecutionCheckWrapper check, Action<BuildCheckDataContext<PropertyReadData>> propertyReadAction)
        => RegisterAction(check, propertyReadAction, _globalCallbacks.PropertyReadActions);

    internal void RegisterPropertyWriteAction(BuildExecutionCheckWrapper check, Action<BuildCheckDataContext<PropertyWriteData>> propertyWriteAction)
        => RegisterAction(check, propertyWriteAction, _globalCallbacks.PropertyWriteActions);

    internal void RegisterProjectProcessingDoneAction(BuildExecutionCheckWrapper check, Action<BuildCheckDataContext<ProjectProcessingDoneData>> projectDoneAction)
        => RegisterAction(check, projectDoneAction, _globalCallbacks.ProjectProcessingDoneActions);

    private void RegisterAction<T>(
        BuildExecutionCheckWrapper wrappedCheck,
        Action<BuildCheckDataContext<T>> handler,
        List<(BuildExecutionCheckWrapper, Action<BuildCheckDataContext<T>>)> handlersRegistry)
        where T : CheckData
    {
        void WrappedHandler(BuildCheckDataContext<T> context)
        {
            using var _ = wrappedCheck.StartSpan();
            handler(context);
        }

        lock (handlersRegistry)
        {
            handlersRegistry.Add((wrappedCheck, WrappedHandler));
        }
    }

    internal void DeregisterCheck(BuildExecutionCheckWrapper check)
    {
        _globalCallbacks.DeregisterCheck(check);
    }

    internal void RunEvaluatedPropertiesActions(
        EvaluatedPropertiesCheckData evaluatedPropertiesCheckData,
        ICheckContext checkContext,
        Action<BuildExecutionCheckWrapper, ICheckContext, BuildExecutionCheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.EvaluatedPropertiesActions, evaluatedPropertiesCheckData,
            checkContext, resultHandler);

    internal void RunParsedItemsActions(
        ParsedItemsCheckData parsedItemsCheckData,
        ICheckContext checkContext,
        Action<BuildExecutionCheckWrapper, ICheckContext, BuildExecutionCheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.ParsedItemsActions, parsedItemsCheckData,
            checkContext, resultHandler);

    internal void RunTaskInvocationActions(
        TaskInvocationCheckData taskInvocationCheckData,
        ICheckContext checkContext,
        Action<BuildExecutionCheckWrapper, ICheckContext, BuildExecutionCheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.TaskInvocationActions, taskInvocationCheckData,
            checkContext, resultHandler);

    internal void RunPropertyReadActions(
        PropertyReadData propertyReadDataData,
        CheckLoggingContext checkContext,
        Action<BuildExecutionCheckWrapper, ICheckContext, BuildExecutionCheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.PropertyReadActions, propertyReadDataData,
            checkContext, resultHandler);

    internal void RunPropertyWriteActions(
        PropertyWriteData propertyWriteData,
        CheckLoggingContext checkContext,
        Action<BuildExecutionCheckWrapper, ICheckContext, BuildExecutionCheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.PropertyWriteActions, propertyWriteData,
            checkContext, resultHandler);

    internal void RunProjectProcessingDoneActions(
        ProjectProcessingDoneData projectProcessingDoneData,
        ICheckContext checkContext,
        Action<BuildExecutionCheckWrapper, ICheckContext, BuildExecutionCheckConfigurationEffective[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.ProjectProcessingDoneActions, projectProcessingDoneData,
            checkContext, resultHandler);

    private void RunRegisteredActions<T>(
        List<(BuildExecutionCheckWrapper, Action<BuildCheckDataContext<T>>)> registeredCallbacks,
        T checkData,
        ICheckContext checkContext,
        Action<BuildExecutionCheckWrapper, ICheckContext, BuildExecutionCheckConfigurationEffective[], BuildCheckResult> resultHandler)
    where T : CheckData
    {
        string projectFullPath = checkData.ProjectFilePath;

        foreach (var checkCallback in registeredCallbacks)
        {
            // Tracing - https://github.com/dotnet/msbuild/issues/9629 - we might want to account this entire block
            //  to the relevant check (with BuildCheckConfigurationEffectively the currently accounted part as being the 'core-execution' subspan)

            BuildExecutionCheckConfigurationEffective? commonConfig = checkCallback.Item1.CommonConfig;
            BuildExecutionCheckConfigurationEffective[] configPerRule;

            if (commonConfig != null)
            {
                if (!commonConfig.IsEnabled)
                {
                    return;
                }

                configPerRule = new[] { commonConfig };
            }
            else
            {
                configPerRule =
                    _configurationProvider.GetMergedConfigurations(projectFullPath,
                        checkCallback.Item1.BuildExecutionCheck);
                if (configPerRule.All(c => !c.IsEnabled))
                {
                    return;
                }
            }

            // Here we might want to check the configPerRule[0].EvaluationsCheckScope - if the input data supports that
            // The decision and implementation depends on the outcome of the investigation tracked in:
            // https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=57851137

            BuildCheckDataContext<T> context = new BuildCheckDataContext<T>(
                checkCallback.Item1,
                checkContext,
                configPerRule,
                resultHandler,
                checkData);

            checkCallback.Item2(context);
        }
    }
}
