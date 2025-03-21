// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.ObjectModelRemoting;

    internal sealed class MockProjectItemLinkRemoter : MockLinkRemoter<ProjectItem>
    {
        public override ProjectItem CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectItemLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }

        // ProjectItemLink remoting
        public MockProjectLinkRemoter Project => OwningCollection.Export<Project, MockProjectLinkRemoter>(Source.Project);
        public MockProjectItemElementLinkRemoter Xml => (MockProjectItemElementLinkRemoter)OwningCollection.ExportElement(Source.Xml);
        public string EvaluatedInclude => Source.EvaluatedInclude;
        public ICollection<MockProjectMetadataLinkRemoter> MetadataCollection => OwningCollection.ExportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(Source.Metadata);
        public ICollection<MockProjectMetadataLinkRemoter> DirectMetadata => OwningCollection.ExportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(Source.DirectMetadata);
        public bool HasMetadata(string name) => Source.HasMetadata(name);
        public MockProjectMetadataLinkRemoter GetMetadata(string name)
            => OwningCollection.Export<ProjectMetadata, MockProjectMetadataLinkRemoter>(Source.GetMetadata(name));
        public string GetMetadataValue(string name) => Source.GetMetadataValue(name);
        public MockProjectMetadataLinkRemoter SetMetadataValue(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems)
            => OwningCollection.Export<ProjectMetadata, MockProjectMetadataLinkRemoter>(Source.SetMetadataValue(name, unevaluatedValue, propagateMetadataToSiblingItems));
        public bool RemoveMetadata(string name) => Source.RemoveMetadata(name);
        public void Rename(string name) => Source.Rename(name);
        public void ChangeItemType(string newItemType) => Source.ItemType = newItemType;
    }

    internal sealed class MockProjectItemLink : ProjectItemLink, ILinkMock
    {
        public MockProjectItemLink(MockProjectItemLinkRemoter proxy, IImportHolder holder)
        {
            Holder = holder;
            Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => Holder.Linker;
        public MockProjectItemLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => Proxy;

        // ProjectItemLink

        public override Project Project => Linker.Import<Project, MockProjectLinkRemoter>(Proxy.Project);
        public override ProjectItemElement Xml => (ProjectItemElement)Proxy.Xml.Import(Linker);
        public override string EvaluatedInclude => Proxy.EvaluatedInclude;
        public override ICollection<ProjectMetadata> MetadataCollection
            => Linker.ImportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(Proxy.MetadataCollection);
        public override ICollection<ProjectMetadata> DirectMetadata
            => Linker.ImportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(Proxy.DirectMetadata);
        public override bool HasMetadata(string name) => Proxy.HasMetadata(name);
        public override ProjectMetadata GetMetadata(string name)
            => Linker.Import<ProjectMetadata, MockProjectMetadataLinkRemoter>(Proxy.GetMetadata(name));
        public override string GetMetadataValue(string name) => Proxy.GetMetadataValue(name);
        public override ProjectMetadata SetMetadataValue(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems)
            => Linker.Import<ProjectMetadata, MockProjectMetadataLinkRemoter>(Proxy.SetMetadataValue(name, unevaluatedValue, propagateMetadataToSiblingItems));
        public override bool RemoveMetadata(string name) => Proxy.RemoveMetadata(name);
        public override void Rename(string name) => Proxy.Rename(name);
        public override void ChangeItemType(string newItemType) => Proxy.ChangeItemType(newItemType);
    }
}
