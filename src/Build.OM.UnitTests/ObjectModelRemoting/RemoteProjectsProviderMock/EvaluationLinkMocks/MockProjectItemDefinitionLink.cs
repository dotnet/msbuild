// Licensed to the .NET Foundation under one or more agreements.
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
        public MockProjectLinkRemoter Project => OwningCollection.Export<Project, MockProjectLinkRemoter>(Source.Project);
        public string ItemType => Source.ItemType;
        public ICollection<MockProjectMetadataLinkRemoter> Metadata => OwningCollection.ExportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(Source.Metadata);
        public MockProjectMetadataLinkRemoter GetMetadata(string name)
            => OwningCollection.Export<ProjectMetadata, MockProjectMetadataLinkRemoter>(Source.GetMetadata(name));
        public string GetMetadataValue(string name) => Source.GetMetadataValue(name);
        public MockProjectMetadataLinkRemoter SetMetadataValue(string name, string unevaluatedValue)
            => OwningCollection.Export<ProjectMetadata, MockProjectMetadataLinkRemoter>(Source.SetMetadataValue(name, unevaluatedValue));
    }

    internal sealed class MockProjectItemDefinitionLink : ProjectItemDefinitionLink, ILinkMock
    {
        public MockProjectItemDefinitionLink(MockProjectItemDefinitionLinkRemoter proxy, IImportHolder holder)
        {
            Holder = holder;
            Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => Holder.Linker;
        public MockProjectItemDefinitionLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => Proxy;

        // ProjectItemDefinitionLink
        public override Project Project => Linker.Import<Project, MockProjectLinkRemoter>(Proxy.Project);
        public override string ItemType => Proxy.ItemType;
        public override ICollection<ProjectMetadata> Metadata
            => Linker.ImportCollection<ProjectMetadata, MockProjectMetadataLinkRemoter>(Proxy.Metadata);
        public override ProjectMetadata GetMetadata(string name)
            => Linker.Import<ProjectMetadata, MockProjectMetadataLinkRemoter>(Proxy.GetMetadata(name));
        public override string GetMetadataValue(string name) => Proxy.GetMetadataValue(name);
        public override ProjectMetadata SetMetadataValue(string name, string unevaluatedValue)
            => Linker.Import<ProjectMetadata, MockProjectMetadataLinkRemoter>(Proxy.SetMetadataValue(name, unevaluatedValue));
    }
}
