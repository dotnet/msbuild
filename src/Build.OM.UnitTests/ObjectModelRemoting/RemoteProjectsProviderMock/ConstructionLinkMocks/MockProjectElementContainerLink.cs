// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Evaluation.Context;
    using Microsoft.Build.Execution;
    using Microsoft.Build.ObjectModelRemoting;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Logging;
    using System.Diagnostics;

    internal abstract class MockProjectElementContainerLinkRemoter : MockProjectElementLinkRemoter
    {
        public ProjectElementContainer ContainerXml => (ProjectElementContainer)Source;

        // ProjectElementContainerLink support
        public int Count => ContainerXml.Count;
        public MockProjectElementLinkRemoter FirstChild => this.Export(ContainerXml.FirstChild);
        public MockProjectElementLinkRemoter LastChild => this.Export(ContainerXml.LastChild);

        public void InsertAfterChild(MockProjectElementLinkRemoter child, MockProjectElementLinkRemoter reference)
        {
            this.ContainerXml.InsertAfterChild(child.Import(OwningCollection), reference.Import(OwningCollection));
        }

        public void InsertBeforeChild(MockProjectElementLinkRemoter child, MockProjectElementLinkRemoter reference)
        {
            this.ContainerXml.InsertBeforeChild(child.Import(OwningCollection), reference.Import(OwningCollection));
        }

        public void AddInitialChild(MockProjectElementLinkRemoter child)
        {
            ProjectElementContainerLink.AddInitialChild(this.ContainerXml, child.Import(OwningCollection));
        }

        public MockProjectElementContainerLinkRemoter DeepClone(MockProjectRootElementLinkRemoter factory, MockProjectElementContainerLinkRemoter parent)
        {
            var pre = (ProjectRootElement)factory.Import(OwningCollection);
            var pec = (ProjectElementContainer)parent.Import(OwningCollection);
            var result = ProjectElementContainerLink.DeepClone(this.ContainerXml, pre, pec);
            return (MockProjectElementContainerLinkRemoter)this.Export(result);
        }

        public void RemoveChild(MockProjectElementLinkRemoter child)
        {
            this.ContainerXml.RemoveChild(child.Import(this.OwningCollection));
        }
    }

    // not used - just a copy/paste template for remoting support objects of Construction model hierarchical containers.
    internal class TemplateProjectElementContainerLink : ProjectElementContainerLink, ILinkMock, IProjectElementLinkHelper, IProjectElementContainerLinkHelper
    {
        public TemplateProjectElementContainerLink(MockProjectElementContainerLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectElementContainerLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => this.Proxy;
        MockProjectElementLinkRemoter IProjectElementLinkHelper.ElementProxy => this.Proxy;
        MockProjectElementContainerLinkRemoter IProjectElementContainerLinkHelper.ContainerProxy => this.Proxy;


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

        #region ProjectElementContainer link redirectors
        private IProjectElementContainerLinkHelper CImpl => (IProjectElementContainerLinkHelper)this;
        public override int Count => CImpl.GetCount();
        public override ProjectElement FirstChild => CImpl.GetFirstChild();
        public override ProjectElement LastChild => CImpl.GetLastChild();
        public override void InsertAfterChild(ProjectElement child, ProjectElement reference) => CImpl.InsertAfterChild(child, reference);
        public override void InsertBeforeChild(ProjectElement child, ProjectElement reference) => CImpl.InsertBeforeChild(child, reference);
        public override void AddInitialChild(ProjectElement child) => CImpl.AddInitialChild(child);
        public override ProjectElementContainer DeepClone(ProjectRootElement factory, ProjectElementContainer parent) => CImpl.DeepClone(factory, parent);
        public override void RemoveChild(ProjectElement child) => CImpl.RemoveChild(child);
        #endregion
    }
}
