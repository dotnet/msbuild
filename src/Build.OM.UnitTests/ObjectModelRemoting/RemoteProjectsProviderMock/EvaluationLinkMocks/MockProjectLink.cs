// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Evaluation.Context;
    using Microsoft.Build.Execution;
    using Microsoft.Build.ObjectModelRemoting;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Logging;

    internal class MockProjectLinkRemoter : MockLinkRemoter<Project>
    {
        public override Project CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }


        ///  ProjectLink remoting

        public MockProjectElementLinkRemoter Xml => this.OwningCollection.ExportElement(this.Source.Xml);

        public bool ThrowInsteadOfSplittingItemElement { get => this.Source.ThrowInsteadOfSplittingItemElement; set => this.Source.ThrowInsteadOfSplittingItemElement = value; }

        public bool IsDirty => this.Source.IsDirty;

        // all bellow are very inefficient,
        // in reality we do cache these collections  until invalidated and use lazy access for dictionaries.
        // TODO: Might bring that infrastructure here as well ... 
        public IDictionary<string, string> GlobalProperties => this.Source.GlobalProperties;
        public ICollection<string> ItemTypes => this.Source.ItemTypes;

        public ICollection<MockProjectPropertyLinkRemoter> Properties
            => this.OwningCollection.ExportCollection<ProjectProperty, MockProjectPropertyLinkRemoter>(this.Source.Properties);

        public IDictionary<string, List<string>> ConditionedProperties => this.Source.ConditionedProperties;

        public IDictionary<string, MockProjectItemDefinitionLinkRemoter> ItemDefinitions
            => this.OwningCollection.ExportDictionary<string, ProjectItemDefinition, MockProjectItemDefinitionLinkRemoter>(this.Source.ItemDefinitions);

        public ICollection<MockProjectItemLinkRemoter> Items => this.OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Source.Items);

        public ICollection<MockProjectItemLinkRemoter> ItemsIgnoringCondition => this.OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Source.ItemsIgnoringCondition);

        public IList<RemotedResolvedImport> Imports => this.Source.Imports.ConvertCollection<RemotedResolvedImport, ResolvedImport>((a) => a.Export(this.OwningCollection));

        public IList<RemotedResolvedImport> ImportsIncludingDuplicates
            => this.Source.Imports.ConvertCollection<RemotedResolvedImport, ResolvedImport>((a) => a.Export(this.OwningCollection));

        public ICollection<MockProjectPropertyLinkRemoter> AllEvaluatedProperties
            => this.OwningCollection.ExportCollection<ProjectProperty, MockProjectPropertyLinkRemoter>(this.Source.AllEvaluatedProperties);


        public IList<MockProjectMetadataLinkRemoter> AllEvaluatedItemDefinitionMetadata
            => this.OwningCollection.ExportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Source.AllEvaluatedItemDefinitionMetadata);

        public ICollection<MockProjectItemLinkRemoter> AllEvaluatedItems => this.OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Source.AllEvaluatedItems);

        public string ToolsVersion => this.Source.ToolsVersion;
        public string SubToolsetVersion => this.Source.SubToolsetVersion;
        public bool SkipEvaluation { get => this.Source.SkipEvaluation; set => this.Source.SkipEvaluation = value; }
        public bool DisableMarkDirty { get => this.Source.DisableMarkDirty; set => this.Source.DisableMarkDirty = value; }
        public bool IsBuildEnabled { get => this.Source.IsBuildEnabled; set => this.Source.IsBuildEnabled = value; }
        public int LastEvaluationId => this.Source.LastEvaluationId;
        public IList<MockProjectItemLinkRemoter> AddItem(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
            => this.OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Source.AddItem(itemType, unevaluatedInclude, metadata));
        public IList<MockProjectItemLinkRemoter> AddItemFast(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
            => this.OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Source.AddItemFast(itemType, unevaluatedInclude, metadata));

        public string ExpandString(string unexpandedValue) => this.Source.ExpandString(unexpandedValue);

        public ICollection<MockProjectItemLinkRemoter> GetItems(string itemType)
            => this.OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Source.GetItems(itemType));

        public  ICollection<MockProjectItemLinkRemoter> GetItemsByEvaluatedInclude(string evaluatedInclude)
            => this.OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Source.GetItemsByEvaluatedInclude(evaluatedInclude));

        public ICollection<MockProjectItemLinkRemoter> GetItemsIgnoringCondition(string itemType)
            => this.OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Source.GetItemsIgnoringCondition(itemType));

        public IEnumerable<MockProjectElementLinkRemoter> GetLogicalProject()
            => this.OwningCollection.ExportCollection(this.Source.GetLogicalProject());

        public MockProjectPropertyLinkRemoter GetProperty(string name) => this.OwningCollection.Export<ProjectProperty, MockProjectPropertyLinkRemoter>(this.Source.GetProperty(name));
        public  string GetPropertyValue(string name) => this.Source.GetPropertyValue(name);
        public void MarkDirty() => this.Source.MarkDirty();
        public void ReevaluateIfNecessary(EvaluationContext evaluationContext) => this.Source.ReevaluateIfNecessary(evaluationContext);
        public bool RemoveGlobalProperty(string name) => this.Source.RemoveGlobalProperty(name);

        public bool RemoveItem(MockProjectItemLinkRemoter item) => this.Source.RemoveItem(this.OwningCollection.Import<ProjectItem, MockProjectItemLinkRemoter>(item));

        public void RemoveItems(IEnumerable<MockProjectItemLinkRemoter> items)
            => this.Source.RemoveItems(this.OwningCollection.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(items));

        public bool RemoveProperty(MockProjectPropertyLinkRemoter propertyRemoter)
            => this.Source.RemoveProperty(this.OwningCollection.Import<ProjectProperty, MockProjectPropertyLinkRemoter>(propertyRemoter));

        public void SaveLogicalProject(TextWriter writer)
        {
            this.Source.SaveLogicalProject(writer);
        }

        public  bool SetGlobalProperty(string name, string escapedValue) => this.Source.SetGlobalProperty(name, escapedValue);

        public  MockProjectPropertyLinkRemoter SetProperty(string name, string unevaluatedValue)
            => this.OwningCollection.Export<ProjectProperty, MockProjectPropertyLinkRemoter>(this.Source.SetProperty(name, unevaluatedValue));
        public void Unload() { }
    }

    internal class MockProjectLink : ProjectLink, ILinkMock
    {
        public MockProjectLink(MockProjectLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => this.Proxy;

        #region ProjectLink
        public override ProjectRootElement Xml => (ProjectRootElement)this.Proxy.Xml.Import(this.Linker);

        public override bool ThrowInsteadOfSplittingItemElement { get => this.Proxy.ThrowInsteadOfSplittingItemElement; set => this.Proxy.ThrowInsteadOfSplittingItemElement = value; }

        public override bool IsDirty => this.Proxy.IsDirty;

        public override IDictionary<string, string> GlobalProperties => this.Proxy.GlobalProperties;

        public override ICollection<string> ItemTypes => this.Proxy.ItemTypes;

        public override ICollection<ProjectProperty> Properties => this.Linker.ImportCollection<ProjectProperty,MockProjectPropertyLinkRemoter>(this.Proxy.Properties);

        public override IDictionary<string, List<string>> ConditionedProperties => this.Proxy.ConditionedProperties;

        public override IDictionary<string, ProjectItemDefinition> ItemDefinitions
            => this.Linker.ImportDictionary<string, ProjectItemDefinition, MockProjectItemDefinitionLinkRemoter>(this.Proxy.ItemDefinitions);

        public override ICollection<ProjectItem> Items => this.Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Proxy.Items);

        public override ICollection<ProjectItem> ItemsIgnoringCondition => this.Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Proxy.ItemsIgnoringCondition);

        public override IList<ResolvedImport> Imports
            => this.Proxy.Imports.ConvertCollection<ResolvedImport, RemotedResolvedImport>((a) => a.Import(this.Linker));

        public override IList<ResolvedImport> ImportsIncludingDuplicates
            => this.Proxy.ImportsIncludingDuplicates.ConvertCollection<ResolvedImport, RemotedResolvedImport>((a) => a.Import(this.Linker));

        public override ICollection<ProjectProperty> AllEvaluatedProperties
            => this.Linker.ImportCollection<ProjectProperty, MockProjectPropertyLinkRemoter>(this.Proxy.AllEvaluatedProperties);
        public override ICollection<ProjectMetadata> AllEvaluatedItemDefinitionMetadata
            => this.Linker.ImportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Proxy.AllEvaluatedItemDefinitionMetadata);

        public override ICollection<ProjectItem> AllEvaluatedItems
            => this.Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Proxy.AllEvaluatedItems);

        public override string ToolsVersion => this.Proxy.ToolsVersion;

        public override string SubToolsetVersion => this.Proxy.SubToolsetVersion;

        public override bool SkipEvaluation { get => this.Proxy.SkipEvaluation; set => this.Proxy.SkipEvaluation = value; }
        public override bool DisableMarkDirty { get => this.Proxy.DisableMarkDirty; set => this.Proxy.DisableMarkDirty = value; }
        public override bool IsBuildEnabled { get => this.Proxy.IsBuildEnabled; set => this.Proxy.IsBuildEnabled = value; }

        public override int LastEvaluationId => this.Proxy.LastEvaluationId;

        public override IList<ProjectItem> AddItem(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
            => this.Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Proxy.AddItem(itemType, unevaluatedInclude, metadata));

        public override IList<ProjectItem> AddItemFast(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
            => this.Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Proxy.AddItemFast(itemType, unevaluatedInclude, metadata));

        // Building support is not required (and technically not supported for now
        public override bool Build(string[] targets, IEnumerable<ILogger> loggers, IEnumerable<ForwardingLoggerRecord> remoteLoggers, EvaluationContext evaluationContext)
            => throw new NotImplementedException();
        public override ProjectInstance CreateProjectInstance(ProjectInstanceSettings settings, EvaluationContext evaluationContext) => throw new NotImplementedException();
        public override IDictionary<string, ProjectTargetInstance> Targets => throw new NotImplementedException();
