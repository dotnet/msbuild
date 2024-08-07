﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Acquisition;
using Microsoft.Build.Experimental.BuildCheck.Analyzers;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Evaluation;
using Microsoft.Build.BuildCheck.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal delegate BuildAnalyzer BuildAnalyzerFactory();
internal delegate BuildAnalyzerWrapper BuildAnalyzerWrapperFactory(ConfigurationContext configurationContext);

/// <summary>
/// The central manager for the BuildCheck - this is the integration point with MSBuild infrastructure.
/// </summary>
internal sealed class BuildCheckManagerProvider : IBuildCheckManagerProvider
{
    private static IBuildCheckManager? s_globalInstance;

    internal static IBuildCheckManager GlobalInstance => s_globalInstance ?? throw new InvalidOperationException("BuildCheckManagerProvider not initialized");

    public IBuildCheckManager Instance => GlobalInstance;

    public IBuildEngineDataRouter BuildEngineDataRouter => (IBuildEngineDataRouter)GlobalInstance;

    public static IBuildEngineDataRouter? GlobalBuildEngineDataRouter => (IBuildEngineDataRouter?)s_globalInstance;

    internal static IBuildComponent CreateComponent(BuildComponentType type)
    {
        ErrorUtilities.VerifyThrow(type == BuildComponentType.BuildCheckManagerProvider, "Cannot create components of type {0}", type);
        return new BuildCheckManagerProvider();
    }

    public void InitializeComponent(IBuildComponentHost host)
    {
        ErrorUtilities.VerifyThrow(host != null, "BuildComponentHost was null");

        if (s_globalInstance == null)
        {
            IBuildCheckManager instance;
            if (host!.BuildParameters.IsBuildCheckEnabled)
            {
                instance = new BuildCheckManager();
            }
            else
            {
                instance = new NullBuildCheckManager();
            }

            // We are fine with the possibility of double creation here - as the construction is cheap
            //  and without side effects and the actual backing field is effectively immutable after the first assignment.
            Interlocked.CompareExchange(ref s_globalInstance, instance, null);
        }
    }

    public void ShutdownComponent() => GlobalInstance.Shutdown();

    internal sealed class BuildCheckManager : IBuildCheckManager, IBuildEngineDataRouter
    {
        private readonly TracingReporter _tracingReporter = new TracingReporter();
        private readonly IConfigurationProvider _configurationProvider = new ConfigurationProvider();
        private readonly BuildCheckCentralContext _buildCheckCentralContext;
        private readonly List<BuildAnalyzerFactoryContext> _analyzersRegistry;
        private readonly bool[] _enabledDataSources = new bool[(int)BuildCheckDataSource.ValuesCount];
        private readonly BuildEventsProcessor _buildEventsProcessor;
        private readonly IBuildCheckAcquisitionModule _acquisitionModule;

        internal BuildCheckManager()
        {
            _analyzersRegistry = new List<BuildAnalyzerFactoryContext>();
            _acquisitionModule = new BuildCheckAcquisitionModule();
            _buildCheckCentralContext = new(_configurationProvider);
            _buildEventsProcessor = new(_buildCheckCentralContext);
        }

        private bool IsInProcNode => _enabledDataSources[(int)BuildCheckDataSource.EventArgs] &&
                                     _enabledDataSources[(int)BuildCheckDataSource.BuildExecution];

        /// <summary>
        /// Notifies the manager that the data source will be used -
        ///   so it should register the built-in analyzers for the source if it hasn't been done yet.
        /// </summary>
        /// <param name="buildCheckDataSource"></param>
        public void SetDataSource(BuildCheckDataSource buildCheckDataSource)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (!_enabledDataSources[(int)buildCheckDataSource])
            {
                _enabledDataSources[(int)buildCheckDataSource] = true;
                RegisterBuiltInAnalyzers(buildCheckDataSource);
            }
            stopwatch.Stop();
            _tracingReporter.AddSetDataSourceStats(stopwatch.Elapsed);
        }

        public void ProcessAnalyzerAcquisition(
            AnalyzerAcquisitionData acquisitionData,
            IAnalysisContext analysisContext)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (IsInProcNode)
            {
                var analyzersFactories = _acquisitionModule.CreateBuildAnalyzerFactories(acquisitionData, analysisContext);
                if (analyzersFactories.Count != 0)
                {
                    RegisterCustomAnalyzer(acquisitionData.ProjectPath, BuildCheckDataSource.EventArgs, analyzersFactories, analysisContext);
                }
                else
                {
                    analysisContext.DispatchAsComment(MessageImportance.Normal, "CustomAnalyzerFailedAcquisition", acquisitionData.AssemblyPath);
                }
            }
            else
            {
                BuildCheckAcquisitionEventArgs eventArgs = acquisitionData.ToBuildEventArgs();
                eventArgs.BuildEventContext = analysisContext.BuildEventContext!;

                analysisContext.DispatchBuildEvent(eventArgs);
            }

