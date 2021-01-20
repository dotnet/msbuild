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
        private readonly PluginLoggerBase _logger;
        private readonly ProjectCacheDescriptor _projectCacheDescriptor;
        private readonly CancellationToken _cancellationToken;
        private readonly ProjectCachePluginBase _projectCachePlugin;

        private ProjectCacheService(
            ProjectCachePluginBase projectCachePlugin,
            BuildManager buildManager,
            PluginLoggerBase logger,
            ProjectCacheDescriptor projectCacheDescriptor,
            CancellationToken cancellationToken)
        {
            _projectCachePlugin = projectCachePlugin;
            _buildManager = buildManager;
            _logger = logger;
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
            var logger = new LoggingServiceToPluginLoggerAdapter(LoggerVerbosity.Normal, loggingService);

            await plugin.BeginBuildAsync(
                new CacheContext(
                    pluginDescriptor.PluginSettings,
                    new IFileSystemAdapter(FileSystems.Default),
                    pluginDescriptor.ProjectGraph,
                    pluginDescriptor.EntryPoints),
                // TODO: Detect verbosity from logging service.
                logger,
                cancellationToken);

            if (logger.HasLoggedErrors)
            {
                throw new Exception(
                    ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectCacheInitializationFailed"));
            }

            return new ProjectCacheService(plugin, buildManager, logger, pluginDescriptor, cancellationToken);
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

#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }

        private static ProjectCachePluginBase GetPluginInstanceFromType(Type pluginType)
        {
            try
            {
                return (ProjectCachePluginBase) Activator.CreateInstance(pluginType)!;
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }

        private static Type GetTypeFromAssemblyPath(string pluginAssemblyPath)
        {
            var assembly = LoadAssembly(pluginAssemblyPath);

            var type = GetTypes<ProjectCachePluginBase>(assembly).FirstOrDefault();

            ErrorUtilities.VerifyThrow(type != null, "NoProjectCachePluginFoundInAssembly", pluginAssemblyPath);

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

            _logger.LogMessage(
                "\n====== Querying project cache for project " + queryDescription,
                MessageImportance.High);

            var cacheResult = await _projectCachePlugin.GetCacheResultAsync(buildRequest, _logger, _cancellationToken);

            if (_logger.HasLoggedErrors || cacheResult.ResultType == CacheResultType.None)
            {
                throw new Exception(
                    ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectCacheQueryFailed", queryDescription));
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

            _logger.LogMessage(
                message,
                MessageImportance.High);

            return cacheResult;
        }

        public async Task ShutDown()
        {
            await _projectCachePlugin.EndBuildAsync(_logger, _cancellationToken);

            if (_logger.HasLoggedErrors)
            {
                throw new Exception(
                    ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectCacheShutdownFailed"));
            }
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
