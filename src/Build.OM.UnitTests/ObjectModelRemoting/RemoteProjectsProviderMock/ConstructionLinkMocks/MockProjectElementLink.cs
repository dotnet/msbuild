// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;

    internal abstract class MockProjectElementLinkRemoter : MockLinkRemoter<ProjectElement>
    {
        public MockProjectElementLinkRemoter Export(ProjectElement xml)
        {
            return this.OwningCollection.ExportElement(xml);
        }

        public abstract ProjectElement ImportImpl(ProjectCollectionLinker remote);


        // ProjectElementLink remoting
        public MockProjectElementContainerLinkRemoter Parent => (MockProjectElementContainerLinkRemoter)this.Export(Source.Parent);

        public MockProjectRootElementLinkRemoter ContainingProject => (MockProjectRootElementLinkRemoter)this.Export(Source.ContainingProject);

        public string ElementName => Source.ElementName;

        public string OuterElement => Source.OuterElement;

        public bool ExpressedAsAttribute { get => ProjectElementLink.GetExpressedAsAttribute(Source); set => ProjectElementLink.SetExpressedAsAttribute(Source, value); }

        public MockProjectElementLinkRemoter PreviousSibling => this.Export(Source.PreviousSibling);

        public MockProjectElementLinkRemoter NextSibling => this.Export(Source.NextSibling);

        public ElementLocation Location => Source.Location;

        public IReadOnlyCollection<XmlAttributeLink> Attributes => ProjectItemElementLink.GetAttributes(this.Source);

        public string PureText => ProjectItemElementLink.GetPureText(this.Source);

        public void CopyFrom(MockProjectElementLinkRemoter element)
        {
            this.Source.CopyFrom(element.Import(this.OwningCollection));
        }

        public MockProjectElementLinkRemoter CreateNewInstance(MockProjectRootElementLinkRemoter owner)
        {
            var pre = (ProjectRootElement)owner.Import(OwningCollection);
            var result = ProjectElementLink.CreateNewInstance(Source, pre);
            return Export(result);
        }

        public ElementLocation GetAttributeLocation(string attributeName)
            => ProjectElementLink.GetAttributeLocation(this.Source, attributeName);

        public string GetAttributeValue(string attributeName, bool nullIfNotExists)
        {
            return ProjectElementLink.GetAttributeValue(this.Source, attributeName, nullIfNotExists);
        }

        public void SetOrRemoveAttribute(string name, string value, bool clearAttributeCache, string reason, string param)
        {
            ProjectElementLink.SetOrRemoveAttribute(this.Source, name, value, clearAttributeCache, reason, param);
            if (reason != null)
            {
                ProjectElementLink.MarkDirty(this.Source, reason, param);
            }
        }
    }

    // not used - just a copy/paste template for remoting support objects of Construction model hierarchical elements.
    internal class TemplateProjectElementLink : ProjectElementLink, ILinkMock, IProjectElementLinkHelper
    {
        public TemplateProjectElementLink(MockProjectElementLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectElementLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => this.Proxy;
        MockProjectElementLinkRemoter IProjectElementLinkHelper.ElementProxy => this.Proxy;


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