// --------------------------------------------------

        public override string ExpandString(string unexpandedValue) => this.Proxy.ExpandString(unexpandedValue);

// TODO: Glob is not needed for the CSproj, but we might want to test it at least 
        public override List<GlobResult> GetAllGlobs(EvaluationContext evaluationContext)
        {
            throw new NotImplementedException();
        }

        public override List<GlobResult> GetAllGlobs(string itemType, EvaluationContext evaluationContext)
        {
            throw new NotImplementedException();
        }

        public override List<ProvenanceResult> GetItemProvenance(string itemToMatch, EvaluationContext evaluationContext)
        {
            throw new NotImplementedException();
        }

        public override List<ProvenanceResult> GetItemProvenance(string itemToMatch, string itemType, EvaluationContext evaluationContext)
        {
            throw new NotImplementedException();
        }

        public override List<ProvenanceResult> GetItemProvenance(ProjectItem item, EvaluationContext evaluationContext)
        {
            throw new NotImplementedException();
        }
// ---------------------------------------------------------------------------------------

        public override ICollection<ProjectItem> GetItems(string itemType)
            => this.Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Proxy.GetItems(itemType));
        public override ICollection<ProjectItem> GetItemsByEvaluatedInclude(string evaluatedInclude)
            => this.Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Proxy.GetItemsByEvaluatedInclude(evaluatedInclude));

        public override ICollection<ProjectItem> GetItemsIgnoringCondition(string itemType)
            => this.Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(this.Proxy.GetItemsIgnoringCondition(itemType));

        public override IEnumerable<ProjectElement> GetLogicalProject()
            => this.Linker.ImportCollection<ProjectElement>(this.Proxy.GetLogicalProject());

        public override ProjectProperty GetProperty(string name) => this.Linker.Import<ProjectProperty, MockProjectPropertyLinkRemoter>(this.Proxy.GetProperty(name));
        public override string GetPropertyValue(string name) => this.Proxy.GetPropertyValue(name);
        public override void MarkDirty() => this.Proxy.MarkDirty();
        public override void ReevaluateIfNecessary(EvaluationContext evaluationContext) => this.Proxy.ReevaluateIfNecessary(evaluationContext);
        public override bool RemoveGlobalProperty(string name) => this.Proxy.RemoveGlobalProperty(name);

        public override bool RemoveItem(ProjectItem item)
            => this.Proxy.RemoveItem(this.Linker.Export<ProjectItem, MockProjectItemLinkRemoter>(item));

        public override void RemoveItems(IEnumerable<ProjectItem> items)
            => this.Proxy.RemoveItems(this.Linker.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(items));

        public override bool RemoveProperty(ProjectProperty property) => this.Proxy.RemoveProperty(this.Linker.Export<ProjectProperty, MockProjectPropertyLinkRemoter>(property));
        public override void SaveLogicalProject(TextWriter writer) => this.Proxy.SaveLogicalProject(writer);

        public override bool SetGlobalProperty(string name, string escapedValue) => this.Proxy.SetGlobalProperty(name, escapedValue);

        public override ProjectProperty SetProperty(string name, string unevaluatedValue) => this.Linker.Import<ProjectProperty, MockProjectPropertyLinkRemoter>(this.Proxy.SetProperty(name, unevaluatedValue));

        public override void Unload() => this.Proxy.Unload();
        #endregion
    }
}
