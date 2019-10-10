// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;

    internal class MockProjectTaskElementLinkRemoter : MockProjectElementContainerLinkRemoter
    {
        public ProjectTaskElement TaskXml => (ProjectTaskElement)Source;

        public override ProjectElement ImportImpl(ProjectCollectionLinker remote)
        {
            return remote.Import<ProjectElement, MockProjectTaskElementLinkRemoter>(this);
        }

        public override ProjectElement CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectTaskElementLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }
        // ProjectTaskElementLink remote
        public IDictionary<string, string> Parameters
        {
            get
            {
                var local = this.TaskXml.Parameters;
                return local == null ? local : new Dictionary<string, string>(local);
            }
        }
        public IEnumerable<KeyValuePair<string, ElementLocation>> ParameterLocations => this.TaskXml.ParameterLocations;
        public string GetParameter(string name) { return this.TaskXml.GetParameter(name); }
        public void SetParameter(string name, string unevaluatedValue) { this.TaskXml.SetParameter(name, unevaluatedValue); }
        public void RemoveParameter(string name) { this.TaskXml.RemoveParameter(name); }
        public void RemoveAllParameters() { this.TaskXml.RemoveAllParameters(); }
    }

    internal class MockProjectTaskElementLink : ProjectTaskElementLink, ILinkMock, IProjectElementLinkHelper, IProjectElementContainerLinkHelper
    {
        public MockProjectTaskElementLink(MockProjectTaskElementLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectTaskElementLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => this.Proxy;
        MockProjectElementLinkRemoter IProjectElementLinkHelper.ElementProxy => this.Proxy;
        MockProjectElementContainerLinkRemoter IProjectElementContainerLinkHelper.ContainerProxy => this.Proxy;

        // ProjectTaskElementLink -----
        public override IDictionary<string, string> Parameters => this.Proxy.Parameters;
        public override IEnumerable<KeyValuePair<string, ElementLocation>> ParameterLocations => this.Proxy.ParameterLocations;
        public override string GetParameter(string name) { return this.Proxy.GetParameter(name); }
        // hmm did not know can use => on functions, can clean the milion other cases some tiem ...
        public override void SetParameter(string name, string unevaluatedValue) =>  this.Proxy.SetParameter(name, unevaluatedValue);
        public override void RemoveParameter(string name) => Proxy.RemoveParameter(name);
        public override void RemoveAllParameters() => Proxy.RemoveAllParameters();

        // ----------------------------

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
