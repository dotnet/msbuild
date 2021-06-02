// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Experimental.ProjectCache
{
    internal record CacheRequest(BuildSubmission Submission, BuildRequestConfiguration Configuration);

    internal record NullableBool(bool Value)
    {
        public static implicit operator bool(NullableBool? d) => d is not null && d.Value;
    }

    internal class ProjectCacheService
    {
        private readonly BuildManager _buildManager;
        private readonly Func<PluginLoggerBase> _loggerFactory;
        private readonly ProjectCacheDescriptor _projectCacheDescriptor;
        private readonly CancellationToken _cancellationToken;
        private readonly ProjectCachePluginBase _projectCachePlugin;

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

        private ProjectCacheService(
            ProjectCachePluginBase projectCachePlugin,
            BuildManager buildManager,
            Func<PluginLoggerBase> loggerFactory,
            ProjectCacheDescriptor projectCacheDescriptor,
            CancellationToken cancellationToken
        )
        {
            _projectCachePlugin = projectCachePlugin;
            _buildManager = buildManager;
            _loggerFactory = loggerFactory;
            _projectCacheDescriptor = projectCacheDescriptor;
            _cancellationToken = cancellationToken;
        }

        public static async Task<ProjectCacheService> FromDescriptorAsync(
            ProjectCacheDescriptor pluginDescriptor,
            BuildManager buildManager,
            ILoggingService loggingService,
            CancellationToken cancellationToken)
        {
            var plugin = await Task.Run(() => GetPluginInstance(pluginDescriptor), cancellationToken)
                .ConfigureAwait(false);

            // TODO: Detect and use the highest verbosity from all the user defined loggers. That's tricky because right now we can't query loggers about
            // their verbosity levels.
            var loggerFactory = new Func<PluginLoggerBase>(() => new LoggingServiceToPluginLoggerAdapter(LoggerVerbosity.Normal, loggingService));

            // TODO: remove after we change VS to set the cache descriptor via build parameters.
            if (pluginDescriptor.VsWorkaround)
            {
                // When running under VS we can't initialize the plugin until we evaluate a project (any project) and extract
                // further information (set by VS) from it required by the plugin.
                return new ProjectCacheService(plugin, buildManager, loggerFactory, pluginDescriptor, cancellationToken);
            }

            await InitializePlugin(pluginDescriptor, cancellationToken, loggerFactory, plugin);

            return new ProjectCacheService(plugin, buildManager, loggerFactory, pluginDescriptor, cancellationToken);
        }

        private static async Task InitializePlugin(
            ProjectCacheDescriptor pluginDescriptor,
            CancellationToken cancellationToken,
            Func<PluginLoggerBase> loggerFactory,
            ProjectCachePluginBase plugin
        )
        {
            var logger = loggerFactory();

            try
            {
                await plugin.BeginBuildAsync(
                    new CacheContext(
                        pluginDescriptor.PluginSettings,
                        new DefaultMSBuildFileSystem(),
                        pluginDescriptor.ProjectGraph,
                        pluginDescriptor.EntryPoints),
                    // TODO: Detect verbosity from logging service.
                    logger,
                    cancellationToken);
            }
            catch (Exception e)
            {
                HandlePluginException(e, nameof(ProjectCachePluginBase.BeginBuildAsync));
            }

            if (logger.HasLoggedErrors)
            {
                ProjectCacheException.ThrowForErrorLoggedInsideTheProjectCache("ProjectCacheInitializationFailed");
            }
        }

        private static ProjectCachePluginBase GetPluginInstance(ProjectCacheDescriptor pluginDescriptor)
        {
            if (pluginDescriptor.PluginInstance != null)
            {
                return pluginDescriptor.PluginInstance;
            }
            if (pluginDescriptor.PluginAssemblyPath != null)
            {
                return GetPluginInstanceFromType(GetTypeFromAssemblyPath(pluginDescriptor.PluginAssemblyPath));
            }

            ErrorUtilities.ThrowInternalErrorUnreachable();

            return null!;
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
            }

            return null!;
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
                return _loader.LoadFromPath(resolverPath);
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
        private static readonly CoreClrAssemblyLoader _loader = new CoreClrAssemblyLoader();
#endif

        public void PostCacheRequest(CacheRequest cacheRequest)
        {
            Task.Run(async () =>
            {
                try
                {
                    var cacheResult = await ProcessCacheRequest(cacheRequest);
                    _buildManager.PostCacheResult(cacheRequest, cacheResult);
                }
                catch (Exception e)
                {
                    _buildManager.PostCacheResult(cacheRequest, CacheResult.IndicateException(e));
                }
            }, _cancellationToken);

            async Task<CacheResult> ProcessCacheRequest(CacheRequest request)
            {
                // Prevent needless evaluation if design time builds detected.
                if (_projectCacheDescriptor.VsWorkaround && DesignTimeBuildsDetected)
                {
                    // The BuildManager should disable the cache when it finds its servicing design time builds.
                    return CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss);
                }

                EvaluateProjectIfNecessary(request);

                if (_projectCacheDescriptor.VsWorkaround)
                {
                    Interlocked.CompareExchange(
                        ref DesignTimeBuildsDetected,
                        new NullableBool(IsDesignTimeBuild(request.Configuration.Project)),
                        null);

                    // No point progressing with expensive plugin initialization or cache query if design time build detected.
                    if (DesignTimeBuildsDetected)
                    {
                        // The BuildManager should disable the cache when it finds its servicing design time builds.
                        return CacheResult.IndicateNonCacheHit(CacheResultType.CacheMiss);
                    }
                }

                if (_projectCacheDescriptor.VsWorkaround)
                {
                    // TODO: remove after we change VS to set the cache descriptor via build parameters.
                    await LateInitializePluginForVsWorkaround(request);
                }

                return await GetCacheResultAsync(cacheRequest.Submission.BuildRequestData);
            }

            static bool IsDesignTimeBuild(ProjectInstance project)
            {
                var designTimeBuild = project.GetPropertyValue(DesignTimeProperties.DesignTimeBuild);
                var buildingProject = project.GlobalPropertiesDictionary[DesignTimeProperties.BuildingProject]?.EvaluatedValue;

                return MSBuildStringIsTrue(designTimeBuild) ||
                       buildingProject != null && !MSBuildStringIsTrue(buildingProject);
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

                ErrorUtilities.VerifyThrow(
                    solutionPath != null && !string.IsNullOrWhiteSpace(solutionPath) && solutionPath != "*Undefined*",
                    $"Expected VS to set a valid SolutionPath property but got: {solutionPath}");

                ErrorUtilities.VerifyThrow(
                    FileSystems.Default.FileExists(solutionPath),
                    $"Solution file does not exist: {solutionPath}");

                await InitializePlugin(
                    ProjectCacheDescriptor.FromAssemblyPath(
                        _projectCacheDescriptor.PluginAssemblyPath!,
                        new[]
                        {
                            new ProjectGraphEntryPoint(
                                solutionPath,
                                configuration.Project.GlobalProperties)
                        },
                        projectGraph: null,
                        _projectCacheDescriptor.PluginSettings),
                    _cancellationToken,
                    _loggerFactory,
                    _projectCachePlugin);
            }

            static bool MSBuildStringIsTrue(string msbuildString) =>
                ConversionUtilities.ConvertStringToBool(msbuildString, nullOrWhitespaceIsFalse: true);
        }

        private async Task<CacheResult> GetCacheResultAsync(BuildRequestData buildRequest)
        {
            var queryDescription = $"{buildRequest.ProjectFullPath}" +
                                   $"\n\tTargets:[{string.Join(", ", buildRequest.TargetNames)}]" +
                                   $"\n\tGlobal Properties: {{{string.Join(",", buildRequest.GlobalProperties.Select(kvp => $"{kvp.Name}={kvp.EvaluatedValue}"))}}}";

            var logger = _loggerFactory();

            logger.LogMessage(
                "\n====== Querying project cache for project " + queryDescription,
                MessageImportance.High);

            CacheResult cacheResult = null!;
            try
            {
                cacheResult = await _projectCachePlugin.GetCacheResultAsync(buildRequest, logger, _cancellationToken);
            }
            catch (Exception e)
            {
                HandlePluginException(e, nameof(ProjectCachePluginBase.GetCacheResultAsync));
            }

            if (logger.HasLoggedErrors || cacheResult.ResultType == CacheResultType.None)
            {
                ProjectCacheException.ThrowForErrorLoggedInsideTheProjectCache("ProjectCacheQueryFailed", queryDescription);
            }

            var message = $"------  Plugin result: {cacheResult.ResultType}.";

            switch (cacheResult.ResultType)
            {
                case CacheResultType.CacheHit:
                    message += " Skipping project.";
                    break;
                case CacheResultType.CacheMiss:
                case CacheResultType.CacheNotApplicable:
                    message += " Building project.";
                    break;
                case CacheResultType.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            logger.LogMessage(
                message,
                MessageImportance.High);

            return cacheResult;
        }

        public async Task ShutDown()
        {
            var logger = _loggerFactory();

            try
            {
                await _projectCachePlugin.EndBuildAsync(logger, _cancellationToken);
            }
            catch (Exception e)
            {
                HandlePluginException(e, nameof(ProjectCachePluginBase.EndBuildAsync));
            }

            if (logger.HasLoggedErrors)
            {
                ProjectCacheException.ThrowForErrorLoggedInsideTheProjectCache("ProjectCacheShutdownFailed");
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

        private class LoggingServiceToPluginLoggerAdapter : PluginLoggerBase
        {
            private readonly ILoggingService _loggingService;

            public override bool HasLoggedErrors { get; protected set; }

            public LoggingServiceToPluginLoggerAdapter(
                LoggerVerbosity verbosity,
                ILoggingService loggingService) : base(verbosity)
            {
                _loggingService = loggingService;
            }

            public override void LogMessage(string message, MessageImportance? messageImportance = null)
            {
                _loggingService.LogCommentFromText(
                    BuildEventContext.Invalid,
                    messageImportance ?? MessageImportance.Normal,
                    message);
            }

            public override void LogWarning(string warning)
            {
                _loggingService.LogWarningFromText(
                    BuildEventContext.Invalid,
                    null,
                    null,
                    null,
                    BuildEventFileInfo.Empty,
                    warning);
            }

            public override void LogError(string error)
            {
                HasLoggedErrors = true;

                _loggingService.LogErrorFromText(
                    BuildEventContext.Invalid,
                    null,
                    null,
                    null,
                    BuildEventFileInfo.Empty,
                    error);
            }
        }
    }
}
