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
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Components.Caching;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCop.Acquisition;
using Microsoft.Build.BuildCop.Analyzers;
using Microsoft.Build.BuildCop.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Experimental.BuildCop;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCop.Infrastructure;

internal delegate BuildAnalyzer BuildAnalyzerFactory();
internal delegate BuildAnalyzerWrapper BuildAnalyzerWrapperFactory(ConfigurationContext configurationContext);

/// <summary>
/// The central manager for the BuildCop - this is the integration point with MSBuild infrastructure.
/// </summary>
internal sealed class BuildCopManagerProvider : IBuildCopManagerProvider
{
    private static int s_isInitialized = 0;
    private static IBuildCopManager s_globalInstance = new NullBuildCopManager();
    internal static IBuildCopManager GlobalInstance => s_isInitialized != 0 ? s_globalInstance : throw new InvalidOperationException("BuildCopManagerProvider not initialized");

    public IBuildCopManager Instance => GlobalInstance;

    internal static IBuildComponent CreateComponent(BuildComponentType type)
    {
        ErrorUtilities.VerifyThrow(type == BuildComponentType.BuildCop, "Cannot create components of type {0}", type);
        return new BuildCopManagerProvider();
    }

    public void InitializeComponent(IBuildComponentHost host)
    {
        ErrorUtilities.VerifyThrow(host != null, "BuildComponentHost was null");

        if (Interlocked.CompareExchange(ref s_isInitialized, 1, 0) == 1)
        {
            // Already initialized
            return;
        }

        if (host!.BuildParameters.IsBuildCopEnabled)
        {
            s_globalInstance = new BuildCopManager(host.LoggingService);
        }
        else
        {
            s_globalInstance = new NullBuildCopManager();
        }
    }

    public void ShutdownComponent() => GlobalInstance.Shutdown();


    private sealed class BuildCopManager : IBuildCopManager
    {
        private readonly TracingReporter _tracingReporter = new TracingReporter();
        private readonly BuildCopCentralContext _buildCopCentralContext = new();
        private readonly ILoggingService _loggingService;
        private readonly List<BuildAnalyzerFactoryContext> _analyzersRegistry =[];
        private readonly bool[] _enabledDataSources = new bool[(int)BuildCopDataSource.ValuesCount];
        private readonly BuildEventsProcessor _buildEventsProcessor;
        private readonly BuildCopAcquisitionModule _acquisitionModule = new();

        private bool IsInProcNode => _enabledDataSources[(int)BuildCopDataSource.EventArgs] &&
                                     _enabledDataSources[(int)BuildCopDataSource.BuildExecution];

        /// <summary>
        /// Notifies the manager that the data source will be used -
        ///   so it should register the built-in analyzers for the source if it hasn't been done yet.
        /// </summary>
        /// <param name="buildCopDataSource"></param>
        public void SetDataSource(BuildCopDataSource buildCopDataSource)
        {
            if (!_enabledDataSources[(int)buildCopDataSource])
            {
                _enabledDataSources[(int)buildCopDataSource] = true;
                RegisterBuiltInAnalyzers(buildCopDataSource);
            }
        }

        public void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData)
        {
            if (IsInProcNode)
            {
                var factory = _acquisitionModule.CreateBuildAnalyzerFactory(acquisitionData);
                RegisterCustomAnalyzer(BuildCopDataSource.EventArgs, factory);
            }
            else
            {
                BuildCopAcquisitionEventArgs eventArgs = acquisitionData.ToBuildEventArgs();

                // TODO: We may want to pass the real context here (from evaluation)
                eventArgs.BuildEventContext = new BuildEventContext(
                    BuildEventContext.InvalidNodeId,
                    BuildEventContext.InvalidProjectInstanceId,
                    BuildEventContext.InvalidProjectContextId,
                    BuildEventContext.InvalidTargetId,
                    BuildEventContext.InvalidTaskId);
            }
        }

        internal BuildCopManager(ILoggingService loggingService)
        {
            _loggingService = loggingService;
            _buildEventsProcessor = new(_buildCopCentralContext);
        }

        private static T Construct<T>() where T : new() => new();
        private static readonly (string[] ruleIds, bool defaultEnablement, BuildAnalyzerFactory factory)[][] s_builtInFactoriesPerDataSource =
        [
            // BuildCopDataSource.EventArgs
            [
                ([SharedOutputPathAnalyzer.SupportedRule.Id], SharedOutputPathAnalyzer.SupportedRule.DefaultConfiguration.IsEnabled ?? false, Construct<SharedOutputPathAnalyzer>)
            ],
            // BuildCopDataSource.Execution
            []
        ];

