// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Experimental.ProjectCache
{
    internal class ProjectCacheService
    {
        private readonly BuildManager _buildManager;
        private readonly Func<PluginLoggerBase> _loggerFactory;
        private readonly ProjectCacheDescriptor _projectCacheDescriptor;
        private readonly CancellationToken _cancellationToken;
        private readonly ProjectCachePluginBase _projectCachePlugin;

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

            // TODO: Detect and use the highest verbosity from all the user defined loggers. That's tricky because right now we can't discern between user set loggers and msbuild's internally added loggers.
            var loggerFactory = new Func<PluginLoggerBase>(() => new LoggingServiceToPluginLoggerAdapter(LoggerVerbosity.Normal, loggingService));

            var logger = loggerFactory();

            try
            {
                await plugin.BeginBuildAsync(
                    new CacheContext(
                        pluginDescriptor.PluginSettings,
                        new IFileSystemAdapter(FileSystems.Default),
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

            return new ProjectCacheService(plugin, buildManager, loggerFactory, pluginDescriptor, cancellationToken);
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

        public async Task<CacheResult> GetCacheResultAsync(BuildRequestData buildRequest)
        {
            // TODO: Parent these logs under the project build event so they appear nested under the project in the binlog viewer.
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

            var message = $"Plugin result: {cacheResult.ResultType}.";

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
