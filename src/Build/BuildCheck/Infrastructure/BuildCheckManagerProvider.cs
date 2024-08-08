// Licensed to the .NET Foundation under one or more agreements.
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
using Microsoft.Build.Experimental.BuildCheck.Checks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Evaluation;
using Microsoft.Build.BuildCheck.Infrastructure;

namespace Microsoft.Build.Experimental.BuildCheck.Infrastructure;

internal delegate Check CheckFactory();
internal delegate CheckWrapper CheckWrapperFactory(ConfigurationContext configurationContext);

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
        private readonly List<CheckFactoryContext> _checkRegistry;
        private readonly bool[] _enabledDataSources = new bool[(int)BuildCheckDataSource.ValuesCount];
        private readonly BuildEventsProcessor _buildEventsProcessor;
        private readonly IBuildCheckAcquisitionModule _acquisitionModule;

        internal BuildCheckManager()
        {
            _checkRegistry = new List<CheckFactoryContext>();
            _acquisitionModule = new BuildCheckAcquisitionModule();
            _buildCheckCentralContext = new(_configurationProvider);
            _buildEventsProcessor = new(_buildCheckCentralContext);
        }

        private bool IsInProcNode => _enabledDataSources[(int)BuildCheckDataSource.EventArgs] &&
                                     _enabledDataSources[(int)BuildCheckDataSource.BuildExecution];

        /// <summary>
        /// Notifies the manager that the data source will be used -
        ///   so it should register the built-in checks for the source if it hasn't been done yet.
        /// </summary>
        /// <param name="buildCheckDataSource"></param>
        public void SetDataSource(BuildCheckDataSource buildCheckDataSource)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (!_enabledDataSources[(int)buildCheckDataSource])
            {
                _enabledDataSources[(int)buildCheckDataSource] = true;
                RegisterBuiltInChecks(buildCheckDataSource);
            } 
            stopwatch.Stop();
            _tracingReporter.AddSetDataSourceStats(stopwatch.Elapsed);
        }

        public void ProcessCheckAcquisition(
            CheckAcquisitionData acquisitionData,
            ICheckContext checkContext)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (IsInProcNode)
            {
                var checksFactories = _acquisitionModule.CreateCheckFactories(acquisitionData, checkContext);
                if (checksFactories.Count != 0)
                {
                    RegisterCustomCheck(acquisitionData.ProjectPath, BuildCheckDataSource.EventArgs, checksFactories, checkContext);
                }
                else
                {
                    checkContext.DispatchAsComment(MessageImportance.Normal, "CustomCheckFailedAcquisition", acquisitionData.AssemblyPath);
                }
            }
            else
            {
                BuildCheckAcquisitionEventArgs eventArgs = acquisitionData.ToBuildEventArgs();
                eventArgs.BuildEventContext = checkContext.BuildEventContext!;

                checkContext.DispatchBuildEvent(eventArgs);
            }

            stopwatch.Stop();
            _tracingReporter.AddAcquisitionStats(stopwatch.Elapsed);
        }

        private static T Construct<T>() where T : new() => new();

        private static readonly (string[] ruleIds, bool defaultEnablement, CheckFactory factory)[][] s_builtInFactoriesPerDataSource =
        [

            // BuildCheckDataSource.EventArgs
            [
                ([SharedOutputPathCheck.SupportedRule.Id], SharedOutputPathCheck.SupportedRule.DefaultConfiguration.IsEnabled ?? false, Construct<SharedOutputPathCheck>),
                ([DoubleWritesCheck.SupportedRule.Id], DoubleWritesCheck.SupportedRule.DefaultConfiguration.IsEnabled ?? false, Construct<DoubleWritesCheck>),
                ([NoEnvironmentVariablePropertyCheck.SupportedRule.Id], NoEnvironmentVariablePropertyCheck.SupportedRule.DefaultConfiguration.IsEnabled ?? false, Construct<NoEnvironmentVariablePropertyCheck>)
            ],

            // BuildCheckDataSource.Execution
            []
        ];

        /// <summary>
        /// For tests only. TODO: Remove when check acquisition is done.
        /// </summary>
        internal static (string[] ruleIds, bool defaultEnablement, CheckFactory factory)[][]? s_testFactoriesPerDataSource;

        private void RegisterBuiltInChecks(BuildCheckDataSource buildCheckDataSource)
        {
            _checkRegistry.AddRange(
                s_builtInFactoriesPerDataSource[(int)buildCheckDataSource]
                    .Select(v => new CheckFactoryContext(v.factory, v.ruleIds, v.defaultEnablement)));

            if (s_testFactoriesPerDataSource is not null)
            {
                _checkRegistry.AddRange(
                    s_testFactoriesPerDataSource[(int)buildCheckDataSource]
                        .Select(v => new CheckFactoryContext(v.factory, v.ruleIds, v.defaultEnablement)));
            }
        }

        /// <summary>
        /// To be used by acquisition module
        /// Registers the custom check, the construction of check is needed during registration.
        /// </summary>
        /// <param name="projectPath">The project path is used for the correct .editorconfig resolution.</param>
        /// <param name="buildCheckDataSource">Represents different data sources used in build check operations.</param>
        /// <param name="factories">A collection of build check factories for rules instantiation.</param>
        /// <param name="checkContext">The logging context of the build event.</param>
        internal void RegisterCustomCheck(
            string projectPath,
            BuildCheckDataSource buildCheckDataSource,
            IEnumerable<CheckFactory> factories,
            ICheckContext checkContext)
        {
            if (_enabledDataSources[(int)buildCheckDataSource])
            {
                foreach (var factory in factories)
                {
                    var instance = factory();
                    var checkFactoryContext = new CheckFactoryContext(
                        factory,
                        instance.SupportedRules.Select(r => r.Id).ToArray(),
                        instance.SupportedRules.Any(r => r.DefaultConfiguration.IsEnabled == true));

                    if (checkFactoryContext != null)
                    {
                        _checkRegistry.Add(checkFactoryContext);
                        SetupSingleCheck(checkFactoryContext, projectPath);
                        checkContext.DispatchAsComment(MessageImportance.Normal, "CustomCheckSuccessfulAcquisition", instance.FriendlyName);
                    }
                }
            }
        }

        private void SetupSingleCheck(CheckFactoryContext checkFactoryContext, string projectFullPath)
        {
            // For custom checks - it should run only on projects where referenced
            // (otherwise error out - https://github.com/orgs/dotnet/projects/373/views/1?pane=issue&itemId=57849480)
            // on others it should work similarly as disabling them.
            // Disabled check should not only post-filter results - it shouldn't even see the data 
            CheckWrapper wrapper;
            CheckConfigurationEffective[] configurations;
            if (checkFactoryContext.MaterializedCheck == null)
            {
                CheckConfiguration[] userConfigs =
                    _configurationProvider.GetUserConfigurations(projectFullPath, checkFactoryContext.RuleIds);

                if (userConfigs.All(c => !(c.IsEnabled ?? checkFactoryContext.IsEnabledByDefault)))
                {
                    // the check was not yet instantiated nor mounted - so nothing to do here now.
                    return;
                }

                CustomConfigurationData[] customConfigData =
                    _configurationProvider.GetCustomConfigurations(projectFullPath, checkFactoryContext.RuleIds);

                Check uninitializedCheck = checkFactoryContext.Factory();
                configurations = _configurationProvider.GetMergedConfigurations(userConfigs, uninitializedCheck);

                ConfigurationContext configurationContext = ConfigurationContext.FromDataEnumeration(customConfigData, configurations);

                wrapper = checkFactoryContext.Initialize(uninitializedCheck, configurationContext);
                checkFactoryContext.MaterializedCheck = wrapper;
                Check check = wrapper.Check;

                // This is to facilitate possible perf improvement for custom checks - as we might want to
                //  avoid loading the assembly and type just to check if it's supported.
                // If we expose a way to declare the enablement status and rule ids during registration (e.g. via
                //  optional arguments of the intrinsic property function) - we can then avoid loading it.
                // But once loaded - we should verify that the declared enablement status and rule ids match the actual ones.
                if (
                    check.SupportedRules.Count != checkFactoryContext.RuleIds.Length
                    ||
                    !check.SupportedRules.Select(r => r.Id)
                        .SequenceEqual(checkFactoryContext.RuleIds, StringComparer.CurrentCultureIgnoreCase)
                )
                {
                    throw new BuildCheckConfigurationException(
                        $"The check '{check.FriendlyName}' exposes rules '{check.SupportedRules.Select(r => r.Id).ToCsvString()}', but different rules were declared during registration: '{checkFactoryContext.RuleIds.ToCsvString()}'");
                }

                // technically all checks rules could be disabled, but that would mean
                // that the provided 'IsEnabledByDefault' value wasn't correct - the only
                // price to be paid in that case is slight performance cost.

                // Create the wrapper and register to central context
                wrapper.StartNewProject(projectFullPath, configurations);
                var wrappedContext = new BuildCheckRegistrationContext(wrapper, _buildCheckCentralContext);
                check.RegisterActions(wrappedContext);
            }
            else
            {
                wrapper = checkFactoryContext.MaterializedCheck;

                configurations = _configurationProvider.GetMergedConfigurations(projectFullPath, wrapper.Check);

                _configurationProvider.CheckCustomConfigurationDataValidity(projectFullPath,
                    checkFactoryContext.RuleIds[0]);

                // Update the wrapper
                wrapper.StartNewProject(projectFullPath, configurations);
            }

            if (configurations.GroupBy(c => c.EvaluationCheckScope).Count() > 1)
            {
                throw new BuildCheckConfigurationException(
                    string.Format("All rules for a single check should have the same EvaluationCheckScope for a single project (violating rules: [{0}], project: {1})",
                        checkFactoryContext.RuleIds.ToCsvString(),
                        projectFullPath));
            }
        }

        private void SetupChecksForNewProject(string projectFullPath, ICheckContext checkContext)
        {
            // Only add checks here
            // On an execution node - we might remove and dispose the checks once project is done

            // If it's already constructed - just control the custom settings do not differ
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<CheckFactoryContext> checksToRemove = new();
            foreach (CheckFactoryContext checkFactoryContext in _checkRegistry)
            {
                try
                {
                    SetupSingleCheck(checkFactoryContext, projectFullPath);
                }
                catch (BuildCheckConfigurationException e)
                {
                    checkContext.DispatchAsErrorFromText(
                        null,
                        null,
                        null,
                        new BuildEventFileInfo(projectFullPath),
                        e.Message);
                    checksToRemove.Add(checkFactoryContext);
                }
            }

            checksToRemove.ForEach(c =>
            {
                _checkRegistry.Remove(c);
                checkContext.DispatchAsCommentFromText(MessageImportance.High, $"Dismounting check '{c.FriendlyName}'");
            });
            foreach (var checkToRemove in checksToRemove.Select(a => a.MaterializedCheck).Where(a => a != null))
            {
                _buildCheckCentralContext.DeregisterCheck(checkToRemove!);
                _tracingReporter.AddCheckStats(checkToRemove!.Check.FriendlyName, checkToRemove.Elapsed);
                checkToRemove.Check.Dispose();
            }

            stopwatch.Stop();
            _tracingReporter.AddNewProjectStats(stopwatch.Elapsed);
        }

        public void ProcessEvaluationFinishedEventArgs(
            ICheckContext checkContext,
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
                .ProcessEvaluationFinishedEventArgs(checkContext, evaluationFinishedEventArgs, propertiesLookup);
        }

        public void ProcessEnvironmentVariableReadEventArgs(ICheckContext checkContext, EnvironmentVariableReadEventArgs projectEvaluationEventArgs)
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
            ICheckContext checkContext,
            TaskStartedEventArgs taskStartedEventArgs)
            => _buildEventsProcessor
                .ProcessTaskStartedEventArgs(checkContext, taskStartedEventArgs);

        public void ProcessTaskFinishedEventArgs(
            ICheckContext checkContext,
            TaskFinishedEventArgs taskFinishedEventArgs)
            => _buildEventsProcessor
                .ProcessTaskFinishedEventArgs(checkContext, taskFinishedEventArgs);

        public void ProcessTaskParameterEventArgs(
            ICheckContext checkContext,
            TaskParameterEventArgs taskParameterEventArgs)
            => _buildEventsProcessor
                .ProcessTaskParameterEventArgs(checkContext, taskParameterEventArgs);

        public Dictionary<string, TimeSpan> CreateCheckTracingStats()
        {
            foreach (CheckFactoryContext checkFactoryContext in _checkRegistry)
            {
                if (checkFactoryContext.MaterializedCheck != null)
                {
                    _tracingReporter.AddCheckStats(checkFactoryContext.FriendlyName, checkFactoryContext.MaterializedCheck.Elapsed);
                    checkFactoryContext.MaterializedCheck.ClearStats();
                }
            }

            _tracingReporter.AddCheckInfraStats();
            return _tracingReporter.TracingStats;
        }

        public void FinalizeProcessing(LoggingContext loggingContext)
        {
            if (IsInProcNode)
            {
                // We do not want to send tracing stats from in-proc node
                return;
            }

            var checkEventStats = CreateCheckTracingStats();

            BuildCheckTracingEventArgs checkEventArg =
                new(checkEventStats) { BuildEventContext = loggingContext.BuildEventContext };
            loggingContext.LogBuildEvent(checkEventArg);
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
            ICheckContext checkContext,
            string projectFullPath)
        {
            if (buildCheckDataSource == BuildCheckDataSource.EventArgs && IsInProcNode)
            {
                // Skipping this event - as it was already handled by the in-proc node.
                // This is because in-proc node has the BuildEventArgs source and BuildExecution source
                //  both in a single manager. The project started is first encountered by the execution before the EventArg is sent
                return;
            }

            SetupChecksForNewProject(projectFullPath, checkContext);
            _projectsByContextId[checkContext.BuildEventContext.ProjectContextId] = projectFullPath;
        }

        /*
         *
         * Following methods are for future use (should we decide to approach in-execution check)
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
            ICheckContext checkContext,
            string projectFullPath)
        {
            _buildEventsProcessor.ProcessProjectDone(checkContext, projectFullPath);
            _projectsByContextId.TryRemove(checkContext.BuildEventContext.ProjectContextId, out _);
        }

        public void ProcessPropertyRead(PropertyReadInfo propertyReadInfo, CheckLoggingContext checkContext)
        {
            if (!_buildCheckCentralContext.HasPropertyReadActions)
            {
                return;
            }

            PropertyReadData propertyReadData = new(
                GetProjectFullPath(checkContext.BuildEventContext),
                checkContext.BuildEventContext.ProjectInstanceId,
                propertyReadInfo);
            _buildEventsProcessor.ProcessPropertyRead(propertyReadData, checkContext);
        }

        public void ProcessPropertyWrite(PropertyWriteInfo propertyWriteInfo, CheckLoggingContext checkContext)
        {
            if (!_buildCheckCentralContext.HasPropertyWriteActions)
            {
                return;
            }

            PropertyWriteData propertyWriteData = new(
                GetProjectFullPath(checkContext.BuildEventContext),
                checkContext.BuildEventContext.ProjectInstanceId,
                propertyWriteInfo);
            _buildEventsProcessor.ProcessPropertyWrite(propertyWriteData, checkContext);
        }

        public void Shutdown()
        { /* Too late here for any communication to the main node or for logging anything */ }

        private class CheckFactoryContext(
            CheckFactory factory,
            string[] ruleIds,
            bool isEnabledByDefault)
        {
            public Check Factory()
            {
                Check ba = factory();
                return ba;
            }

            public CheckWrapper Initialize(Check ba, ConfigurationContext configContext)
            {
                ba.Initialize(configContext);
                return new CheckWrapper(ba);
            }

            public CheckWrapper? MaterializedCheck { get; set; }

            public string[] RuleIds { get; init; } = ruleIds;

            public bool IsEnabledByDefault { get; init; } = isEnabledByDefault;

            public string FriendlyName => MaterializedCheck?.Check.FriendlyName ?? factory().FriendlyName;
        }
    }
}