        private void RegisterBuiltInAnalyzers(BuildCopDataSource buildCopDataSource)
        {
            _analyzersRegistry.AddRange(
                s_builtInFactoriesPerDataSource[(int)buildCopDataSource]
                    .Select(v => new BuildAnalyzerFactoryContext(v.factory, v.ruleIds, v.defaultEnablement)));
        }

        /// <summary>
        /// To be used by acquisition module
        /// Registeres the custom analyzer, the construction of analyzer is deferred until the first using project is encountered
        /// </summary>
        internal void RegisterCustomAnalyzer(
            BuildCopDataSource buildCopDataSource,
            BuildAnalyzerFactory factory,
            string[] ruleIds,
            bool defaultEnablement)
        {
            if (_enabledDataSources[(int)buildCopDataSource])
            {
                _analyzersRegistry.Add(new BuildAnalyzerFactoryContext(factory, ruleIds, defaultEnablement));
            }
        }

        /// <summary>
        /// To be used by acquisition module
        /// Registeres the custom analyzer, the construction of analyzer is needed during registration
        /// </summary>
        internal void RegisterCustomAnalyzer(
            BuildCopDataSource buildCopDataSource,
            BuildAnalyzerFactory factory)
        {
            if (_enabledDataSources[(int)buildCopDataSource])
            {
                var instance = factory();
                _analyzersRegistry.Add(new BuildAnalyzerFactoryContext(factory,
                    instance.SupportedRules.Select(r => r.Id).ToArray(),
                    instance.SupportedRules.Any(r => r.DefaultConfiguration.IsEnabled == true)));
            }
        }

        private void SetupSingleAnalyzer(BuildAnalyzerFactoryContext analyzerFactoryContext, string projectFullPath, BuildEventContext buildEventContext)
        {
            // TODO: For user analyzers - it should run only on projects where referenced
            //  on others it should work similarly as disabling them.
            // Disabled analyzer should not only post-filter results - it shouldn't even see the data 

            BuildAnalyzerWrapper wrapper;
            BuildAnalyzerConfigurationInternal[] configurations;
            if (analyzerFactoryContext.MaterializedAnalyzer == null)
            {
                BuildAnalyzerConfiguration[] userConfigs =
                    ConfigurationProvider.GetUserConfigurations(projectFullPath, analyzerFactoryContext.RuleIds);

                if (userConfigs.All(c => !(c.IsEnabled ?? analyzerFactoryContext.IsEnabledByDefault)))
                {
                    // the analyzer was not yet instantiated nor mounted - so nothing to do here now.
                    return;
                }

                CustomConfigurationData[] customConfigData =
                    ConfigurationProvider.GetCustomConfigurations(projectFullPath, analyzerFactoryContext.RuleIds);

                ConfigurationContext configurationContext = ConfigurationContext.FromDataEnumeration(customConfigData);

                wrapper = analyzerFactoryContext.Factory(configurationContext);
                analyzerFactoryContext.MaterializedAnalyzer = wrapper;
                BuildAnalyzer analyzer = wrapper.BuildAnalyzer;

                if (
                    analyzer.SupportedRules.Count != analyzerFactoryContext.RuleIds.Length
                    ||
                    !analyzer.SupportedRules.Select(r => r.Id)
                        .SequenceEqual(analyzerFactoryContext.RuleIds, StringComparer.CurrentCultureIgnoreCase)
                )
                {
                    throw new BuildCopConfigurationException(
                        $"The analyzer '{analyzer.FriendlyName}' exposes rules '{analyzer.SupportedRules.Select(r => r.Id).ToCsvString()}', but different rules were declared during registration: '{analyzerFactoryContext.RuleIds.ToCsvString()}'");
                }

                configurations = ConfigurationProvider.GetMergedConfigurations(userConfigs, analyzer);

                // technically all analyzers rules could be disabled, but that would mean
                // that the provided 'IsEnabledByDefault' value wasn't correct - the only
                // price to be paid in that case is slight performance cost.

                // Create the wrapper and register to central context
                wrapper.StartNewProject(projectFullPath, configurations);
                var wrappedContext = new BuildCopContext(wrapper, _buildCopCentralContext);
                analyzer.RegisterActions(wrappedContext);
            }
            else
            {
                wrapper = analyzerFactoryContext.MaterializedAnalyzer;

                configurations = ConfigurationProvider.GetMergedConfigurations(projectFullPath, wrapper.BuildAnalyzer);

                ConfigurationProvider.CheckCustomConfigurationDataValidity(projectFullPath,
                    analyzerFactoryContext.RuleIds[0]);

                // Update the wrapper
                wrapper.StartNewProject(projectFullPath, configurations);
            }

            if (configurations.GroupBy(c => c.EvaluationAnalysisScope).Count() > 1)
            {
                throw new BuildCopConfigurationException(
                    string.Format("All rules for a single analyzer should have the same EvaluationAnalysisScope for a single project (violating rules: [{0}], project: {1})",
                        analyzerFactoryContext.RuleIds.ToCsvString(),
                        projectFullPath));
            }
        }

