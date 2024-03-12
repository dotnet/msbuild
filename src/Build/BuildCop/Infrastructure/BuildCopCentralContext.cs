// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCop;

namespace Microsoft.Build.BuildCop.Infrastructure;

/// <summary>
/// A manager of the runs of the analyzers - deciding based on configuration of what to run and what to postfilter.
/// </summary>
internal sealed class BuildCopCentralContext
{
    private record CallbackRegistry(
        List<(BuildAnalyzerWrapper, Action<BuildCopDataContext<EvaluatedPropertiesAnalysisData>>)> EvaluatedPropertiesActions,
        List<(BuildAnalyzerWrapper, Action<BuildCopDataContext<ParsedItemsAnalysisData>>)> ParsedItemsActions)
    {
        public CallbackRegistry() : this([],[]) { }
    }

    // In a future we can have callbacks per project as well
    private readonly CallbackRegistry _globalCallbacks = new();

    // This we can potentially use to subscribe for receiving evaluated props in the
    //  build event args. However - this needs to be done early on, when analyzers might not be known yet
    internal bool HasEvaluatedPropertiesActions => _globalCallbacks.EvaluatedPropertiesActions.Any();
    internal bool HasParsedItemsActions => _globalCallbacks.ParsedItemsActions.Any();

    internal void RegisterEvaluatedPropertiesAction(BuildAnalyzerWrapper analyzer, Action<BuildCopDataContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction)
        // Here we might want to communicate to node that props need to be sent.
        //  (it was being communicated via MSBUILDLOGPROPERTIESANDITEMSAFTEREVALUATION)
        => RegisterAction(analyzer, evaluatedPropertiesAction, _globalCallbacks.EvaluatedPropertiesActions);

    internal void RegisterParsedItemsAction(BuildAnalyzerWrapper analyzer, Action<BuildCopDataContext<ParsedItemsAnalysisData>> parsedItemsAction)
        => RegisterAction(analyzer, parsedItemsAction, _globalCallbacks.ParsedItemsActions);

    private void RegisterAction<T>(
        BuildAnalyzerWrapper wrappedAnalyzer,
        Action<BuildCopDataContext<T>> handler,
        List<(BuildAnalyzerWrapper, Action<BuildCopDataContext<T>>)> handlersRegistry)
        where T : AnalysisData
    {
        void WrappedHandler(BuildCopDataContext<T> context)
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
    }

    internal void RunEvaluatedPropertiesActions(
        EvaluatedPropertiesAnalysisData evaluatedPropertiesAnalysisData,
        LoggingContext loggingContext,
        Action<BuildAnalyzerWrapper, LoggingContext, BuildAnalyzerConfigurationInternal[], BuildCopResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.EvaluatedPropertiesActions, evaluatedPropertiesAnalysisData,
            loggingContext, resultHandler);

    internal void RunParsedItemsActions(
        ParsedItemsAnalysisData parsedItemsAnalysisData,
        LoggingContext loggingContext,
        Action<BuildAnalyzerWrapper, LoggingContext, BuildAnalyzerConfigurationInternal[], BuildCopResult>
            resultHandler)
        => RunRegisteredActions(_globalCallbacks.ParsedItemsActions, parsedItemsAnalysisData,
            loggingContext, resultHandler);

    private void RunRegisteredActions<T>(
        List<(BuildAnalyzerWrapper, Action<BuildCopDataContext<T>>)> registeredCallbacks,
        T analysisData,
        LoggingContext loggingContext,
        Action<BuildAnalyzerWrapper, LoggingContext, BuildAnalyzerConfigurationInternal[], BuildCopResult> resultHandler)
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
                // TODO: tracing - we might want tp account this entire block
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
                        ConfigurationProvider.GetMergedConfigurations(projectFullPath,
                            analyzerCallback.Item1.BuildAnalyzer);
                    if (configPerRule.All(c => !c.IsEnabled))
                    {
                        return;
                    }
                }

                // TODO: if the input data supports that - check the configPerRule[0].EvaluationAnalysisScope

                BuildCopDataContext<T> context = new BuildCopDataContext<T>(
                    analyzerCallback.Item1,
                    loggingContext,
                    configPerRule,
                    resultHandler,
                    analysisData);

                analyzerCallback.Item2(context);
            });
    }
}
