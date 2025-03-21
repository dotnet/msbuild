// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Evaluation.Context;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Logging;
    using Microsoft.Build.ObjectModelRemoting;

    internal sealed class MockProjectLinkRemoter : MockLinkRemoter<Project>
    {
        public override Project CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }

        // ProjectLink remoting
        public MockProjectElementLinkRemoter Xml => OwningCollection.ExportElement(Source.Xml);

        public bool ThrowInsteadOfSplittingItemElement { get => Source.ThrowInsteadOfSplittingItemElement; set => Source.ThrowInsteadOfSplittingItemElement = value; }

        public bool IsDirty => Source.IsDirty;

        // all bellow are very inefficient,
        // in reality we do cache these collections  until invalidated and use lazy access for dictionaries.
        // TODO: Might bring that infrastructure here as well ...
        public IDictionary<string, string> GlobalProperties => Source.GlobalProperties;
        public ICollection<string> ItemTypes => Source.ItemTypes;

        public ICollection<MockProjectPropertyLinkRemoter> Properties
            => OwningCollection.ExportCollection<ProjectProperty, MockProjectPropertyLinkRemoter>(Source.Properties);

        public IDictionary<string, List<string>> ConditionedProperties => Source.ConditionedProperties;

        public IDictionary<string, MockProjectItemDefinitionLinkRemoter> ItemDefinitions
            => OwningCollection.ExportDictionary<string, ProjectItemDefinition, MockProjectItemDefinitionLinkRemoter>(Source.ItemDefinitions);

        public ICollection<MockProjectItemLinkRemoter> Items => OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(Source.Items);

        public ICollection<MockProjectItemLinkRemoter> ItemsIgnoringCondition => OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(Source.ItemsIgnoringCondition);

        public IList<RemotedResolvedImport> Imports => Source.Imports.ConvertCollection((a) => a.Export(OwningCollection));

        public IList<RemotedResolvedImport> ImportsIncludingDuplicates
            => Source.Imports.ConvertCollection((a) => a.Export(OwningCollection));

        public ICollection<MockProjectPropertyLinkRemoter> AllEvaluatedProperties
            => OwningCollection.ExportCollection<ProjectProperty, MockProjectPropertyLinkRemoter>(Source.AllEvaluatedProperties);


        public IList<MockProjectMetadataLinkRemoter> AllEvaluatedItemDefinitionMetadata
            => OwningCollection.ExportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(Source.AllEvaluatedItemDefinitionMetadata);

        public ICollection<MockProjectItemLinkRemoter> AllEvaluatedItems => OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(Source.AllEvaluatedItems);

        public string ToolsVersion => Source.ToolsVersion;
        public string SubToolsetVersion => Source.SubToolsetVersion;
        public bool SkipEvaluation { get => Source.SkipEvaluation; set => Source.SkipEvaluation = value; }
        public bool DisableMarkDirty { get => Source.DisableMarkDirty; set => Source.DisableMarkDirty = value; }
        public bool IsBuildEnabled { get => Source.IsBuildEnabled; set => Source.IsBuildEnabled = value; }
        public int LastEvaluationId => Source.LastEvaluationId;
        public IList<MockProjectItemLinkRemoter> AddItem(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
            => OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(Source.AddItem(itemType, unevaluatedInclude, metadata));
        public IList<MockProjectItemLinkRemoter> AddItemFast(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
            => OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(Source.AddItemFast(itemType, unevaluatedInclude, metadata));

        public string ExpandString(string unexpandedValue) => Source.ExpandString(unexpandedValue);

        public ICollection<MockProjectItemLinkRemoter> GetItems(string itemType)
            => OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(Source.GetItems(itemType));

        public ICollection<MockProjectItemLinkRemoter> GetItemsByEvaluatedInclude(string evaluatedInclude)
            => OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(Source.GetItemsByEvaluatedInclude(evaluatedInclude));

        public ICollection<MockProjectItemLinkRemoter> GetItemsIgnoringCondition(string itemType)
            => OwningCollection.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(Source.GetItemsIgnoringCondition(itemType));

        public IEnumerable<MockProjectElementLinkRemoter> GetLogicalProject()
            => OwningCollection.ExportCollection(Source.GetLogicalProject());

        public MockProjectPropertyLinkRemoter GetProperty(string name) => OwningCollection.Export<ProjectProperty, MockProjectPropertyLinkRemoter>(Source.GetProperty(name));
        public string GetPropertyValue(string name) => Source.GetPropertyValue(name);
        public void MarkDirty() => Source.MarkDirty();
        public void ReevaluateIfNecessary(EvaluationContext evaluationContext) => Source.ReevaluateIfNecessary(evaluationContext);
        public bool RemoveGlobalProperty(string name) => Source.RemoveGlobalProperty(name);

        public bool RemoveItem(MockProjectItemLinkRemoter item) => Source.RemoveItem(OwningCollection.Import<ProjectItem, MockProjectItemLinkRemoter>(item));

        public void RemoveItems(IEnumerable<MockProjectItemLinkRemoter> items)
            => Source.RemoveItems(OwningCollection.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(items));

        public bool RemoveProperty(MockProjectPropertyLinkRemoter propertyRemoter)
            => Source.RemoveProperty(OwningCollection.Import<ProjectProperty, MockProjectPropertyLinkRemoter>(propertyRemoter));

        public void SaveLogicalProject(TextWriter writer)
        {
            Source.SaveLogicalProject(writer);
        }

        public bool SetGlobalProperty(string name, string escapedValue) => Source.SetGlobalProperty(name, escapedValue);

        public MockProjectPropertyLinkRemoter SetProperty(string name, string unevaluatedValue)
            => OwningCollection.Export<ProjectProperty, MockProjectPropertyLinkRemoter>(Source.SetProperty(name, unevaluatedValue));
        public void Unload() { }
    }

    internal sealed class MockProjectLink : ProjectLink, ILinkMock
    {
        public MockProjectLink(MockProjectLinkRemoter proxy, IImportHolder holder)
        {
            Holder = holder;
            Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => Holder.Linker;
        public MockProjectLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => Proxy;

        #region ProjectLink
        public override ProjectRootElement Xml => (ProjectRootElement)Proxy.Xml.Import(Linker);

        public override bool ThrowInsteadOfSplittingItemElement { get => Proxy.ThrowInsteadOfSplittingItemElement; set => Proxy.ThrowInsteadOfSplittingItemElement = value; }

        public override bool IsDirty => Proxy.IsDirty;

        public override IDictionary<string, string> GlobalProperties => Proxy.GlobalProperties;

        public override ICollection<string> ItemTypes => Proxy.ItemTypes;

        public override ICollection<ProjectProperty> Properties => Linker.ImportCollection<ProjectProperty, MockProjectPropertyLinkRemoter>(Proxy.Properties);

        public override IDictionary<string, List<string>> ConditionedProperties => Proxy.ConditionedProperties;

        public override IDictionary<string, ProjectItemDefinition> ItemDefinitions
            => Linker.ImportDictionary<string, ProjectItemDefinition, MockProjectItemDefinitionLinkRemoter>(Proxy.ItemDefinitions);

        public override ICollection<ProjectItem> Items => Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(Proxy.Items);

        public override ICollection<ProjectItem> ItemsIgnoringCondition => Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(Proxy.ItemsIgnoringCondition);

        public override IList<ResolvedImport> Imports
            => Proxy.Imports.ConvertCollection((a) => a.Import(Linker));

        public override IList<ResolvedImport> ImportsIncludingDuplicates
            => Proxy.ImportsIncludingDuplicates.ConvertCollection((a) => a.Import(Linker));

        public override ICollection<ProjectProperty> AllEvaluatedProperties
            => Linker.ImportCollection<ProjectProperty, MockProjectPropertyLinkRemoter>(Proxy.AllEvaluatedProperties);
        public override ICollection<ProjectMetadata> AllEvaluatedItemDefinitionMetadata
            => Linker.ImportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(Proxy.AllEvaluatedItemDefinitionMetadata);

        public override ICollection<ProjectItem> AllEvaluatedItems
            => Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(Proxy.AllEvaluatedItems);

        public override string ToolsVersion => Proxy.ToolsVersion;

        public override string SubToolsetVersion => Proxy.SubToolsetVersion;

        public override bool SkipEvaluation { get => Proxy.SkipEvaluation; set => Proxy.SkipEvaluation = value; }
        public override bool DisableMarkDirty { get => Proxy.DisableMarkDirty; set => Proxy.DisableMarkDirty = value; }
        public override bool IsBuildEnabled { get => Proxy.IsBuildEnabled; set => Proxy.IsBuildEnabled = value; }

        public override int LastEvaluationId => Proxy.LastEvaluationId;

        public override IList<ProjectItem> AddItem(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
            => Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(Proxy.AddItem(itemType, unevaluatedInclude, metadata));

        public override IList<ProjectItem> AddItemFast(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
            => Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(Proxy.AddItemFast(itemType, unevaluatedInclude, metadata));

        // Building support is not required (and technically not supported for now
        public override bool Build(string[] targets, IEnumerable<ILogger> loggers, IEnumerable<ForwardingLoggerRecord> remoteLoggers, EvaluationContext evaluationContext)
            => throw new NotImplementedException();
        public override ProjectInstance CreateProjectInstance(ProjectInstanceSettings settings, EvaluationContext evaluationContext) => throw new NotImplementedException();
        public override IDictionary<string, ProjectTargetInstance> Targets => throw new NotImplementedException();
        // --------------------------------------------------

        public override string ExpandString(string unexpandedValue) => Proxy.ExpandString(unexpandedValue);

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
            => Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(Proxy.GetItems(itemType));
        public override ICollection<ProjectItem> GetItemsByEvaluatedInclude(string evaluatedInclude)
            => Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(Proxy.GetItemsByEvaluatedInclude(evaluatedInclude));

        public override ICollection<ProjectItem> GetItemsIgnoringCondition(string itemType)
            => Linker.ImportCollection<ProjectItem, MockProjectItemLinkRemoter>(Proxy.GetItemsIgnoringCondition(itemType));

        public override IEnumerable<ProjectElement> GetLogicalProject()
            => Linker.ImportCollection<ProjectElement>(Proxy.GetLogicalProject());

        public override ProjectProperty GetProperty(string name) => Linker.Import<ProjectProperty, MockProjectPropertyLinkRemoter>(Proxy.GetProperty(name));
        public override string GetPropertyValue(string name) => Proxy.GetPropertyValue(name);
        public override void MarkDirty() => Proxy.MarkDirty();
        public override void ReevaluateIfNecessary(EvaluationContext evaluationContext) => Proxy.ReevaluateIfNecessary(evaluationContext);
        public override bool RemoveGlobalProperty(string name) => Proxy.RemoveGlobalProperty(name);

        public override bool RemoveItem(ProjectItem item)
            => Proxy.RemoveItem(Linker.Export<ProjectItem, MockProjectItemLinkRemoter>(item));

        public override void RemoveItems(IEnumerable<ProjectItem> items)
            => Proxy.RemoveItems(Linker.ExportCollection<ProjectItem, MockProjectItemLinkRemoter>(items));

        public override bool RemoveProperty(ProjectProperty property) => Proxy.RemoveProperty(Linker.Export<ProjectProperty, MockProjectPropertyLinkRemoter>(property));
        public override void SaveLogicalProject(TextWriter writer) => Proxy.SaveLogicalProject(writer);

        public override bool SetGlobalProperty(string name, string escapedValue) => Proxy.SetGlobalProperty(name, escapedValue);

        public override ProjectProperty SetProperty(string name, string unevaluatedValue) => Linker.Import<ProjectProperty, MockProjectPropertyLinkRemoter>(Proxy.SetProperty(name, unevaluatedValue));

        public override void Unload() => Proxy.Unload();
        #endregion
    }
}
