﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;

    internal sealed class MockProjectMetadataElementLinkRemoter : MockProjectElementLinkRemoter
    {
        public ProjectMetadataElement MetadataXml => (ProjectMetadataElement)Source;

        public override ProjectElement ImportImpl(ProjectCollectionLinker remote)
        {
            return remote.Import<ProjectElement, MockProjectMetadataElementLinkRemoter>(this);
        }

        public override ProjectElement CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectMetadataElementLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }

        // ProjectMetadataElementLink support
        public string Value { get => MetadataXml.Value; set => MetadataXml.Value = value; }
        public void ChangeName(string newName) { MetadataXml.Name = newName; }
    }

    internal sealed class MockProjectMetadataElementLink : ProjectMetadataElementLink, ILinkMock, IProjectElementLinkHelper
    {
        public MockProjectMetadataElementLink(MockProjectMetadataElementLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectMetadataElementLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => this.Proxy;
        MockProjectElementLinkRemoter IProjectElementLinkHelper.ElementProxy => this.Proxy;

        public override string Value { get => this.Proxy.Value; set => this.Proxy.Value = value; }
        public override void ChangeName(string newName)
        {
            this.Proxy.ChangeName(newName);
        }

        #region ProjectElementLink redirectors
        private IProjectElementLinkHelper EImpl => (IProjectElementLinkHelper)this;
        public override ProjectElementContainer Parent => EImpl.GetParent();
        public override ProjectRootElement ContainingProject => EImpl.GetContainingProject();
        public override string ElementName => EImpl.GetElementName();
        public override string OuterElement => EImpl.GetOuterElement();
        public override bool ExpressedAsAttribute { get => EImpl.GetExpressedAsAttribute(); set => EImpl.SetExpressedAsAttribute(value); }
        public override ProjectElement PreviousSibling => EImpl.GetPreviousSibling();
        public override ProjectElement NextSibling => EImpl.GetNextSibling();
        public override ElementLocation Location => EImpl.GetLocation();
        public override IReadOnlyCollection<XmlAttributeLink> Attributes => EImpl.GetAttributes();
        public override string PureText => EImpl.GetPureText();
        public override void CopyFrom(ProjectElement element) => EImpl.CopyFrom(element);
        public override ProjectElement CreateNewInstance(ProjectRootElement owner) => EImpl.CreateNewInstance(owner);
        public override ElementLocation GetAttributeLocation(string attributeName) => EImpl.GetAttributeLocation(attributeName);
        public override string GetAttributeValue(string attributeName, bool nullIfNotExists) => EImpl.GetAttributeValue(attributeName, nullIfNotExists);
        public override void SetOrRemoveAttribute(string name, string value, bool clearAttributeCache, string reason, string param) => EImpl.SetOrRemoveAttribute(name, value, clearAttributeCache, reason, param);
        #endregion
    }
}
