// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="Project"/>
    /// </summary>
    public abstract class ProjectLink
    {
        /// <summary>
        /// Access to remote <see cref="Project.Xml"/>.
        /// </summary>
        public abstract ProjectRootElement Xml { get; }

        /// <summary>
        /// Access to remote <see cref="Project.ThrowInsteadOfSplittingItemElement"/>.
        /// </summary>
        public abstract bool ThrowInsteadOfSplittingItemElement { get; set; }

        /// <summary>
        /// Access to remote <see cref="Project.IsDirty"/>.
        /// </summary>
        public abstract bool IsDirty { get; }

        /// <summary>
        /// Access to remote <see cref="Project.GlobalProperties"/>.
        /// </summary>
        public abstract IDictionary<string, string> GlobalProperties { get; }

        /// <summary>
        /// Access to remote <see cref="Project.ItemTypes"/>.
        /// </summary>
        public abstract ICollection<string> ItemTypes { get; }

        /// <summary>
        /// Access to remote <see cref="Project.Properties"/>.
        /// </summary>
        public abstract ICollection<ProjectProperty> Properties { get; }

        /// <summary>
        /// Access to remote <see cref="Project.ConditionedProperties"/>.
        /// </summary>
        public abstract IDictionary<string, List<string>> ConditionedProperties { get; }

        /// <summary>
        /// Access to remote <see cref="Project.ItemDefinitions"/>.
        /// </summary>
        public abstract IDictionary<string, ProjectItemDefinition> ItemDefinitions { get; }

        /// <summary>
        /// Access to remote <see cref="Project.Items"/>.
        /// </summary>
        public abstract ICollection<ProjectItem> Items { get; }

        /// <summary>
        /// Access to remote <see cref="Project.ItemsIgnoringCondition"/>.
        /// </summary>
        public abstract  ICollection<ProjectItem> ItemsIgnoringCondition { get; }

        /// <summary>
        /// Access to remote <see cref="Project.Imports"/>.
        /// </summary>
        public abstract IList<ResolvedImport> Imports { get; }

        /// <summary>
        /// Access to remote <see cref="Project.ImportsIncludingDuplicates"/>.
        /// </summary>
        public abstract IList<ResolvedImport> ImportsIncludingDuplicates { get; }

        /// <summary>
        /// Access to remote <see cref="Project.Targets"/>.
        /// </summary>
        public abstract IDictionary<string, ProjectTargetInstance> Targets { get; }

        /// <summary>
        /// Access to remote <see cref="Project.AllEvaluatedProperties"/>.
        /// </summary>
        public abstract ICollection<ProjectProperty> AllEvaluatedProperties { get; }

        /// <summary>
        /// Access to remote <see cref="Project.AllEvaluatedItemDefinitionMetadata "/>.
        /// </summary>
        public abstract ICollection<ProjectMetadata> AllEvaluatedItemDefinitionMetadata { get; }

        /// <summary>
        /// Access to remote <see cref="Project.AllEvaluatedItems "/>.
        /// </summary>
        public abstract ICollection<ProjectItem> AllEvaluatedItems { get; }

        /// <summary>
        /// Access to remote <see cref="Project.ToolsVersion"/>.
        /// </summary>
        public abstract string ToolsVersion { get; }

        /// <summary>
        /// Access to remote <see cref="Project.SubToolsetVersion"/>.
        /// </summary>
        public abstract string SubToolsetVersion { get; }

        /// <summary>
        /// Access to remote <see cref="Project.SkipEvaluation"/>.
        /// </summary>
        public abstract bool SkipEvaluation { get; set; }

        /// <summary>
        /// Access to remote <see cref="Project.DisableMarkDirty"/>.
        /// </summary>
        public abstract bool DisableMarkDirty { get; set; }

        /// <summary>
        /// Access to remote <see cref="Project.IsBuildEnabled"/>.
        /// </summary>
        public abstract bool IsBuildEnabled { get; set; }

        /// <summary>
        /// Access to remote <see cref="Project.LastEvaluationId"/>.
        /// </summary>
        public abstract int LastEvaluationId { get; }

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetAllGlobs(EvaluationContext)"/>.
        /// </summary>
        public abstract List<GlobResult> GetAllGlobs(EvaluationContext evaluationContext);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetAllGlobs(string, EvaluationContext)"/>.
        /// </summary>
        public abstract List<GlobResult> GetAllGlobs(string itemType, EvaluationContext evaluationContext);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetItemProvenance(string, EvaluationContext)"/>.
        /// </summary>
        public abstract List<ProvenanceResult> GetItemProvenance(string itemToMatch, EvaluationContext evaluationContext);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetItemProvenance(string, string, EvaluationContext)"/>.
        /// </summary>
        public abstract List<ProvenanceResult> GetItemProvenance(string itemToMatch, string itemType, EvaluationContext evaluationContext);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetItemProvenance(ProjectItem, EvaluationContext)"/>.
        /// </summary>
        public abstract List<ProvenanceResult> GetItemProvenance(ProjectItem item, EvaluationContext evaluationContext);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetLogicalProject"/>.
        /// </summary>
        public abstract IEnumerable<ProjectElement> GetLogicalProject();

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetProperty"/>.
        /// </summary>
        public abstract ProjectProperty GetProperty(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetPropertyValue"/>.
        /// </summary>
        public abstract string GetPropertyValue(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.SetProperty"/>.
        /// </summary>
        public abstract ProjectProperty SetProperty(string name, string unevaluatedValue);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.SetGlobalProperty"/>.
        /// </summary>
        public abstract bool SetGlobalProperty(string name, string escapedValue);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.AddItem(string, string, IEnumerable{KeyValuePair{string, string}})"/>.
        /// </summary>
        public abstract IList<ProjectItem> AddItem(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.AddItemFast(string, string, IEnumerable{KeyValuePair{string, string}})"/>.
        /// </summary>
        public abstract IList<ProjectItem> AddItemFast(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetItems"/>.
        /// </summary>
        public abstract ICollection<ProjectItem> GetItems(string itemType);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetItemsIgnoringCondition"/>.
        /// </summary>
        public abstract ICollection<ProjectItem> GetItemsIgnoringCondition(string itemType);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.GetItemsByEvaluatedInclude"/>.
        /// </summary>
        public abstract ICollection<ProjectItem> GetItemsByEvaluatedInclude(string evaluatedInclude);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.RemoveProperty"/>.
        /// </summary>
        public abstract bool RemoveProperty(ProjectProperty property);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.RemoveGlobalProperty"/>.
        /// </summary>
        public abstract bool RemoveGlobalProperty(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.RemoveItem"/>.
        /// </summary>
        public abstract bool RemoveItem(ProjectItem item);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.RemoveItems"/>.
        /// </summary>
        public abstract void RemoveItems(IEnumerable<ProjectItem> items);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.ExpandString"/>.
        /// </summary>
        public abstract string ExpandString(string unexpandedValue);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.CreateProjectInstance(ProjectInstanceSettings, EvaluationContext)"/>.
        /// </summary>
        public abstract ProjectInstance CreateProjectInstance(ProjectInstanceSettings settings, EvaluationContext evaluationContext);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.MarkDirty"/>.
        /// </summary>
        public abstract void MarkDirty();

        /// <summary>
        /// Facilitate remoting the <see cref="Project.ReevaluateIfNecessary(EvaluationContext)"/>.
        /// </summary>
        public abstract void ReevaluateIfNecessary(EvaluationContext evaluationContext);

        /// <summary>
        /// Facilitate remoting the <see cref="Project.SaveLogicalProject"/>.
        /// </summary>
        public abstract void SaveLogicalProject(TextWriter writer);

        /// <summary>
        /// Facilitate support for remote build.
        /// </summary>
        public abstract bool Build(string[] targets, IEnumerable<ILogger> loggers, IEnumerable<ForwardingLoggerRecord> remoteLoggers, EvaluationContext evaluationContext);

        /// <summary>
        /// Called by the local project collection to indicate to this project that it is no longer loaded.
        /// </summary>
        public abstract void Unload();
    }
}