            stopwatch.Stop();
            _tracingReporter.AddAcquisitionStats(stopwatch.Elapsed);
        }

        private static T Construct<T>() where T : new() => new();

        private static readonly (string[] ruleIds, bool defaultEnablement, BuildAnalyzerFactory factory)[][] s_builtInFactoriesPerDataSource =
        [

            // BuildCheckDataSource.EventArgs
            [
                ([SharedOutputPathAnalyzer.SupportedRule.Id], SharedOutputPathAnalyzer.SupportedRule.DefaultConfiguration.IsEnabled ?? false, Construct<SharedOutputPathAnalyzer>),
                ([DoubleWritesAnalyzer.SupportedRule.Id], DoubleWritesAnalyzer.SupportedRule.DefaultConfiguration.IsEnabled ?? false, Construct<DoubleWritesAnalyzer>),
                ([NoEnvironmentVariablePropertyAnalyzer.SupportedRule.Id], NoEnvironmentVariablePropertyAnalyzer.SupportedRule.DefaultConfiguration.IsEnabled ?? false, Construct<NoEnvironmentVariablePropertyAnalyzer>)
            ],

            // BuildCheckDataSource.Execution
            []
        ];

        /// <summary>
        /// For tests only. TODO: Remove when analyzer acquisition is done.
        /// </summary>
        internal static (string[] ruleIds, bool defaultEnablement, BuildAnalyzerFactory factory)[][]? s_testFactoriesPerDataSource;

        private void RegisterBuiltInAnalyzers(BuildCheckDataSource buildCheckDataSource)
        {
            _analyzersRegistry.AddRange(
                s_builtInFactoriesPerDataSource[(int)buildCheckDataSource]
                    .Select(v => new BuildAnalyzerFactoryContext(v.factory, v.ruleIds, v.defaultEnablement)));

            if (s_testFactoriesPerDataSource is not null)
            {
                _analyzersRegistry.AddRange(
                    s_testFactoriesPerDataSource[(int)buildCheckDataSource]
                        .Select(v => new BuildAnalyzerFactoryContext(v.factory, v.ruleIds, v.defaultEnablement)));
            }
        }

        /// <summary>
        /// To be used by acquisition module
        /// Registers the custom analyzer, the construction of analyzer is needed during registration.
        /// </summary>
        /// <param name="projectPath">The project path is used for the correct .editorconfig resolution.</param>
        /// <param name="buildCheckDataSource">Represents different data sources used in build check operations.</param>
        /// <param name="factories">A collection of build analyzer factories for rules instantiation.</param>
        /// <param name="analysisContext">The logging context of the build event.</param>
        internal void RegisterCustomAnalyzer(
            string projectPath,
            BuildCheckDataSource buildCheckDataSource,
            IEnumerable<BuildAnalyzerFactory> factories,
            IAnalysisContext analysisContext)
        {
            if (_enabledDataSources[(int)buildCheckDataSource])
            {
                foreach (var factory in factories)
                {
                    var instance = factory();
                    var analyzerFactoryContext = new BuildAnalyzerFactoryContext(
                        factory,
                        instance.SupportedRules.Select(r => r.Id).ToArray(),
                        instance.SupportedRules.Any(r => r.DefaultConfiguration.IsEnabled == true));

                    if (analyzerFactoryContext != null)
                    {
                        _analyzersRegistry.Add(analyzerFactoryContext);
                        SetupSingleAnalyzer(analyzerFactoryContext, projectPath);
                        analysisContext.DispatchAsComment(MessageImportance.Normal, "CustomAnalyzerSuccessfulAcquisition", instance.FriendlyName);
                    }
                }
            }
        }

        private void SetupSingleAnalyzer(BuildAnalyzerFactoryContext analyzerFactoryContext, string projectFullPath)
        {
            // For custom analyzers - it should run only on projects where referenced
            // (otherwise error out - https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=57849480)
            // on others it should work similarly as disabling them.
            // Disabled analyzer should not only post-filter results - it shouldn't even see the data 
            BuildAnalyzerWrapper wrapper;
            BuildAnalyzerConfigurationEffective[] configurations;
            if (analyzerFactoryContext.MaterializedAnalyzer == null)
            {
                BuildAnalyzerConfiguration[] userConfigs =
                    _configurationProvider.GetUserConfigurations(projectFullPath, analyzerFactoryContext.RuleIds);

                if (userConfigs.All(c => !(c.IsEnabled ?? analyzerFactoryContext.IsEnabledByDefault)))
                {
                    // the analyzer was not yet instantiated nor mounted - so nothing to do here now.
                    return;
                }

                CustomConfigurationData[] customConfigData =
                    _configurationProvider.GetCustomConfigurations(projectFullPath, analyzerFactoryContext.RuleIds);

                BuildAnalyzer uninitializedAnalyzer = analyzerFactoryContext.Factory();
                configurations = _configurationProvider.GetMergedConfigurations(userConfigs, uninitializedAnalyzer);

                ConfigurationContext configurationContext = ConfigurationContext.FromDataEnumeration(customConfigData, configurations);

                wrapper = analyzerFactoryContext.Initialize(uninitializedAnalyzer, configurationContext);
                analyzerFactoryContext.MaterializedAnalyzer = wrapper;
                BuildAnalyzer analyzer = wrapper.BuildAnalyzer;

                // This is to facilitate possible perf improvement for custom analyzers - as we might want to
                //  avoid loading the assembly and type just to check if it's supported.
                // If we expose a way to declare the enablement status and rule ids during registration (e.g. via
                //  optional arguments of the intrinsic property function) - we can then avoid loading it.
                // But once loaded - we should verify that the declared enablement status and rule ids match the actual ones.
                if (
                    analyzer.SupportedRules.Count != analyzerFactoryContext.RuleIds.Length
                    ||
                    !analyzer.SupportedRules.Select(r => r.Id)
                        .SequenceEqual(analyzerFactoryContext.RuleIds, StringComparer.CurrentCultureIgnoreCase)
                )
                {
                    throw new BuildCheckConfigurationException(
                        $"The analyzer '{analyzer.FriendlyName}' exposes rules '{analyzer.SupportedRules.Select(r => r.Id).ToCsvString()}', but different rules were declared during registration: '{analyzerFactoryContext.RuleIds.ToCsvString()}'");
                }

                // technically all analyzers rules could be disabled, but that would mean
                // that the provided 'IsEnabledByDefault' value wasn't correct - the only
                // price to be paid in that case is slight performance cost.

                // Create the wrapper and register to central context
                wrapper.StartNewProject(projectFullPath, configurations);
                var wrappedContext = new BuildCheckRegistrationContext(wrapper, _buildCheckCentralContext);
                analyzer.RegisterActions(wrappedContext);
            }
            else
            {
                wrapper = analyzerFactoryContext.MaterializedAnalyzer;

                configurations = _configurationProvider.GetMergedConfigurations(projectFullPath, wrapper.BuildAnalyzer);

                _configurationProvider.CheckCustomConfigurationDataValidity(projectFullPath,
                    analyzerFactoryContext.RuleIds[0]);

                // Update the wrapper
                wrapper.StartNewProject(projectFullPath, configurations);
            }

            if (configurations.GroupBy(c => c.EvaluationAnalysisScope).Count() > 1)
            {
                throw new BuildCheckConfigurationException(
                    string.Format("All rules for a single analyzer should have the same EvaluationAnalysisScope for a single project (violating rules: [{0}], project: {1})",
                        analyzerFactoryContext.RuleIds.ToCsvString(),
                        projectFullPath));
            }
        }

        private void SetupAnalyzersForNewProject(string projectFullPath, IAnalysisContext analysisContext)
        {
            // Only add analyzers here
            // On an execution node - we might remove and dispose the analyzers once project is done

            // If it's already constructed - just control the custom settings do not differ
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<BuildAnalyzerFactoryContext> analyzersToRemove = new();
            foreach (BuildAnalyzerFactoryContext analyzerFactoryContext in _analyzersRegistry)
            {
                try
                {
                    SetupSingleAnalyzer(analyzerFactoryContext, projectFullPath);
                }
                catch (BuildCheckConfigurationException e)
                {
                    analysisContext.DispatchAsErrorFromText(
                        null,
                        null,
                        null,
                        new BuildEventFileInfo(projectFullPath),
                        e.Message);
                    analyzersToRemove.Add(analyzerFactoryContext);
                }
            }

            analyzersToRemove.ForEach(c =>
            {
                _analyzersRegistry.Remove(c);
                analysisContext.DispatchAsCommentFromText(MessageImportance.High, $"Dismounting analyzer '{c.FriendlyName}'");
            });
            foreach (var analyzerToRemove in analyzersToRemove.Select(a => a.MaterializedAnalyzer).Where(a => a != null))
            {
                _buildCheckCentralContext.DeregisterAnalyzer(analyzerToRemove!);
                _tracingReporter.AddAnalyzerStats(analyzerToRemove!.BuildAnalyzer.FriendlyName, analyzerToRemove.Elapsed);
                analyzerToRemove.BuildAnalyzer.Dispose();
            }

            stopwatch.Stop();
            _tracingReporter.AddNewProjectStats(stopwatch.Elapsed);
        }

        public void ProcessEvaluationFinishedEventArgs(
            IAnalysisContext analysisContext,
            ProjectEvaluationFinishedEventArgs evaluationFinishedEventArgs)
        {
            Dictionary<string, string>? propertiesLookup = null;
            // The FileClassifier is normally initialized by executing build requests.
            // However, if we are running in a main node that has no execution nodes - we need to initialize it here (from events).
            if (!IsInProcNode)
            {
                propertiesLookup =
                    BuildEventsProcessor.ExtractPropertiesLookup(evaluationFinishedEventArgs);
                Func<string, string?> getPropertyValue = p =>
                    propertiesLookup.TryGetValue(p, out string? value) ? value : null;

                FileClassifier.Shared.RegisterFrameworkLocations(getPropertyValue);
                FileClassifier.Shared.RegisterKnownImmutableLocations(getPropertyValue);
            }

            _buildEventsProcessor
                .ProcessEvaluationFinishedEventArgs(analysisContext, evaluationFinishedEventArgs, propertiesLookup);
        }

        public void ProcessEnvironmentVariableReadEventArgs(IAnalysisContext analysisContext, EnvironmentVariableReadEventArgs projectEvaluationEventArgs)
        {
            if (projectEvaluationEventArgs is EnvironmentVariableReadEventArgs evr)
            {
                _buildEventsProcessor.ProcessEnvironmentVariableReadEventArgs(
                    evr.EnvironmentVariableName,
                    evr.Message ?? string.Empty,
                    evr.File,
                    evr.LineNumber,
                    evr.ColumnNumber);
            }
        }

        public void ProcessTaskStartedEventArgs(
            IAnalysisContext analysisContext,
            TaskStartedEventArgs taskStartedEventArgs)
            => _buildEventsProcessor
                .ProcessTaskStartedEventArgs(analysisContext, taskStartedEventArgs);

        public void ProcessTaskFinishedEventArgs(
            IAnalysisContext analysisContext,
            TaskFinishedEventArgs taskFinishedEventArgs)
            => _buildEventsProcessor
                .ProcessTaskFinishedEventArgs(analysisContext, taskFinishedEventArgs);

        public void ProcessTaskParameterEventArgs(
            IAnalysisContext analysisContext,
            TaskParameterEventArgs taskParameterEventArgs)
            => _buildEventsProcessor
                .ProcessTaskParameterEventArgs(analysisContext, taskParameterEventArgs);

        public Dictionary<string, TimeSpan> CreateAnalyzerTracingStats()
        {
            foreach (BuildAnalyzerFactoryContext analyzerFactoryContext in _analyzersRegistry)
            {
                if (analyzerFactoryContext.MaterializedAnalyzer != null)
                {
                    _tracingReporter.AddAnalyzerStats(analyzerFactoryContext.FriendlyName, analyzerFactoryContext.MaterializedAnalyzer.Elapsed);
                    analyzerFactoryContext.MaterializedAnalyzer.ClearStats();
                }
            }

            _tracingReporter.AddAnalyzerInfraStats();
            return _tracingReporter.TracingStats;
        }

        public void FinalizeProcessing(LoggingContext loggingContext)
        {
            if (IsInProcNode)
            {
                // We do not want to send tracing stats from in-proc node
                return;
            }

            var analyzerEventStats = CreateAnalyzerTracingStats();

            BuildCheckTracingEventArgs analyzerEventArg =
                new(analyzerEventStats) { BuildEventContext = loggingContext.BuildEventContext };
            loggingContext.LogBuildEvent(analyzerEventArg);
        }

        private readonly ConcurrentDictionary<int, string> _projectsByContextId = new();
        /// <summary>
        /// This method fetches the project full path from the context id.
        /// This is needed because the full path is needed for configuration and later for fetching configured checks
        ///  (future version might optimize by using the ProjectContextId directly for fetching the checks).
        /// </summary>
        /// <param name="buildEventContext"></param>
        /// <returns></returns>
        private string GetProjectFullPath(BuildEventContext buildEventContext)
        {
            const string defaultProjectFullPath = "Unknown_Project";

            if (_projectsByContextId.TryGetValue(buildEventContext.ProjectContextId, out string? projectFullPath))
            {
                return projectFullPath;
            }
            else if (buildEventContext.ProjectContextId == BuildEventContext.InvalidProjectContextId &&
                     _projectsByContextId.Count == 1)
            {
                // The coalescing is for a rare possibility of a race where other thread removed the item (between the if check and fetch here).
                // We currently do not support multiple projects in parallel in a single node anyway.
                return _projectsByContextId.FirstOrDefault().Value ?? defaultProjectFullPath;
            }

            return defaultProjectFullPath;
        }

        public void StartProjectEvaluation(
            BuildCheckDataSource buildCheckDataSource,
            IAnalysisContext analysisContext,
            string projectFullPath)
        {
            if (buildCheckDataSource == BuildCheckDataSource.EventArgs && IsInProcNode)
            {
                // Skipping this event - as it was already handled by the in-proc node.
                // This is because in-proc node has the BuildEventArgs source and BuildExecution source
                //  both in a single manager. The project started is first encountered by the execution before the EventArg is sent
                return;
            }

            SetupAnalyzersForNewProject(projectFullPath, analysisContext);
            _projectsByContextId[analysisContext.BuildEventContext.ProjectContextId] = projectFullPath;
        }

        /*
         *
         * Following methods are for future use (should we decide to approach in-execution analysis)
         *
         */


        public void EndProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
        {
        }

        public void StartProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext, string projectFullPath)
        {
            // There can be multiple ProjectStarted-ProjectFinished per single configuration project build (each request for different target)
            _projectsByContextId[buildEventContext.ProjectContextId] = projectFullPath;
        }

        public void EndProjectRequest(
            BuildCheckDataSource buildCheckDataSource,
            IAnalysisContext analysisContext,
            string projectFullPath)
        {
            _buildEventsProcessor.ProcessProjectDone(analysisContext, projectFullPath);
            _projectsByContextId.TryRemove(analysisContext.BuildEventContext.ProjectContextId, out _);
        }

        public void ProcessPropertyRead(PropertyReadInfo propertyReadInfo, AnalysisLoggingContext analysisContext)
        {
            if (!_buildCheckCentralContext.HasPropertyReadActions)
            {
                return;
            }

            PropertyReadData propertyReadData = new(
                GetProjectFullPath(analysisContext.BuildEventContext),
                analysisContext.BuildEventContext.ProjectInstanceId,
                propertyReadInfo);
            _buildEventsProcessor.ProcessPropertyRead(propertyReadData, analysisContext);
        }

        public void ProcessPropertyWrite(PropertyWriteInfo propertyWriteInfo, AnalysisLoggingContext analysisContext)
        {
            if (!_buildCheckCentralContext.HasPropertyWriteActions)
            {
                return;
            }

            PropertyWriteData propertyWriteData = new(
                GetProjectFullPath(analysisContext.BuildEventContext),
                analysisContext.BuildEventContext.ProjectInstanceId,
                propertyWriteInfo);
            _buildEventsProcessor.ProcessPropertyWrite(propertyWriteData, analysisContext);
        }

        public void Shutdown()
        { /* Too late here for any communication to the main node or for logging anything */ }

        private class BuildAnalyzerFactoryContext(
            BuildAnalyzerFactory factory,
            string[] ruleIds,
            bool isEnabledByDefault)
        {
            public BuildAnalyzer Factory()
            {
                BuildAnalyzer ba = factory();
                return ba;
            }

            public BuildAnalyzerWrapper Initialize(BuildAnalyzer ba, ConfigurationContext configContext)
            {
                ba.Initialize(configContext);
                return new BuildAnalyzerWrapper(ba);
            }

            public BuildAnalyzerWrapper? MaterializedAnalyzer { get; set; }

            public string[] RuleIds { get; init; } = ruleIds;

            public bool IsEnabledByDefault { get; init; } = isEnabledByDefault;

            public string FriendlyName => MaterializedAnalyzer?.BuildAnalyzer.FriendlyName ?? factory().FriendlyName;
        }
    }
}
