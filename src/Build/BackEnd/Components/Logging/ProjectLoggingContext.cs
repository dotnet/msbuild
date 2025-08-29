﻿// Licensed to the .NET Foundation under one or more agreements.
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
            requestEntry.RequestConfiguration.Project.EvaluationId,
            requestEntry.Request.ProjectContextId)
        {
        }

        /// <summary>
        /// Constructs a project logging context.
        /// </summary>
        internal ProjectLoggingContext(
            NodeLoggingContext nodeLoggingContext,
            BuildRequest request,
            string projectFullPath,
            string toolsVersion,
            int evaluationId = BuildEventContext.InvalidEvaluationId)
            : this
            (
            nodeLoggingContext,
            request.SubmissionId,
            request.ConfigurationId,
            projectFullPath,
            request.Targets,
            toolsVersion,
            projectProperties: null,
            projectItems: null,
            request.ParentBuildEventContext,
            evaluationId,
            request.ProjectContextId)
        {
        }

        /// <summary>
        /// Constructs a project logging contexts.
        /// </summary>
        private ProjectLoggingContext(
            NodeLoggingContext nodeLoggingContext,
            int submissionId,
            int configurationId,
            string projectFullPath,
            List<string> targets,
            string toolsVersion,
            PropertyDictionary<ProjectPropertyInstance> projectProperties,
            ItemDictionary<ProjectItemInstance> projectItems,
            BuildEventContext parentBuildEventContext,
            int evaluationId,
            int projectContextId)
            : base(nodeLoggingContext)
        {
            _projectFullPath = projectFullPath;

            IEnumerable<DictionaryEntry> properties = null;
            IEnumerable<DictionaryEntry> items = null;

            string[] propertiesToSerialize = LoggingService.PropertiesToSerialize;

            // If we are only logging critical events lets not pass back the items or properties
            if (!LoggingService.OnlyLogCriticalEvents &&
                !LoggingService.IncludeEvaluationPropertiesAndItems &&
                (!LoggingService.RunningOnRemoteNode || LoggingService.SerializeAllProperties))
            {
                if (projectProperties is null)
                {
                    properties = Enumerable.Empty<DictionaryEntry>();
                }
                else if (Traits.LogAllEnvironmentVariables)
                {
                    properties = projectProperties.GetCopyOnReadEnumerable(property => new DictionaryEntry(property.Name, property.EvaluatedValue));
                }
                else
                {
                    properties = projectProperties.Filter(p => p is not EnvironmentDerivedProjectPropertyInstance || EnvironmentUtilities.IsWellKnownEnvironmentDerivedProperty(p.Name), p => new DictionaryEntry(p.Name, p.EvaluatedValue));
                }

                items = projectItems?.GetCopyOnReadEnumerable(item => new DictionaryEntry(item.ItemType, new TaskItem(item))) ?? Enumerable.Empty<DictionaryEntry>();
            }

            if (projectProperties != null &&
                !LoggingService.IncludeEvaluationPropertiesAndItems &&
                propertiesToSerialize?.Length > 0 &&
                !LoggingService.SerializeAllProperties)
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

            this.BuildEventContext = LoggingService.LogProjectStarted(
                nodeLoggingContext.BuildEventContext,
                submissionId,
                configurationId,
                parentBuildEventContext,
                projectFullPath,
                string.Join(";", targets),
                properties,
                items,
                evaluationId,
                projectContextId);

            // No need to log a redundant message in the common case
            if (toolsVersion != "Current")
            {
                LoggingService.LogComment(this.BuildEventContext, MessageImportance.Low, "ToolsVersionInEffectForBuild", toolsVersion);
            }

            this.IsValid = true;
        }

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
