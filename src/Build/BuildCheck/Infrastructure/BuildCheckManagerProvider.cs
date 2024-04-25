// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Acquisition;
using Microsoft.Build.BuildCheck.Analyzers;
using Microsoft.Build.BuildCheck.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BuildCheck.Infrastructure;

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
                instance = new BuildCheckManager(host.LoggingService);
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

    internal sealed class BuildCheckManager : IBuildCheckManager
    {
        private readonly TracingReporter _tracingReporter = new TracingReporter();
        private readonly BuildCheckCentralContext _buildCheckCentralContext = new();
        private readonly ILoggingService _loggingService;
        private readonly List<BuildAnalyzerFactoryContext> _analyzersRegistry;
        private readonly bool[] _enabledDataSources = new bool[(int)BuildCheckDataSource.ValuesCount];
        private readonly BuildEventsProcessor _buildEventsProcessor;
        private readonly IBuildCheckAcquisitionModule _acquisitionModule;

        internal BuildCheckManager(ILoggingService loggingService)
        {
            _analyzersRegistry = new List<BuildAnalyzerFactoryContext>();
            _acquisitionModule = new BuildCheckAcquisitionModule(loggingService);
            _loggingService = loggingService;
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
            if (!_enabledDataSources[(int)buildCheckDataSource])
            {
                _enabledDataSources[(int)buildCheckDataSource] = true;
                RegisterBuiltInAnalyzers(buildCheckDataSource);
            }
        }

        public void ProcessAnalyzerAcquisition(AnalyzerAcquisitionData acquisitionData, BuildEventContext buildEventContext)
        {
            if (IsInProcNode)
            {
                var analyzersFactories = _acquisitionModule.CreateBuildAnalyzerFactories(acquisitionData, buildEventContext);
                if (analyzersFactories.Count != 0)
                {
                    RegisterCustomAnalyzer(BuildCheckDataSource.EventArgs, analyzersFactories, buildEventContext);
                }
                else
                {
                    _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "CustomAnalyzerFailedAcquisition", acquisitionData.AssemblyPath);
                }
            }
            else
            {
                BuildCheckAcquisitionEventArgs eventArgs = acquisitionData.ToBuildEventArgs();
                eventArgs.BuildEventContext = buildEventContext;

                _loggingService.LogBuildEvent(eventArgs);
            }
        }

        private static T Construct<T>() where T : new() => new();

        private static readonly (string[] ruleIds, bool defaultEnablement, BuildAnalyzerFactory factory)[][] s_builtInFactoriesPerDataSource =
        [
            // BuildCheckDataSource.EventArgs
            [
                ([SharedOutputPathAnalyzer.SupportedRule.Id], SharedOutputPathAnalyzer.SupportedRule.DefaultConfiguration.IsEnabled ?? false, Construct<SharedOutputPathAnalyzer>)
            ],
            // BuildCheckDataSource.Execution
            []
        ];

        private void RegisterBuiltInAnalyzers(BuildCheckDataSource buildCheckDataSource)
        {
            _analyzersRegistry.AddRange(
                s_builtInFactoriesPerDataSource[(int)buildCheckDataSource]
                    .Select(v => new BuildAnalyzerFactoryContext(v.factory, v.ruleIds, v.defaultEnablement)));
        }

        /// <summary>
        /// To be used by acquisition module.
        /// Registers the custom analyzers, the construction of analyzers is deferred until the first using project is encountered.
        /// </summary>
        internal void RegisterCustomAnalyzers(
            BuildCheckDataSource buildCheckDataSource,
            IEnumerable<BuildAnalyzerFactory> factories,
            string[] ruleIds,
            bool defaultEnablement)
        {
            if (_enabledDataSources[(int)buildCheckDataSource])
            {
                foreach (BuildAnalyzerFactory factory in factories)
                {
                    _analyzersRegistry.Add(new BuildAnalyzerFactoryContext(factory, ruleIds, defaultEnablement));
                }
            }
        }

        /// <summary>
        /// To be used by acquisition module
        /// Registers the custom analyzer, the construction of analyzer is needed during registration.
        /// </summary>
        /// <param name="buildCheckDataSource">Represents different data sources used in build check operations.</param>
        /// <param name="factories">A collection of build analyzer factories for rules instantiation.</param>
        /// <param name="buildEventContext">The context of the build event.</param>
        internal void RegisterCustomAnalyzer(
            BuildCheckDataSource buildCheckDataSource,
            IEnumerable<BuildAnalyzerFactory> factories,
            BuildEventContext buildEventContext)
        {
            if (_enabledDataSources[(int)buildCheckDataSource])
            {
                foreach (var factory in factories)
                {
                    var instance = factory();
                    _analyzersRegistry.Add(new BuildAnalyzerFactoryContext(
                        factory,
                        instance.SupportedRules.Select(r => r.Id).ToArray(),
                        instance.SupportedRules.Any(r => r.DefaultConfiguration.IsEnabled == true)));
                    _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "CustomAnalyzerSuccessfulAcquisition", instance.FriendlyName);
                }     
            }
        }

        private void SetupSingleAnalyzer(BuildAnalyzerFactoryContext analyzerFactoryContext, string projectFullPath, BuildEventContext buildEventContext)
        {
            // For custom analyzers - it should run only on projects where referenced
            //  (otherwise error out - https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=57849480)
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

                configurations = ConfigurationProvider.GetMergedConfigurations(userConfigs, analyzer);

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

                configurations = ConfigurationProvider.GetMergedConfigurations(projectFullPath, wrapper.BuildAnalyzer);

                ConfigurationProvider.CheckCustomConfigurationDataValidity(projectFullPath,
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
                catch (BuildCheckConfigurationException e)
                {
                    _loggingService.LogErrorFromText(buildEventContext, null, null, null,
                        new BuildEventFileInfo(projectFullPath),
                        e.Message);
                    analyzersToRemove.Add(analyzerFactoryContext);
                }
            }

            analyzersToRemove.ForEach(c =>
            {
                _analyzersRegistry.Remove(c);
                _loggingService.LogCommentFromText(buildEventContext, MessageImportance.High, $"Dismounting analyzer '{c.FriendlyName}'");
            });
            foreach (var analyzerToRemove in analyzersToRemove.Select(a => a.MaterializedAnalyzer).Where(a => a != null))
            {
                _buildCheckCentralContext.DeregisterAnalyzer(analyzerToRemove!);
                _tracingReporter.AddStats(analyzerToRemove!.BuildAnalyzer.FriendlyName, analyzerToRemove.Elapsed);
                analyzerToRemove.BuildAnalyzer.Dispose();
            }
        }

        public void ProcessEvaluationFinishedEventArgs(
            AnalyzerLoggingContext buildAnalysisContext,
            ProjectEvaluationFinishedEventArgs evaluationFinishedEventArgs)
            => _buildEventsProcessor
                .ProcessEvaluationFinishedEventArgs(buildAnalysisContext, evaluationFinishedEventArgs);

        // Tracing: https://github.com/dotnet/msbuild/issues/9629
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

            BuildCheckTracingEventArgs eventArgs =
                new(CreateTracingStats()) { BuildEventContext = loggingContext.BuildEventContext };
            loggingContext.LogBuildEvent(eventArgs);
        }

        public void StartProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext,
            string fullPath)
        {
            if (buildCheckDataSource == BuildCheckDataSource.EventArgs && IsInProcNode)
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


        public void EndProjectEvaluation(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
        {
        }

        public void StartProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
        {
        }

        public void EndProjectRequest(BuildCheckDataSource buildCheckDataSource, BuildEventContext buildEventContext)
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
