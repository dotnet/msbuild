// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        /// Constructs a project logging context.
        /// </summary>
        internal ProjectLoggingContext(NodeLoggingContext nodeLoggingContext, BuildRequestEntry requestEntry)
            : this
            (
            nodeLoggingContext,
            requestEntry.Request.SubmissionId,
            requestEntry.Request.ConfigurationId,
            requestEntry.RequestConfiguration.ProjectFullPath,
            requestEntry.Request.Targets,
            requestEntry.RequestConfiguration.ToolsVersion,
            requestEntry.RequestConfiguration.Project.PropertiesToBuildWith,
            requestEntry.RequestConfiguration.Project.ItemsToBuildWith,
            requestEntry.Request.ParentBuildEventContext,
            requestEntry.RequestConfiguration.ProjectEvaluationId,
            requestEntry.Request.ProjectContextId,
            requestEntry.Request.ScheduledNodeId)
        {
        }

        /// <summary>
        /// Constructs a project logging context.
        /// </summary>
        internal ProjectLoggingContext(
            NodeLoggingContext nodeLoggingContext,
            BuildRequest request,
            BuildRequestConfiguration configuration)
            : this
            (
            nodeLoggingContext,
            request.SubmissionId,
            request.ConfigurationId,
            configuration.ProjectFullPath,
            request.Targets,
            configuration.ToolsVersion,
            projectProperties: null,
            projectItems: null,
            request.ParentBuildEventContext,
            // if the project was built on a different node, the evaluation id will be a lie anyway, make that super clear
            configuration.ResultsNodeId != nodeLoggingContext.BuildEventContext.NodeId ? int.MaxValue : configuration.ProjectEvaluationId,
            request.ProjectContextId,
            configuration.ResultsNodeId)
        {
        }

        /// <summary>
        /// Creates ProjectLoggingContext, without logging ProjectStartedEventArgs as a side effect.
        /// The ProjectStartedEventArgs is returned as well - so that it can be later logged explicitly
        /// </summary>
        public static (ProjectStartedEventArgs, ProjectLoggingContext) CreateLoggingContext(
            NodeLoggingContext nodeLoggingContext, BuildRequestEntry requestEntry)
        {
            ProjectStartedEventArgs args = CreateProjectStarted(
                nodeLoggingContext,
                requestEntry.Request.SubmissionId,
                requestEntry.Request.ConfigurationId,
                requestEntry.RequestConfiguration.ProjectFullPath,
                requestEntry.Request.Targets,
                requestEntry.RequestConfiguration.ToolsVersion,
                requestEntry.RequestConfiguration.Project.PropertiesToBuildWith,
                requestEntry.RequestConfiguration.Project.ItemsToBuildWith,
                requestEntry.Request.ParentBuildEventContext,
                requestEntry.RequestConfiguration.ProjectEvaluationId,
                requestEntry.Request.ProjectContextId,
                // in this scenario we are on the same node, so just use the current node id
                nodeLoggingContext.BuildEventContext.NodeId);

            return (args, new ProjectLoggingContext(nodeLoggingContext, args));
        }

        private ProjectLoggingContext(
            NodeLoggingContext nodeLoggingContext,
            ProjectStartedEventArgs projectStarted)
        : base(nodeLoggingContext, projectStarted.BuildEventContext)
        {
            _projectFullPath = projectStarted.ProjectFile;

            // No need to log a redundant message in the common case
            if (projectStarted.ToolsVersion != "Current")
            {
                LoggingService.LogComment(this.BuildEventContext, MessageImportance.Low, "ToolsVersionInEffectForBuild", projectStarted.ToolsVersion);
            }

            this.IsValid = true;
        }

        /// <summary>
        /// Constructs a project logging contexts.
        /// </summary>
        /// <param name="nodeLoggingContext">The node logging context for the currently executing node.</param>
        /// <param name="submissionId">The submission id for this project.</param>
        /// <param name="configurationId">The configuration id for this project.</param>
        /// <param name="projectFullPath">The full path to the project file.</param>
        /// <param name="targets">The targets being built in this project.</param>
        /// <param name="toolsVersion">The tools version for this project.</param>
        /// <param name="projectProperties">The properties in the project.</param>
        /// <param name="projectItems">The items in the project.</param>
        /// <param name="parentBuildEventContext">The parent build event context.</param>
        /// <param name="evaluationId">The evaluation id for this project.</param>
        /// <param name="projectContextId">The project context id for this project.</param>
        /// <param name="hostNodeId">The node id hosting this project - may be different from that of the nodeLoggingContext if this project was actually started/built on another node</param>
        private ProjectLoggingContext(
            NodeLoggingContext nodeLoggingContext,
            int submissionId,
            int configurationId,
            string projectFullPath,
            List<string> targets,
            string toolsVersion,
            PropertyDictionary<ProjectPropertyInstance> projectProperties,
            IItemDictionary<ProjectItemInstance> projectItems,
            BuildEventContext parentBuildEventContext,
            int evaluationId,
            int projectContextId,
            int hostNodeId)
            : base(nodeLoggingContext,
                CreateInitialContext(nodeLoggingContext,
                    submissionId,
                     configurationId,
                    projectFullPath,
                    targets,
                    toolsVersion,
                    projectProperties,
                    projectItems,
                    parentBuildEventContext,
                    evaluationId,
                    projectContextId,
                    hostNodeId))
        {
            _projectFullPath = projectFullPath;

            // No need to log a redundant message in the common case
            if (toolsVersion != "Current")
            {
                LoggingService.LogComment(this.BuildEventContext, MessageImportance.Low, "ToolsVersionInEffectForBuild", toolsVersion);
            }

            this.IsValid = true;
        }

        private static BuildEventContext CreateInitialContext(
            NodeLoggingContext nodeLoggingContext,
            int submissionId,
            int configurationId,
            string projectFullPath,
            List<string> targets,
            string toolsVersion,
            PropertyDictionary<ProjectPropertyInstance> projectProperties,
            IItemDictionary<ProjectItemInstance> projectItems,
            BuildEventContext parentBuildEventContext,
            int evaluationId,
            int projectContextId,
            int hostNodeId)
        {
            ProjectStartedEventArgs args = CreateProjectStarted(
                nodeLoggingContext,
                submissionId,
                configurationId,
                projectFullPath,
                targets,
                toolsVersion,
                projectProperties,
                projectItems,
                parentBuildEventContext,
                evaluationId,
                projectContextId,
                hostNodeId);

            nodeLoggingContext.LoggingService.LogProjectStarted(args);

            return args.BuildEventContext;
        }

        private static ProjectStartedEventArgs CreateProjectStarted(
            NodeLoggingContext nodeLoggingContext,
            int submissionId,
            int configurationId,
            string projectFullPath,
            List<string> targets,
            string toolsVersion,
            PropertyDictionary<ProjectPropertyInstance> projectProperties,
            IItemDictionary<ProjectItemInstance> projectItems,
            BuildEventContext parentBuildEventContext,
            int evaluationId,
            int projectContextId,
            int hostNodeId)
        {
            IEnumerable<DictionaryEntry> properties = null;
            IEnumerable<DictionaryEntry> items = null;

            ILoggingService loggingService = nodeLoggingContext.LoggingService;

            string[] propertiesToSerialize = loggingService.PropertiesToSerialize;

            // If we are only logging critical events lets not pass back the items or properties
            if (!loggingService.OnlyLogCriticalEvents &&
                loggingService.IncludeEvaluationPropertiesAndItemsInProjectStartedEvent &&
                (!loggingService.RunningOnRemoteNode || loggingService.SerializeAllProperties))
            {
                if (projectProperties is null)
                {
                    properties = [];
                }
                else if (Traits.LogAllEnvironmentVariables)
                {
                    properties = projectProperties.GetCopyOnReadEnumerable(property => new DictionaryEntry(property.Name, property.EvaluatedValue));
                }
                else
                {
                    properties = projectProperties.Filter(p => p is not EnvironmentDerivedProjectPropertyInstance || EnvironmentUtilities.IsWellKnownEnvironmentDerivedProperty(p.Name), p => new DictionaryEntry(p.Name, p.EvaluatedValue));
                }

                items = projectItems?.GetCopyOnReadEnumerable(item => new DictionaryEntry(item.ItemType, new TaskItem(item))) ?? [];
            }

            if (projectProperties != null &&
                loggingService.IncludeEvaluationPropertiesAndItemsInProjectStartedEvent &&
                propertiesToSerialize?.Length > 0 &&
                !loggingService.SerializeAllProperties)
            {
                PropertyDictionary<ProjectPropertyInstance> projectPropertiesToSerialize = new PropertyDictionary<ProjectPropertyInstance>();
                foreach (string propertyToGet in propertiesToSerialize)
                {
                    ProjectPropertyInstance instance = projectProperties[propertyToGet];
                    {
                        if (instance != null)
                        {
                            projectPropertiesToSerialize.Set(instance);
                        }
                    }
                }

                properties = projectPropertiesToSerialize.Select((ProjectPropertyInstance property) => new DictionaryEntry(property.Name, property.EvaluatedValue));
            }

            return loggingService.CreateProjectStarted(
                // adjust the message to come from the node that actually built the project
                nodeLoggingContext.BuildEventContext.WithNodeId(hostNodeId),
                submissionId,
                configurationId,
                parentBuildEventContext,
                projectFullPath,
                string.Join(";", targets),
                properties,
                items,
                evaluationId,
                projectContextId);
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
            ErrorUtilities.VerifyThrow(this.IsValid, "invalid");
            LoggingService.LogProjectFinished(BuildEventContext, _projectFullPath, success);
            this.IsValid = false;
        }

        /// <summary>
        /// Log that a target has started
        /// </summary>
        internal TargetLoggingContext LogTargetBatchStarted(string projectFullPath, ProjectTargetInstance target, string parentTargetName, TargetBuiltReason buildReason)
        {
            ErrorUtilities.VerifyThrow(this.IsValid, "invalid");
            return new TargetLoggingContext(this, projectFullPath, target, parentTargetName, buildReason);
        }
    }
}
