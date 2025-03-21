// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.ObjectModelRemoting;

    internal sealed class MockProjectMetadataLinkRemoter : MockLinkRemoter<ProjectMetadata>
    {
        public override ProjectMetadata CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectMetadataLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }

        // ProjectMetadataLink remoting
        public object Parent
        {
            get
            {
                var parent = ProjectMetadataLink.GetParent(Source);
                if (parent == null)
                {
                    return null;
                }

                var itemParent = parent as ProjectItem;
                if (itemParent != null)
                {
                    return OwningCollection.Export<ProjectItem, MockProjectItemLinkRemoter>(itemParent);
                }

                return OwningCollection.Export<ProjectItemDefinition, MockProjectItemDefinitionLinkRemoter>((ProjectItemDefinition)parent);
            }
        }

        public MockProjectMetadataElementLinkRemoter Xml => (MockProjectMetadataElementLinkRemoter)OwningCollection.ExportElement(Source.Xml);
        public string EvaluatedValueEscaped => ProjectMetadataLink.GetEvaluatedValueEscaped(Source);
        public MockProjectMetadataLinkRemoter Predecessor => OwningCollection.Export<ProjectMetadata, MockProjectMetadataLinkRemoter>(Source.Predecessor);
    }

    internal sealed class MockProjectMetadataLink : ProjectMetadataLink, ILinkMock
    {
        public MockProjectMetadataLink(MockProjectMetadataLinkRemoter proxy, IImportHolder holder)
        {
            Holder = holder;
            Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => Holder.Linker;
        public MockProjectMetadataLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => Proxy;

        // ProjectMetadataLink
        public override object Parent
        {
            get
            {
                var parentRemoter = Proxy.Parent;
                if (parentRemoter == null)
                {
                    return null;
                }

                var itemParent = parentRemoter as MockProjectItemLinkRemoter;
                if (itemParent != null)
                {
                    return Linker.Import<ProjectItem, MockProjectItemLinkRemoter>(itemParent);
                }

                return Linker.Import<ProjectItemDefinition, MockProjectItemDefinitionLinkRemoter>((MockProjectItemDefinitionLinkRemoter)parentRemoter);
            }
        }

        public override ProjectMetadataElement Xml => (ProjectMetadataElement)Proxy.Xml.Import(Linker);
        public override string EvaluatedValueEscaped => Proxy.EvaluatedValueEscaped;
        public override ProjectMetadata Predecessor => Linker.Import<ProjectMetadata, MockProjectMetadataLinkRemoter>(Proxy.Predecessor);
    }
}
