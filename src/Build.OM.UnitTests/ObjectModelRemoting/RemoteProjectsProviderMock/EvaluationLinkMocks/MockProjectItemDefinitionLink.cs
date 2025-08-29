﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.ObjectModelRemoting;

    internal sealed class MockProjectItemDefinitionLinkRemoter : MockLinkRemoter<ProjectItemDefinition>
    {
        public override ProjectItemDefinition CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectItemDefinitionLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }

        // ProjectItemDefinitionLink remoting
        public MockProjectLinkRemoter Project => this.OwningCollection.Export<Project, MockProjectLinkRemoter>(this.Source.Project);
        public string ItemType => this.Source.ItemType;
        public ICollection<MockProjectMetadataLinkRemoter> Metadata => this.OwningCollection.ExportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Source.Metadata);
        public MockProjectMetadataLinkRemoter GetMetadata(string name)
            => this.OwningCollection.Export<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Source.GetMetadata(name));
        public string GetMetadataValue(string name) => this.Source.GetMetadataValue(name);
        public MockProjectMetadataLinkRemoter SetMetadataValue(string name, string unevaluatedValue)
            => this.OwningCollection.Export<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Source.SetMetadataValue(name, unevaluatedValue));
    }

    internal sealed class MockProjectItemDefinitionLink : ProjectItemDefinitionLink, ILinkMock
    {
        public MockProjectItemDefinitionLink(MockProjectItemDefinitionLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectItemDefinitionLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => this.Proxy;

        // ProjectItemDefinitionLink
        public override Project Project => this.Linker.Import<Project, MockProjectLinkRemoter>(this.Proxy.Project);
        public override string ItemType => this.Proxy.ItemType;
        public override ICollection<ProjectMetadata> Metadata
            => this.Linker.ImportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Proxy.Metadata);
        public override ProjectMetadata GetMetadata(string name)
            => this.Linker.Import<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Proxy.GetMetadata(name));
        public override string GetMetadataValue(string name) => this.Proxy.GetMetadataValue(name);
        public override ProjectMetadata SetMetadataValue(string name, string unevaluatedValue)
            => this.Linker.Import<ProjectMetadata, MockProjectMetadataLinkRemoter>(this.Proxy.SetMetadataValue(name, unevaluatedValue));
    }
}
