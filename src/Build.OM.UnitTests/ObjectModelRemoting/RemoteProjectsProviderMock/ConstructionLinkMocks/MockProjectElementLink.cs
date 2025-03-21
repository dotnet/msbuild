// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;

    internal abstract class MockProjectElementLinkRemoter : MockLinkRemoter<ProjectElement>
    {
        public MockProjectElementLinkRemoter Export(ProjectElement xml)
        {
            return OwningCollection.ExportElement(xml);
        }

        public abstract ProjectElement ImportImpl(ProjectCollectionLinker remote);


        // ProjectElementLink remoting
        public MockProjectElementContainerLinkRemoter Parent => (MockProjectElementContainerLinkRemoter)Export(Source.Parent);

        public MockProjectRootElementLinkRemoter ContainingProject => (MockProjectRootElementLinkRemoter)Export(Source.ContainingProject);

        public string ElementName => Source.ElementName;

        public string OuterElement => Source.OuterElement;

        public bool ExpressedAsAttribute { get => ProjectElementLink.GetExpressedAsAttribute(Source); set => ProjectElementLink.SetExpressedAsAttribute(Source, value); }

        public MockProjectElementLinkRemoter PreviousSibling => Export(Source.PreviousSibling);

        public MockProjectElementLinkRemoter NextSibling => Export(Source.NextSibling);

        public ElementLocation Location => Source.Location;

        public IReadOnlyCollection<XmlAttributeLink> Attributes => ProjectElementLink.GetAttributes(Source);

        public string PureText => ProjectElementLink.GetPureText(Source);

        public void CopyFrom(MockProjectElementLinkRemoter element)
        {
            Source.CopyFrom(element.Import(OwningCollection));
        }

        public MockProjectElementLinkRemoter CreateNewInstance(MockProjectRootElementLinkRemoter owner)
        {
            var pre = (ProjectRootElement)owner.Import(OwningCollection);
            var result = ProjectElementLink.CreateNewInstance(Source, pre);
            return Export(result);
        }

        public ElementLocation GetAttributeLocation(string attributeName)
            => ProjectElementLink.GetAttributeLocation(Source, attributeName);

        public string GetAttributeValue(string attributeName, bool nullIfNotExists)
        {
            return ProjectElementLink.GetAttributeValue(Source, attributeName, nullIfNotExists);
        }

        public void SetOrRemoveAttribute(string name, string value, bool clearAttributeCache, string reason, string param)
        {
            ProjectElementLink.SetOrRemoveAttribute(Source, name, value, clearAttributeCache, reason, param);
            if (reason != null)
            {
                ProjectElementLink.MarkDirty(Source, reason, param);
            }
        }
    }

    // not used - just a copy/paste template for remoting support objects of Construction model hierarchical elements.
    internal sealed class TemplateProjectElementLink : ProjectElementLink, ILinkMock, IProjectElementLinkHelper
    {
        public TemplateProjectElementLink(MockProjectElementLinkRemoter proxy, IImportHolder holder)
        {
            Holder = holder;
            Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => Holder.Linker;
        public MockProjectElementLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => Proxy;
        MockProjectElementLinkRemoter IProjectElementLinkHelper.ElementProxy => Proxy;


        #region standard ProjectElementLink implementation
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
