// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;

    internal sealed class MockProjectExtensionsElementLinkRemoter : MockProjectElementLinkRemoter
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
        public string Content { get => ExtensionXml.Content; set => ExtensionXml.Content = value; }
        public string GetSubElement(string name) { return ExtensionXml[name]; }
        public void SetSubElement(string name, string value) { ExtensionXml[name] = value; }
    }

    internal sealed class MockProjectExtensionsElementLink : ProjectExtensionsElementLink, ILinkMock, IProjectElementLinkHelper
    {
        public MockProjectExtensionsElementLink(MockProjectExtensionsElementLinkRemoter proxy, IImportHolder holder)
        {
            Holder = holder;
            Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => Holder.Linker;
        public MockProjectExtensionsElementLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => Proxy;
        MockProjectElementLinkRemoter IProjectElementLinkHelper.ElementProxy => Proxy;

        // - ProjectExtensionsElementLink ------
        public override string Content { get => Proxy.Content; set => Proxy.Content = value; }
        public override string GetSubElement(string name) { return Proxy.GetSubElement(name); }
        public override void SetSubElement(string name, string value) { Proxy.SetSubElement(name, value); }
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
