// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    /// <summary>
    /// A mock implementation of ProjectLink to be used for testing ProjectInstance created from cache state.
    /// </summary>
    internal class FakeProjectLink : ProjectLink
    {
        public FakeProjectLink(
            string path,
            ICollection<ProjectProperty>? properties = null,
            IDictionary<string, ProjectItemDefinition>? itemDefinitions = null,
            ICollection<ProjectItem>? items = null)
        {
            Xml = new ProjectRootElement(new FakeProjectRootElementLink(path));
            Properties = properties ?? new FakeCachedEntityDictionary<ProjectProperty>();
            ItemDefinitions = itemDefinitions ?? new FakeCachedEntityDictionary<ProjectItemDefinition>();
            Items = items ?? new FakeProjectItemDictionary();
        }

        public override ProjectRootElement Xml { get; }

        public override bool ThrowInsteadOfSplittingItemElement { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override bool IsDirty => false;

        public override IDictionary<string, string> GlobalProperties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public override ICollection<string> ItemTypes => throw new NotImplementedException();

        public override ICollection<ProjectProperty> Properties { get; }

        public override IDictionary<string, List<string>> ConditionedProperties => throw new NotImplementedException();

        public override IDictionary<string, ProjectItemDefinition> ItemDefinitions { get; }

        public override ICollection<ProjectItem> Items { get; }

        public override ICollection<ProjectItem> ItemsIgnoringCondition => throw new NotImplementedException();

        public override IList<ResolvedImport> Imports => throw new NotImplementedException();

        public override IList<ResolvedImport> ImportsIncludingDuplicates => throw new NotImplementedException();

        public override IDictionary<string, ProjectTargetInstance> Targets { get; } = new Dictionary<string, ProjectTargetInstance>(StringComparer.OrdinalIgnoreCase);

        public override ICollection<ProjectProperty> AllEvaluatedProperties => throw new NotImplementedException();

        public override ICollection<ProjectMetadata> AllEvaluatedItemDefinitionMetadata => throw new NotImplementedException();

        public override ICollection<ProjectItem> AllEvaluatedItems => throw new NotImplementedException();

        public override string ToolsVersion => ProjectCollection.GlobalProjectCollection.DefaultToolsVersion;

        public override string SubToolsetVersion => null!;

        public override bool SkipEvaluation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override bool DisableMarkDirty { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override bool IsBuildEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override int LastEvaluationId => 0;

        public override IList<ProjectItem> AddItem(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata) => throw new NotImplementedException();

        public override IList<ProjectItem> AddItemFast(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata) => throw new NotImplementedException();

        public override bool Build(string[] targets, IEnumerable<ILogger> loggers, IEnumerable<ForwardingLoggerRecord> remoteLoggers, EvaluationContext evaluationContext) => throw new NotImplementedException();

        public override ProjectInstance CreateProjectInstance(ProjectInstanceSettings settings, EvaluationContext evaluationContext) => throw new NotImplementedException();

        public override string ExpandString(string unexpandedValue) => throw new NotImplementedException();

        public override List<GlobResult> GetAllGlobs(EvaluationContext evaluationContext) => throw new NotImplementedException();

        public override List<GlobResult> GetAllGlobs(string itemType, EvaluationContext evaluationContext) => throw new NotImplementedException();

        public override List<ProvenanceResult> GetItemProvenance(string itemToMatch, EvaluationContext evaluationContext) => throw new NotImplementedException();

        public override List<ProvenanceResult> GetItemProvenance(string itemToMatch, string itemType, EvaluationContext evaluationContext) => throw new NotImplementedException();

        public override List<ProvenanceResult> GetItemProvenance(ProjectItem item, EvaluationContext evaluationContext) => throw new NotImplementedException();

        public override ICollection<ProjectItem> GetItems(string itemType) => throw new NotImplementedException();

        public override ICollection<ProjectItem> GetItemsByEvaluatedInclude(string evaluatedInclude) => throw new NotImplementedException();

        public override ICollection<ProjectItem> GetItemsIgnoringCondition(string itemType) => throw new NotImplementedException();

        public override IEnumerable<ProjectElement> GetLogicalProject() => throw new NotImplementedException();

        public override ProjectProperty GetProperty(string name) => throw new NotImplementedException();

        public override string GetPropertyValue(string name) => throw new NotImplementedException();

        public override void MarkDirty() => throw new NotImplementedException();

        public override void ReevaluateIfNecessary(EvaluationContext evaluationContext) => throw new NotImplementedException();

        public override bool RemoveGlobalProperty(string name) => throw new NotImplementedException();

        public override bool RemoveItem(ProjectItem item) => throw new NotImplementedException();

        public override void RemoveItems(IEnumerable<ProjectItem> items) => throw new NotImplementedException();

        public override bool RemoveProperty(ProjectProperty property) => throw new NotImplementedException();

        public override void SaveLogicalProject(TextWriter writer) => throw new NotImplementedException();

        public override bool SetGlobalProperty(string name, string escapedValue) => throw new NotImplementedException();

        public override ProjectProperty SetProperty(string name, string unevaluatedValue) => throw new NotImplementedException();

        public override void Unload() => throw new NotImplementedException();
    }
}
