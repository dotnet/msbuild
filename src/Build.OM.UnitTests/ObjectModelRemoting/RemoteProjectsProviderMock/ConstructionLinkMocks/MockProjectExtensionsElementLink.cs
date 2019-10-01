// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;

    internal class MockProjectExtensionsElementLinkRemoter : MockProjectElementLinkRemoter
    {
        public ProjectExtensionsElement ExtensionXml => (ProjectExtensionsElement)Source;

        public override ProjectElement ImportImpl(ProjectCollectionLinker remote)
        {
            return remote.Import<ProjectElement, MockProjectExtensionsElementLinkRemoter>(this);
        }

        public override ProjectElement CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectExtensionsElementLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }

        // ProjectExtensionsElementLink support
        public string Content { get => this.ExtensionXml.Content; set => this.ExtensionXml.Content = value; }
        public string GetSubElement(string name) { return this.ExtensionXml[name]; }
        public void SetSubElement(string name, string value) { this.ExtensionXml[name] = value; }
    }

    internal class MockProjectExtensionsElementLink : ProjectExtensionsElementLink, ILinkMock, IProjectElementLinkHelper
    {
        public MockProjectExtensionsElementLink(MockProjectExtensionsElementLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectExtensionsElementLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => this.Proxy;
        MockProjectElementLinkRemoter IProjectElementLinkHelper.ElementProxy => this.Proxy;

        // - ProjectExtensionsElementLink ------
        public override string Content { get => Proxy.Content; set => Proxy.Content = value; }
        public override string GetSubElement(string name) { return this.Proxy.GetSubElement(name); }
        public override void SetSubElement(string name, string value) { this.Proxy.SetSubElement(name, value); }
        // -------------------------------------

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
