// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using static Microsoft.Build.Execution.ProjectPropertyInstance;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// A logging context for a project.
    /// </summary>
    internal class ProjectLoggingContext : BuildLoggingContext
    {
        /// <summary>
        /// The project's full path
        /// </summary>
        private string _projectFullPath;

        /// <summary>
        /// Telemetry data for a project
        /// </summary>
        private readonly ProjectTelemetry _projectTelemetry = new ProjectTelemetry();

        /// <summary>
        /// Private constructor - use factory methods instead.
        /// </summary>
        private ProjectLoggingContext(
            NodeLoggingContext nodeLoggingContext,
            BuildEventContext buildEventContext,
            string projectFullPath,
            string toolsVersion)
            : base(nodeLoggingContext, buildEventContext)
        {
            _projectFullPath = projectFullPath;

            // No need to log a redundant message in the common case
            if (toolsVersion != "Current")
            {
                LoggingService.LogComment(BuildEventContext, MessageImportance.Low, "ToolsVersionInEffectForBuild", toolsVersion);
            }

            IsValid = true;
        }

        /// <summary>
        /// Creates ProjectLoggingContext for real local project builds.
        /// Returns both ProjectStartedEventArgs (for caller to configure) and ProjectLoggingContext.
        /// Does NOT log ProjectStarted immediately.
        /// </summary>
        public static (ProjectStartedEventArgs, ProjectLoggingContext) CreateForLocalBuild(
            NodeLoggingContext nodeLoggingContext, BuildRequestEntry requestEntry)
        {
            IEnumerable<DictionaryEntry> properties = GetProjectProperties(
                nodeLoggingContext.LoggingService,
                requestEntry.RequestConfiguration.Project?.PropertiesToBuildWith);
            IEnumerable<DictionaryEntry> items = GetProjectItems(
                nodeLoggingContext.LoggingService,
                requestEntry.RequestConfiguration.Project?.ItemsToBuildWith);

            IDictionary<string, string> globalProperties = requestEntry.RequestConfiguration.GlobalProperties.ToDictionary();

            ProjectStartedEventArgs args = nodeLoggingContext.LoggingService.CreateProjectStartedForLocalProject(
                requestEntry.Request.ParentBuildEventContext,
                requestEntry.RequestConfiguration.ConfigurationId,
                requestEntry.RequestConfiguration.ProjectFullPath,
                string.Join(";", requestEntry.Request.Targets),
                globalProperties,
                properties,
                items,
                requestEntry.RequestConfiguration.ToolsVersion);

            var context = new ProjectLoggingContext(
                nodeLoggingContext,
                args.BuildEventContext,
                args.ProjectFile,
                args.ToolsVersion);

            return (args, context);
        }

        /// <summary>
        /// Creates ProjectLoggingContext for cached project builds.
        /// Immediately logs ProjectStarted event with minimal data.
        /// </summary>
        public static ProjectLoggingContext CreateForCacheBuild(
            NodeLoggingContext nodeLoggingContext,
            BuildRequest request,
            BuildRequestConfiguration configuration)
        {
            BuildEventContext buildEventContext = CreateAndLogProjectStartedForCache(
                nodeLoggingContext,
                request,
                configuration);

            return new ProjectLoggingContext(
                nodeLoggingContext,
                buildEventContext,
                configuration.ProjectFullPath,
                configuration.ToolsVersion);
        }

        /// <summary>
        /// Creates BuildEventContext and logs ProjectStarted for cache scenarios.
        /// </summary>
        private static BuildEventContext CreateAndLogProjectStartedForCache(
            NodeLoggingContext nodeLoggingContext,
            BuildRequest request,
            BuildRequestConfiguration configuration)
        {
            // Create a remote node evaluation context with the original evaluation ID
            BuildEventContext remoteNodeEvaluationBuildEventContext = BuildEventContext.CreateInitial(
                configuration.ResultsNodeId, // Use the node that originally built this project configuration
                request.SubmissionId)
                .WithEvaluationId(configuration.ProjectEvaluationId)
                .WithProjectInstanceId(configuration.ConfigurationId)
                // TODO: should we keep this data on the 'original' context - it is _very likely_ not the project
                // context Id of the request that created the cached result, but it is the only data we have at this point (so far)
                .WithProjectContextId(request.ProjectContextId);
            
            IDictionary<string, string> globalProperties = configuration.GlobalProperties.ToDictionary();

            ProjectStartedEventArgs args = nodeLoggingContext.LoggingService.CreateProjectStartedForCachedProject(
                nodeLoggingContext.BuildEventContext, // Current node context
                remoteNodeEvaluationBuildEventContext, // Original remote node context
                request.ParentBuildEventContext,
                globalProperties,
                configuration.ProjectFullPath,
                string.Join(";", request.Targets),
                configuration.ToolsVersion);

            nodeLoggingContext.LoggingService.LogProjectStarted(args);
            return args.BuildEventContext;
        }

        /// <summary>
        /// Gets project properties for logging if appropriate - as determined by the logging service.
        /// </summary>
        private static IEnumerable<DictionaryEntry> GetProjectProperties(
            ILoggingService loggingService,
            PropertyDictionary<ProjectPropertyInstance> projectProperties)
        {
            if (projectProperties == null ||
                loggingService.OnlyLogCriticalEvents ||
                !loggingService.IncludeEvaluationPropertiesAndItemsInProjectStartedEvent ||
                (loggingService.RunningOnRemoteNode && !loggingService.SerializeAllProperties))
            {
                return null;
            }

            if (Traits.LogAllEnvironmentVariables)
            {
                return projectProperties.GetCopyOnReadEnumerable(property => new DictionaryEntry(property.Name, property.EvaluatedValue));
            }
            else
            {
                return projectProperties.Filter(
                    p => p is not EnvironmentDerivedProjectPropertyInstance || EnvironmentUtilities.IsWellKnownEnvironmentDerivedProperty(p.Name),
                    p => new DictionaryEntry(p.Name, p.EvaluatedValue));
            }
        }

        /// <summary>
        /// Gets project items for logging if appropriate - as determined by the logging service.
        /// </summary>
        private static IEnumerable<DictionaryEntry> GetProjectItems(
            ILoggingService loggingService,
            IItemDictionary<ProjectItemInstance> projectItems)
        {
            if (projectItems == null ||
                loggingService.OnlyLogCriticalEvents ||
                !loggingService.IncludeEvaluationPropertiesAndItemsInProjectStartedEvent ||
                (loggingService.RunningOnRemoteNode && !loggingService.SerializeAllProperties))
            {
                return null;
            }

            return projectItems.GetCopyOnReadEnumerable(item => new DictionaryEntry(item.ItemType, new TaskItem(item)));
        }

        /// <summary>
        /// Telemetry data for a project
        /// </summary>
        internal ProjectTelemetry ProjectTelemetry => _projectTelemetry;

        /// <summary>
        /// Log that the project has finished
        /// </summary>
        /// <param name="success">Did the build succeede or not</param>
        internal void LogProjectFinished(bool success)
        {
            ErrorUtilities.VerifyThrow(IsValid, "invalid");
            LoggingService.LogProjectFinished(BuildEventContext, _projectFullPath, success);
            IsValid = false;
        }

        /// <summary>
        /// Log that a target has started
        /// </summary>
        internal TargetLoggingContext LogTargetBatchStarted(string projectFullPath, ProjectTargetInstance target, string parentTargetName, TargetBuiltReason buildReason)
        {
            ErrorUtilities.VerifyThrow(IsValid, "invalid");
            return new TargetLoggingContext(this, projectFullPath, target, parentTargetName, buildReason);
        }
    }
}
