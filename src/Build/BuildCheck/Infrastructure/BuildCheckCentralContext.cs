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
/// A manager of the runs of the analyzers - deciding based on configuration of what to run and what to postfilter.
/// </summary>
internal sealed class BuildCheckCentralContext
{
    private readonly ConfigurationProvider _configurationProvider;

    internal BuildCheckCentralContext(ConfigurationProvider configurationProvider)
        => _configurationProvider = configurationProvider;

    private record CallbackRegistry(
        List<(BuildAnalyzerWrapper, Action<BuildCheckDataContext<EvaluatedPropertiesAnalysisData>>)> EvaluatedPropertiesActions,
        List<(BuildAnalyzerWrapper, Action<BuildCheckDataContext<ParsedItemsAnalysisData>>)> ParsedItemsActions,
        List<(BuildAnalyzerWrapper, Action<BuildCheckDataContext<TaskInvocationAnalysisData>>)> TaskInvocationActions)
    {
        public CallbackRegistry() : this([], [], []) { }
    }

    // In a future we can have callbacks per project as well
    private readonly CallbackRegistry _globalCallbacks = new();

    // This we can potentially use to subscribe for receiving evaluated props in the
    //  build event args. However - this needs to be done early on, when analyzers might not be known yet
    internal bool HasEvaluatedPropertiesActions => _globalCallbacks.EvaluatedPropertiesActions.Count > 0;
    internal bool HasParsedItemsActions => _globalCallbacks.ParsedItemsActions.Count > 0;
    internal bool HasTaskInvocationActions => _globalCallbacks.TaskInvocationActions.Count > 0;

    internal void RegisterEvaluatedPropertiesAction(BuildAnalyzerWrapper analyzer, Action<BuildCheckDataContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction)
        // Here we might want to communicate to node that props need to be sent.
        //  (it was being communicated via MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION)
        => RegisterAction(analyzer, evaluatedPropertiesAction, _globalCallbacks.EvaluatedPropertiesActions);

    internal void RegisterParsedItemsAction(BuildAnalyzerWrapper analyzer, Action<BuildCheckDataContext<ParsedItemsAnalysisData>> parsedItemsAction)
        => RegisterAction(analyzer, parsedItemsAction, _globalCallbacks.ParsedItemsActions);

    internal void RegisterTaskInvocationAction(BuildAnalyzerWrapper analyzer, Action<BuildCheckDataContext<TaskInvocationAnalysisData>> taskInvocationAction)
        => RegisterAction(analyzer, taskInvocationAction, _globalCallbacks.TaskInvocationActions);

    private void RegisterAction<T>(
        BuildAnalyzerWrapper wrappedAnalyzer,
        Action<BuildCheckDataContext<T>> handler,
        List<(BuildAnalyzerWrapper, Action<BuildCheckDataContext<T>>)> handlersRegistry)
        where T : AnalysisData
    {
        void WrappedHandler(BuildCheckDataContext<T> context)
        {
            using var _ = wrappedAnalyzer.StartSpan();
            handler(context);
        }

        lock (handlersRegistry)
        {
            handlersRegistry.Add((wrappedAnalyzer, WrappedHandler));
        }
    }

    internal void DeregisterAnalyzer(BuildAnalyzerWrapper analyzer)
    {
        _globalCallbacks.EvaluatedPropertiesActions.RemoveAll(a => a.Item1 == analyzer);
        _globalCallbacks.ParsedItemsActions.RemoveAll(a => a.Item1 == analyzer);
        _globalCallbacks.TaskInvocationActions.RemoveAll(a => a.Item1 == analyzer);
    }

    internal void RunEvaluatedPropertiesActions(
        EvaluatedPropertiesAnalysisData evaluatedPropertiesAnalysisData,
        IAnalysisContext analysisContext,
        Action<BuildAnalyzerWrapper, IAnalysisContext, BuildAnalyzerConfigurationInternal[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.EvaluatedPropertiesActions, evaluatedPropertiesAnalysisData,
            analysisContext, resultHandler);

    internal void RunParsedItemsActions(
        ParsedItemsAnalysisData parsedItemsAnalysisData,
        IAnalysisContext analysisContext,
        Action<BuildAnalyzerWrapper, IAnalysisContext, BuildAnalyzerConfigurationInternal[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.ParsedItemsActions, parsedItemsAnalysisData,
            analysisContext, resultHandler);

    internal void RunTaskInvocationActions(
        TaskInvocationAnalysisData taskInvocationAnalysisData,
        IAnalysisContext analysisContext,
        Action<BuildAnalyzerWrapper, IAnalysisContext, BuildAnalyzerConfigurationInternal[], BuildCheckResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.TaskInvocationActions, taskInvocationAnalysisData,
            analysisContext, resultHandler);

    private void RunRegisteredActions<T>(
        List<(BuildAnalyzerWrapper, Action<BuildCheckDataContext<T>>)> registeredCallbacks,
        T analysisData,
        IAnalysisContext analysisContext,
        Action<BuildAnalyzerWrapper, IAnalysisContext, BuildAnalyzerConfigurationInternal[], BuildCheckResult> resultHandler)
    where T : AnalysisData
    {
        string projectFullPath = analysisData.ProjectFilePath;

        // Alternatively we might want to actually do this all in serial, but asynchronously (blocking queue)
        Parallel.ForEach(
            registeredCallbacks,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            /* (BuildAnalyzerWrapper2, Action<BuildAnalysisContext<T>>) */
            analyzerCallback =>
            {
                // Tracing - https://github.com/dotnet/msbuild/issues/9629 - we might want to account this entire block
                //  to the relevant analyzer (with only the currently accounted part as being the 'core-execution' subspan)

                BuildAnalyzerConfigurationInternal? commonConfig = analyzerCallback.Item1.CommonConfig;
                BuildAnalyzerConfigurationInternal[] configPerRule;

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
                            analyzerCallback.Item1.BuildAnalyzer);
                    if (configPerRule.All(c => !c.IsEnabled))
                    {
                        return;
                    }
                }

                // Here we might want to check the configPerRule[0].EvaluationAnalysisScope - if the input data supports that
                // The decision and implementation depends on the outcome of the investigation tracked in:
                // https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=57851137

                BuildCheckDataContext<T> context = new BuildCheckDataContext<T>(
                    analyzerCallback.Item1,
                    analysisContext,
                    configPerRule,
                    resultHandler,
                    analysisData);

                analyzerCallback.Item2(context);
            });
    }
}
