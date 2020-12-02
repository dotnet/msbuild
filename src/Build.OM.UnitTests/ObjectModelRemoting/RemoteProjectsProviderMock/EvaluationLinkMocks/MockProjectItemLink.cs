// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.ObjectModelRemoting;

    internal class MockProjectItemLinkRemoter : MockLinkRemoter<ProjectItem>
    {
        public override ProjectItem CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectItemLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }


        ///  ProjectItemLink remoting
        public MockProjectLinkRemoter Project => this.OwningCollection.Export<Project, MockProjectLinkRemoter>(this.Source.Project);
        public MockProjectItemElementLinkRemoter Xml => (MockProjectItemElementLinkRemoter)this.OwningCollection.ExportElement(this.Source.Xml);
        public string EvaluatedInclude => this.Source.EvaluatedInclude;
        public ICollection<MockProjectMetadataLinkRemoter> MetadataCollection => this.OwningCollection.ExportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Source.Metadata);
        public ICollection<MockProjectMetadataLinkRemoter> DirectMetadata => this.OwningCollection.ExportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Source.DirectMetadata);
        public bool HasMetadata(string name) => this.Source.HasMetadata(name);
        public MockProjectMetadataLinkRemoter GetMetadata(string name)
            => this.OwningCollection.Export<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Source.GetMetadata(name));
        public string GetMetadataValue(string name) => this.Source.GetMetadataValue(name);
        public MockProjectMetadataLinkRemoter SetMetadataValue(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems)
            => this.OwningCollection.Export<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Source.SetMetadataValue(name, unevaluatedValue, propagateMetadataToSiblingItems));
        public bool RemoveMetadata(string name) => this.Source.RemoveMetadata(name);
        public void Rename(string name) => this.Source.Rename(name);
        public void ChangeItemType(string newItemType) => this.Source.ItemType = newItemType;
    }

    internal class MockProjectItemLink : ProjectItemLink, ILinkMock
    {
        public MockProjectItemLink(MockProjectItemLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectItemLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => this.Proxy;

        // ProjectItemLink

        public override Project Project => this.Linker.Import<Project, MockProjectLinkRemoter>(this.Proxy.Project);
        public override ProjectItemElement Xml => (ProjectItemElement)this.Proxy.Xml.Import(this.Linker);
        public override string EvaluatedInclude => this.Proxy.EvaluatedInclude;
        public override ICollection<ProjectMetadata> MetadataCollection
            => this.Linker.ImportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Proxy.MetadataCollection);
        public override ICollection<ProjectMetadata> DirectMetadata
            => this.Linker.ImportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Proxy.DirectMetadata);
        public override bool HasMetadata(string name) => this.Proxy.HasMetadata(name);
        public override ProjectMetadata GetMetadata(string name)
            => this.Linker.Import<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Proxy.GetMetadata(name));
        public override string GetMetadataValue(string name) => this.Proxy.GetMetadataValue(name);
        public override ProjectMetadata SetMetadataValue(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems)
            => this.Linker.Import<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Proxy.SetMetadataValue(name, unevaluatedValue, propagateMetadataToSiblingItems));
        public override bool RemoveMetadata(string name) => this.Proxy.RemoveMetadata(name);
        public override void Rename(string name) => this.Proxy.Rename(name);
        public override void ChangeItemType(string newItemType) => this.Proxy.ChangeItemType(newItemType);
    }
}