        private void SetupAnalyzersForNewProject(string projectFullPath, BuildEventContext buildEventContext)
        {
            // Only add analyzers here
            // On an execution node - we might remove and dispose the analyzers once project is done

            // If it's already constructed - just control the custom settings do not differ

            List<BuildAnalyzerFactoryContext> analyzersToRemove = new();
            foreach (BuildAnalyzerFactoryContext analyzerFactoryContext in _analyzersRegistry)
            {
                try
                {
                    SetupSingleAnalyzer(analyzerFactoryContext, projectFullPath, buildEventContext);
                }
                catch (BuildCopConfigurationException e)
                {
                    _loggingService.LogErrorFromText(buildEventContext, null, null, null,
                        new BuildEventFileInfo(projectFullPath),
                        e.Message);
                    _loggingService.LogCommentFromText(buildEventContext, MessageImportance.High, $"Dismounting analyzer '{analyzerFactoryContext.FriendlyName}'");
                    analyzersToRemove.Add(analyzerFactoryContext);
                }
            }

            analyzersToRemove.ForEach(c => _analyzersRegistry.Remove(c));
            foreach (var analyzerToRemove in analyzersToRemove.Select(a => a.MaterializedAnalyzer).Where(a => a != null))
            {
                _buildCopCentralContext.DeregisterAnalyzer(analyzerToRemove!);
                _tracingReporter.AddStats(analyzerToRemove!.BuildAnalyzer.FriendlyName, analyzerToRemove.Elapsed);
                analyzerToRemove.BuildAnalyzer.Dispose();
            }
        }


        public void ProcessEvaluationFinishedEventArgs(
            IBuildAnalysisLoggingContext buildAnalysisContext,
            ProjectEvaluationFinishedEventArgs evaluationFinishedEventArgs)
            => _buildEventsProcessor
                .ProcessEvaluationFinishedEventArgs(buildAnalysisContext, evaluationFinishedEventArgs);

        // TODO: tracing: https://github.com/dotnet/msbuild/issues/9629
        public Dictionary<string, TimeSpan> CreateTracingStats()
        {
            foreach (BuildAnalyzerFactoryContext analyzerFactoryContext in _analyzersRegistry)
            {
                if (analyzerFactoryContext.MaterializedAnalyzer != null)
                {
                    _tracingReporter.AddStats(analyzerFactoryContext.FriendlyName,
                        analyzerFactoryContext.MaterializedAnalyzer.Elapsed);
                    analyzerFactoryContext.MaterializedAnalyzer.ClearStats();
                }
            }

            return _tracingReporter.TracingStats;
        }

        public void FinalizeProcessing(LoggingContext loggingContext)
        {
            if (IsInProcNode)
            {
                // We do not want to send tracing stats from in-proc node
                return;
            }

            BuildCopTracingEventArgs eventArgs =
                new(CreateTracingStats()) { BuildEventContext = loggingContext.BuildEventContext };
            loggingContext.LogBuildEvent(eventArgs);
        }

        public void StartProjectEvaluation(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext,
            string fullPath)
        {
            if (buildCopDataSource == BuildCopDataSource.EventArgs && IsInProcNode)
            {
                // Skipping this event - as it was already handled by the in-proc node.
                // This is because in-proc node has the BuildEventArgs source and BuildExecution source
                //  both in a single manager. The project started is first encountered by the execution before the EventArg is sent
                return;
            }

            SetupAnalyzersForNewProject(fullPath, buildEventContext);
        }

        /*
         *
         * Following methods are for future use (should we decide to approach in-execution analysis)
         *
         */


        public void EndProjectEvaluation(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext)
        {
        }

        public void StartProjectRequest(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext)
        {
        }

        public void EndProjectRequest(BuildCopDataSource buildCopDataSource, BuildEventContext buildEventContext)
        {
        }

        public void Shutdown()
        { /* Too late here for any communication to the main node or for logging anything */ }

        private class BuildAnalyzerFactoryContext(
            BuildAnalyzerFactory factory,
            string[] ruleIds,
            bool isEnabledByDefault)
        {
            public BuildAnalyzerWrapperFactory Factory { get; init; } = configContext =>
            {
                BuildAnalyzer ba = factory();
                ba.Initialize(configContext);
                return new BuildAnalyzerWrapper(ba);
            };
            public BuildAnalyzerWrapper? MaterializedAnalyzer { get; set; }
            public string[] RuleIds { get; init; } = ruleIds;
            public bool IsEnabledByDefault { get; init; } = isEnabledByDefault;
            public string FriendlyName => MaterializedAnalyzer?.BuildAnalyzer.FriendlyName ?? factory().FriendlyName;
        }
    }
}
