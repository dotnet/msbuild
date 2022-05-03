// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Eventing;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Experimental.ProjectCache
{
    internal record CacheRequest(BuildSubmission Submission, BuildRequestConfiguration Configuration);

    internal record NullableBool(bool Value)
    {
        public static implicit operator bool(NullableBool? d) => d is not null && d.Value;
    }

    internal enum ProjectCacheServiceState
    {
        NotInitialized,
        BeginBuildStarted,
        BeginBuildFinished,
        ShutdownStarted,
        ShutdownFinished
    }

    internal class ProjectCacheService
    {
        private readonly BuildManager _buildManager;
        private readonly ILoggingService _loggingService;
        private readonly ProjectCacheDescriptor _projectCacheDescriptor;
        private readonly CancellationToken _cancellationToken;
        private readonly ProjectCachePluginBase _projectCachePlugin;
        private readonly string _projectCachePluginTypeName;
        private ProjectCacheServiceState _serviceState = ProjectCacheServiceState.NotInitialized;

        /// <summary>
        /// An instanatiable version of MSBuildFileSystemBase not overriding any methods,
        /// i.e. falling back to FileSystem.Default.
        /// </summary>
        private sealed class DefaultMSBuildFileSystem : MSBuildFileSystemBase { }

        // Use NullableBool to make it work with Interlock.CompareExchange (doesn't accept bool?).
        // Assume that if one request is a design time build, all of them are.
        // Volatile because it is read by the BuildManager thread and written by one project cache service thread pool thread.
        // TODO: remove after we change VS to set the cache descriptor via build parameters.
        public volatile NullableBool? DesignTimeBuildsDetected;
        private TaskCompletionSource<bool>? LateInitializationForVSWorkaroundCompleted;

        private ProjectCacheService(
            ProjectCachePluginBase projectCachePlugin,
            string pluginTypeName,
            BuildManager buildManager,
            ILoggingService loggingService,
            ProjectCacheDescriptor projectCacheDescriptor,
            CancellationToken cancellationToken
        )
        {
            _projectCachePlugin = projectCachePlugin;
            _projectCachePluginTypeName = pluginTypeName;
            _buildManager = buildManager;
            _loggingService = loggingService;
            _projectCacheDescriptor = projectCacheDescriptor;
            _cancellationToken = cancellationToken;
        }

        public static async Task<ProjectCacheService> FromDescriptorAsync(
            ProjectCacheDescriptor pluginDescriptor,
            BuildManager buildManager,
            ILoggingService loggingService,
            CancellationToken cancellationToken)
        {
            (ProjectCachePluginBase plugin, string pluginTypeName) = await Task.Run(() => GetPluginInstance(pluginDescriptor), cancellationToken)
                .ConfigureAwait(false);

            var service = new ProjectCacheService(plugin, pluginTypeName, buildManager, loggingService, pluginDescriptor, cancellationToken);

            // TODO: remove the if after we change VS to set the cache descriptor via build parameters and always call BeginBuildAsync in FromDescriptorAsync.
            // When running under VS we can't initialize the plugin until we evaluate a project (any project) and extract
            // further information (set by VS) from it required by the plugin.
            if (!pluginDescriptor.VsWorkaround)
            {
                await service.BeginBuildAsync();
            }

            return service;
        }

        // TODO: remove vsWorkaroundOverrideDescriptor after we change VS to set the cache descriptor via build parameters.
        private async Task BeginBuildAsync(ProjectCacheDescriptor? vsWorkaroundOverrideDescriptor = null)
        {
            BuildEventContext buildEventContext = BuildEventContext.Invalid;
            BuildEventFileInfo buildEventFileInfo = BuildEventFileInfo.Empty;
            var pluginLogger = new LoggingServiceToPluginLoggerAdapter(
                _loggingService,
                buildEventContext,
                buildEventFileInfo);
            ProjectCacheDescriptor projectDescriptor = vsWorkaroundOverrideDescriptor ?? _projectCacheDescriptor;

            try
            {
                SetState(ProjectCacheServiceState.BeginBuildStarted);
                _loggingService.LogComment(buildEventContext, MessageImportance.Low, "ProjectCacheBeginBuild");
                MSBuildEventSource.Log.ProjectCacheBeginBuildStart(_projectCachePluginTypeName);

                await _projectCachePlugin.BeginBuildAsync(
                    new CacheContext(
                        projectDescriptor.PluginSettings,
                        new DefaultMSBuildFileSystem(),
                        projectDescriptor.ProjectGraph,
                        projectDescriptor.EntryPoints),
                    pluginLogger,
                    _cancellationToken);
            }
            catch (Exception e)
            {
                HandlePluginException(e, nameof(ProjectCachePluginBase.BeginBuildAsync));
            }
            finally
            {
                MSBuildEventSource.Log.ProjectCacheBeginBuildStop(_projectCachePluginTypeName);
                SetState(ProjectCacheServiceState.BeginBuildFinished);
            }

            if (pluginLogger.HasLoggedErrors)
            {
                ProjectCacheException.ThrowForErrorLoggedInsideTheProjectCache("ProjectCacheInitializationFailed");
            }
        }

        private static (ProjectCachePluginBase PluginInstance, string PluginTypeName) GetPluginInstance(ProjectCacheDescriptor pluginDescriptor)
        {
            if (pluginDescriptor.PluginInstance != null)
            {
                return (pluginDescriptor.PluginInstance, pluginDescriptor.PluginInstance.GetType().Name);
            }

            if (pluginDescriptor.PluginAssemblyPath != null)
            {
                MSBuildEventSource.Log.ProjectCacheCreatePluginInstanceStart(pluginDescriptor.PluginAssemblyPath);
                Type pluginType = GetTypeFromAssemblyPath(pluginDescriptor.PluginAssemblyPath);
                ProjectCachePluginBase pluginInstance = GetPluginInstanceFromType(pluginType);
                MSBuildEventSource.Log.ProjectCacheCreatePluginInstanceStop(pluginDescriptor.PluginAssemblyPath, pluginType.Name);
                return (pluginInstance, pluginType.Name);
            }

            ErrorUtilities.ThrowInternalErrorUnreachable();
            return (null!, null!); // Unreachable
        }

        private static ProjectCachePluginBase GetPluginInstanceFromType(Type pluginType)
        {
            try
            {
                return (ProjectCachePluginBase) Activator.CreateInstance(pluginType)!;
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                HandlePluginException(e.InnerException, "Constructor");
                return null!; // Unreachable
            }
        }

        private static Type GetTypeFromAssemblyPath(string pluginAssemblyPath)
        {
            var assembly = LoadAssembly(pluginAssemblyPath);

            var type = GetTypes<ProjectCachePluginBase>(assembly).FirstOrDefault();

            if (type == null)
            {
                ProjectCacheException.ThrowForMSBuildIssueWithTheProjectCache("NoProjectCachePluginFoundInAssembly", pluginAssemblyPath);
            }

            return type!;

            Assembly LoadAssembly(string resolverPath)
            {
#if !FEATURE_ASSEMBLYLOADCONTEXT
                return Assembly.LoadFrom(resolverPath);
#else
                return s_loader.LoadFromPath(resolverPath);
#endif
            }

            IEnumerable<Type> GetTypes<T>(Assembly assembly)
            {
                return assembly.ExportedTypes
                    .Select(type => new {type, info = type.GetTypeInfo()})
                    .Where(
                        t => t.info.IsClass &&
                             t.info.IsPublic &&
                             !t.info.IsAbstract &&
                             typeof(T).IsAssignableFrom(t.type))
                    .Select(t => t.type);
            }
        }

#if FEATURE_ASSEMBLYLOADCONTEXT
        private static readonly CoreClrAssemblyLoader s_loader = new CoreClrAssemblyLoader();
#endif

        public void PostCacheRequest(CacheRequest cacheRequest)
        {
            Task.Run(async () =>
            {
                try
                {
                    (CacheResult cacheResult, int projectContextId) = await ProcessCacheRequest(cacheRequest);
                    _buildManager.PostCacheResult(cacheRequest, cacheResult, projectContextId);
                }
                catch (Exception e)
                {
                    _buildManager.PostCacheResult(cacheRequest, CacheResult.IndicateException(e), BuildEventContext.InvalidProjectContextId);
                }
            }, _cancellationToken);

            async Task<(CacheResult Result, int ProjectContextId)> ProcessCacheRequest(CacheRequest request)
            {
                // Prevent needless evaluation if design time builds detected.
                if (_projectCacheDescriptor.VsWorkaround && DesignTimeBuildsDetected)
                {
                    // The BuildManager should disable the cache when it finds its servicing design time builds.
                    return (CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss), BuildEventContext.InvalidProjectContextId);
                }

                EvaluateProjectIfNecessary(request);

                // Detect design time builds.
                if (_projectCacheDescriptor.VsWorkaround)
                {
                    var isDesignTimeBuild = IsDesignTimeBuild(request.Configuration.Project);

                    var previousValue = Interlocked.CompareExchange(
                        ref DesignTimeBuildsDetected,
                        new NullableBool(isDesignTimeBuild),
                        null);

                    ErrorUtilities.VerifyThrowInternalError(
                        previousValue is null || previousValue == false || isDesignTimeBuild,
                        "Either all builds in a build session or design time builds, or none");

                    // No point progressing with expensive plugin initialization or cache query if design time build detected.
                    if (DesignTimeBuildsDetected)
                    {
                        // The BuildManager should disable the cache when it finds its servicing design time builds.
                        return (CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss), BuildEventContext.InvalidProjectContextId);
                    }
                }

                // TODO: remove after we change VS to set the cache descriptor via build parameters.
                // VS workaround needs to wait until the first project is evaluated to extract enough information to initialize the plugin.
                // No cache request can progress until late initialization is complete.
                if (_projectCacheDescriptor.VsWorkaround)
                {
                    if (Interlocked.CompareExchange(
                            ref LateInitializationForVSWorkaroundCompleted,
                            new TaskCompletionSource<bool>(),
                            null) is null)
                    {
                        await LateInitializePluginForVsWorkaround(request);
                        LateInitializationForVSWorkaroundCompleted.SetResult(true);
                    }
                    else
                    {
                        // Can't be null. If the thread got here it means another thread initialized the completion source.
                        await LateInitializationForVSWorkaroundCompleted!.Task;
                    }
                }

                ErrorUtilities.VerifyThrowInternalError(
                    LateInitializationForVSWorkaroundCompleted is null ||
                    (_projectCacheDescriptor.VsWorkaround && LateInitializationForVSWorkaroundCompleted.Task.IsCompleted),
                    "Completion source should be null when this is not the VS workaround");

                BuildRequestData buildRequest = new BuildRequestData(
                    cacheRequest.Configuration.Project,
                    cacheRequest.Submission.BuildRequestData.TargetNames.ToArray());
                BuildEventContext buildEventContext = _loggingService.CreateProjectCacheBuildEventContext(
                    cacheRequest.Submission.SubmissionId,
                    evaluationId: cacheRequest.Configuration.Project.EvaluationId,
                    projectInstanceId: cacheRequest.Configuration.ConfigurationId,
                    projectFile: cacheRequest.Configuration.Project.FullPath);

                CacheResult cacheResult;
                try
                {
                    cacheResult = await GetCacheResultAsync(buildRequest, cacheRequest.Configuration, buildEventContext);
                }
                catch (Exception ex)
                {
                    // Wrap the exception here so we can preserve the ProjectContextId
                    cacheResult = CacheResult.IndicateException(ex);
                }

                return (cacheResult, buildEventContext.ProjectContextId);
            }

            static bool IsDesignTimeBuild(ProjectInstance project)
            {
                var designTimeBuild = project.GetPropertyValue(DesignTimeProperties.DesignTimeBuild);
                var buildingProject = project.GlobalPropertiesDictionary[DesignTimeProperties.BuildingProject]?.EvaluatedValue;

                return MSBuildStringIsTrue(designTimeBuild) ||
                       (buildingProject != null && !MSBuildStringIsTrue(buildingProject));
            }

            void EvaluateProjectIfNecessary(CacheRequest request)
            {
                // TODO: only do this if the project cache requests evaluation. QB needs evaluations, but the Anybuild implementation
                // TODO: might not need them, so no point evaluating if it's not necessary. As a caveat, evaluations would still be optimal
                // TODO: when proxy builds are issued by the plugin ( scheduled on the inproc node, no point re-evaluating on out-of-proc nodes).
                lock (request.Configuration)
                {
                    if (!request.Configuration.IsLoaded)
                    {
                        request.Configuration.LoadProjectIntoConfiguration(
                            _buildManager,
                            request.Submission.BuildRequestData.Flags,
                            request.Submission.SubmissionId,
                            Scheduler.InProcNodeId
                        );

                        // If we're taking the time to evaluate, avoid having other nodes to repeat the same evaluation.
                        // Based on the assumption that ProjectInstance serialization is faster than evaluating from scratch.
                        request.Configuration.Project.TranslateEntireState = true;
                    }
                }
            }

            async Task LateInitializePluginForVsWorkaround(CacheRequest request)
            {
                var (_, configuration) = request;
                var solutionPath = configuration.Project.GetPropertyValue(SolutionProjectGenerator.SolutionPathPropertyName);
                var solutionConfigurationXml = configuration.Project.GetPropertyValue(SolutionProjectGenerator.CurrentSolutionConfigurationContents);

                ErrorUtilities.VerifyThrow(
                    solutionPath != null && !string.IsNullOrWhiteSpace(solutionPath) && solutionPath != "*Undefined*",
                    $"Expected VS to set a valid SolutionPath property but got: {solutionPath}");

                ErrorUtilities.VerifyThrow(
                    FileSystems.Default.FileExists(solutionPath),
                    $"Solution file does not exist: {solutionPath}");

                ErrorUtilities.VerifyThrow(
                    string.IsNullOrWhiteSpace(solutionConfigurationXml) is false,
                    "Expected VS to set a xml with all the solution projects' configurations for the currently building solution configuration.");

                // A solution supports multiple solution configurations (different values for Configuration and Platform).
                // Each solution configuration generates a different static graph.
                // Therefore, plugin implementations that rely on creating static graphs in their BeginBuild methods need access to the
                // currently solution configuration used by VS.
                //
                // In this VS workaround, however, we do not have access to VS' solution configuration. The only information we have is a global property
                // named "CurrentSolutionConfigurationContents" that VS sets on each built project. It does not contain the solution configuration itself, but
                // instead it contains information on how the solution configuration maps to each project's configuration.
                //
                // So instead of using the solution file as the entry point, we parse this VS property and extract graph entry points from it, for every project
                // mentioned in the "CurrentSolutionConfigurationContents" global property.
                //
                // Ideally, when the VS workaround is removed from MSBuild and moved into VS, VS should create ProjectGraphDescriptors with the solution path as
                // the graph entrypoint file, and the VS solution configuration as the entry point's global properties.
                var graphEntryPointsFromSolutionConfig = GenerateGraphEntryPointsFromSolutionConfigurationXml(
                    solutionConfigurationXml,
                    configuration.Project);

                await BeginBuildAsync(
                    ProjectCacheDescriptor.FromAssemblyPath(
                        _projectCacheDescriptor.PluginAssemblyPath!,
                        graphEntryPointsFromSolutionConfig,
                        projectGraph: null,
                        _projectCacheDescriptor.PluginSettings));
            }

            static IReadOnlyCollection<ProjectGraphEntryPoint> GenerateGraphEntryPointsFromSolutionConfigurationXml(
                string solutionConfigurationXml,
                ProjectInstance project
            )
            {
                // TODO: fix code clone for parsing CurrentSolutionConfiguration xml: https://github.com/dotnet/msbuild/issues/6751
                var doc = new XmlDocument();
                doc.LoadXml(solutionConfigurationXml);

                var root = doc.DocumentElement!;
                var projectConfigurationNodes = root.GetElementsByTagName("ProjectConfiguration");

                ErrorUtilities.VerifyThrow(projectConfigurationNodes.Count > 0, "Expected at least one project in solution");

                var definingProjectPath = project.FullPath;
                var graphEntryPoints = new List<ProjectGraphEntryPoint>(projectConfigurationNodes.Count);

                var templateGlobalProperties = new Dictionary<string, string>(project.GlobalProperties, StringComparer.OrdinalIgnoreCase);
                RemoveProjectSpecificGlobalProperties(templateGlobalProperties, project);

                foreach (XmlNode node in projectConfigurationNodes)
                {
                    ErrorUtilities.VerifyThrowInternalNull(node.Attributes, nameof(node.Attributes));

                    var buildProjectInSolution = node.Attributes!["BuildProjectInSolution"];
                    if (buildProjectInSolution is not null &&
                        string.IsNullOrWhiteSpace(buildProjectInSolution.Value) is false &&
                        bool.TryParse(buildProjectInSolution.Value, out var buildProject) &&
                        buildProject is false)
                    {
                        continue;
                    }

                    ErrorUtilities.VerifyThrow(
                        node.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.Name == "ProjectDependency") is null,
                        "Project cache service does not support solution only dependencies when running under Visual Studio.");

                    var projectPathAttribute = node.Attributes!["AbsolutePath"];
                    ErrorUtilities.VerifyThrow(projectPathAttribute is not null, "Expected VS to set the project path on each ProjectConfiguration element.");

                    var projectPath = projectPathAttribute!.Value;

                    var (configuration, platform) = SolutionFile.ParseConfigurationName(node.InnerText, definingProjectPath, 0, solutionConfigurationXml);

                    // Take the defining project global properties and override the configuration and platform.
                    // It's sufficient to only set Configuration and Platform.
                    // But we send everything to maximize the plugins' potential to quickly workaround potential MSBuild issues while the MSBuild fixes flow into VS.
                    var globalProperties = new Dictionary<string, string>(templateGlobalProperties, StringComparer.OrdinalIgnoreCase)
                    {
                        ["Configuration"] = configuration,
                        ["Platform"] = platform
                    };

                    graphEntryPoints.Add(new ProjectGraphEntryPoint(projectPath, globalProperties));
                }

                return graphEntryPoints;

                // If any project specific property is set, it will propagate down the project graph and force all nodes to that property's specific side effects, which is incorrect.
                void RemoveProjectSpecificGlobalProperties(Dictionary<string, string> globalProperties, ProjectInstance project)
                {
                    // InnerBuildPropertyName is TargetFramework for the managed sdk.
                    var innerBuildPropertyName = ProjectInterpretation.GetInnerBuildPropertyName(project);

                    IEnumerable<string> projectSpecificPropertyNames = new []{innerBuildPropertyName, "Configuration", "Platform", "TargetPlatform", "OutputType"};

                    foreach (var propertyName in projectSpecificPropertyNames)
                    {
                        if (!string.IsNullOrWhiteSpace(propertyName) && globalProperties.ContainsKey(propertyName))
                        {
                            globalProperties.Remove(propertyName);
                        }
                    }
                }
            }

            static bool MSBuildStringIsTrue(string msbuildString) =>
                ConversionUtilities.ConvertStringToBool(msbuildString, nullOrWhitespaceIsFalse: true);
        }

        private async Task<CacheResult> GetCacheResultAsync(BuildRequestData buildRequest, BuildRequestConfiguration buildRequestConfiguration, BuildEventContext buildEventContext)
        {
            lock (this)
            {
                CheckNotInState(ProjectCacheServiceState.NotInitialized);
                CheckNotInState(ProjectCacheServiceState.BeginBuildStarted);

                if (_serviceState is ProjectCacheServiceState.ShutdownStarted or ProjectCacheServiceState.ShutdownFinished)
                {
                    return CacheResult.IndicateNonCacheHit(CacheResultType.CacheNotApplicable);
                }
            }

            ErrorUtilities.VerifyThrowInternalNull(buildRequest.ProjectInstance, nameof(buildRequest.ProjectInstance));

            var buildEventFileInfo = new BuildEventFileInfo(buildRequest.ProjectFullPath);
            var pluginLogger = new LoggingServiceToPluginLoggerAdapter(
                _loggingService,
                buildEventContext,
                buildEventFileInfo);

            string? targetNames = buildRequest.TargetNames != null && buildRequest.TargetNames.Count > 0
                ? string.Join(", ", buildRequest.TargetNames)
                : null;
            if (string.IsNullOrEmpty(targetNames))
            {
                _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "ProjectCacheQueryStartedWithDefaultTargets", buildRequest.ProjectFullPath);
            }
            else
            {
                _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "ProjectCacheQueryStartedWithTargetNames", buildRequest.ProjectFullPath, targetNames);
            }

            CacheResult? cacheResult = null;
            try
            {
                MSBuildEventSource.Log.ProjectCacheGetCacheResultStart(_projectCachePluginTypeName, buildRequest.ProjectFullPath, targetNames);
                cacheResult = await _projectCachePlugin.GetCacheResultAsync(buildRequest, pluginLogger, _cancellationToken);
            }
            catch (Exception e)
            {
                HandlePluginException(e, nameof(ProjectCachePluginBase.GetCacheResultAsync));
                return null!; // Unreachable
            }
            finally
            {
                if (MSBuildEventSource.Log.IsEnabled())
                {
                    string cacheResultType = cacheResult?.ResultType.ToString() ?? nameof(CacheResultType.None);
                    MSBuildEventSource.Log.ProjectCacheGetCacheResultStop(_projectCachePluginTypeName, buildRequest.ProjectFullPath, targetNames, cacheResultType);
                }
            }

            if (pluginLogger.HasLoggedErrors || cacheResult.ResultType == CacheResultType.None)
            {
                ProjectCacheException.ThrowForErrorLoggedInsideTheProjectCache("ProjectCacheQueryFailed", buildRequest.ProjectFullPath);
            }

            switch (cacheResult.ResultType)
            {
                case CacheResultType.CacheHit:
                    if (string.IsNullOrEmpty(targetNames))
                    {
                        _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "ProjectCacheHitWithDefaultTargets", buildRequest.ProjectFullPath);
                    }
                    else
                    {
                        _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "ProjectCacheHitWithTargetNames", buildRequest.ProjectFullPath, targetNames);
                    }

                    // Similar to CopyFilesToOutputDirectory from Microsoft.Common.CurrentVersion.targets, so that progress can be seen.
                    // TODO: This should be indented by the console logger. That requires making these log events structured.
                    if (!buildRequestConfiguration.IsTraversal)
                    {
                        _loggingService.LogComment(buildEventContext, MessageImportance.High, "ProjectCacheHitWithOutputs", buildRequest.ProjectInstance.GetPropertyValue(ReservedPropertyNames.projectName));
                    }

                    break;
                case CacheResultType.CacheMiss:
                    if (string.IsNullOrEmpty(targetNames))
                    {
                        _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "ProjectCacheMissWithDefaultTargets", buildRequest.ProjectFullPath);
                    }
                    else
                    {
                        _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "ProjectCacheMissWithTargetNames", buildRequest.ProjectFullPath, targetNames);
                    }

                    break;
                case CacheResultType.CacheNotApplicable:
                    if (string.IsNullOrEmpty(targetNames))
                    {
                        _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "ProjectCacheNotApplicableWithDefaultTargets", buildRequest.ProjectFullPath);
                    }
                    else
                    {
                        _loggingService.LogComment(buildEventContext, MessageImportance.Normal, "ProjectCacheNotApplicableWithTargetNames", buildRequest.ProjectFullPath, targetNames);
                    }

                    break;
                case CacheResultType.None: // Should not get here based on the throw above
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return cacheResult;
        }

        public async Task ShutDown()
        {
            bool shouldInitiateShutdownState = _serviceState != ProjectCacheServiceState.ShutdownStarted && _serviceState != ProjectCacheServiceState.ShutdownFinished;

            if (!shouldInitiateShutdownState)
            {
                return;
            }

            BuildEventContext buildEventContext = BuildEventContext.Invalid;
            BuildEventFileInfo buildEventFileInfo = BuildEventFileInfo.Empty;
            var pluginLogger = new LoggingServiceToPluginLoggerAdapter(
                _loggingService,
                BuildEventContext.Invalid,
                BuildEventFileInfo.Empty);
            
            try
            {
                SetState(ProjectCacheServiceState.ShutdownStarted);
                _loggingService.LogComment(buildEventContext, MessageImportance.Low, "ProjectCacheEndBuild");
                MSBuildEventSource.Log.ProjectCacheEndBuildStart(_projectCachePluginTypeName);

                await _projectCachePlugin.EndBuildAsync(pluginLogger, _cancellationToken);

                if (pluginLogger.HasLoggedErrors)
                {
                    ProjectCacheException.ThrowForErrorLoggedInsideTheProjectCache("ProjectCacheShutdownFailed");
                }
            }
            catch (Exception e) when (e is not ProjectCacheException)
            {
                HandlePluginException(e, nameof(ProjectCachePluginBase.EndBuildAsync));
            }
            finally
            {
                MSBuildEventSource.Log.ProjectCacheEndBuildStop(_projectCachePluginTypeName);
                SetState(ProjectCacheServiceState.ShutdownFinished);
            }
        }

        private static void HandlePluginException(Exception e, string apiExceptionWasThrownFrom)
        {
            if (ExceptionHandling.IsCriticalException(e))
            {
                throw e;
            }

            ProjectCacheException.ThrowAsUnhandledException(
                e,
                "ProjectCacheException",
                apiExceptionWasThrownFrom);
        }

        private void SetState(ProjectCacheServiceState newState)
        {
            lock (this)
            {
                switch (newState)
                {
                    case ProjectCacheServiceState.NotInitialized:
                        ErrorUtilities.ThrowInternalError($"Cannot transition to {ProjectCacheServiceState.NotInitialized}");
                        break;
                    case ProjectCacheServiceState.BeginBuildStarted:
                        CheckInState(ProjectCacheServiceState.NotInitialized);
                        break;
                    case ProjectCacheServiceState.BeginBuildFinished:
                        CheckInState(ProjectCacheServiceState.BeginBuildStarted);
                        break;
                    case ProjectCacheServiceState.ShutdownStarted:
                        CheckNotInState(ProjectCacheServiceState.ShutdownStarted);
                        CheckNotInState(ProjectCacheServiceState.ShutdownFinished);
                        break;
                    case ProjectCacheServiceState.ShutdownFinished:
                        CheckInState(ProjectCacheServiceState.ShutdownStarted);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
                }

                _serviceState = newState;
            }
        }

        private void CheckInState(ProjectCacheServiceState expectedState)
        {
            lock (this)
            {
                ErrorUtilities.VerifyThrowInternalError(_serviceState == expectedState, $"Expected state {expectedState}, actual state {_serviceState}");
            }
        }

        private void CheckNotInState(ProjectCacheServiceState unexpectedState)
        {
            lock (this)
            {
                ErrorUtilities.VerifyThrowInternalError(_serviceState != unexpectedState, $"Unexpected state {_serviceState}");
            }
        }

        private class LoggingServiceToPluginLoggerAdapter : PluginLoggerBase
        {
            private readonly ILoggingService _loggingService;

            private readonly BuildEventContext _buildEventContext;

            private readonly BuildEventFileInfo _buildEventFileInfo;

            public override bool HasLoggedErrors { get; protected set; }

            public LoggingServiceToPluginLoggerAdapter(
                ILoggingService loggingService,
                BuildEventContext buildEventContext,
                BuildEventFileInfo buildEventFileInfo)
            {
                _loggingService = loggingService;
                _buildEventContext = buildEventContext;
                _buildEventFileInfo = buildEventFileInfo;
            }

            public override void LogMessage(string message, MessageImportance? messageImportance = null)
            {
                _loggingService.LogCommentFromText(
                    _buildEventContext,
                    messageImportance ?? MessageImportance.Normal,
                    message);
            }

            public override void LogWarning(string warning)
            {
                _loggingService.LogWarningFromText(
                    _buildEventContext,
                    subcategoryResourceName: null,
                    warningCode: null,
                    helpKeyword: null,
                    _buildEventFileInfo,
                    warning);
            }

            public override void LogError(string error)
            {
                HasLoggedErrors = true;

                _loggingService.LogErrorFromText(
                    _buildEventContext,
                    subcategoryResourceName: null,
                    errorCode: null,
                    helpKeyword: null,
                    _buildEventFileInfo,
                    error);
            }
        }
    }
}
