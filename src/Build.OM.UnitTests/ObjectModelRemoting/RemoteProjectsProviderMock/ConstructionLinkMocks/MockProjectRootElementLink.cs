// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Collections.Generic;
    using System;
    using System.IO;
    using System.Text;
    using System.Xml;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;

    internal class MockProjectRootElementLinkRemoter : MockProjectElementContainerLinkRemoter
    {
        ProjectRootElement ProjectXml => (ProjectRootElement)Source;

        public override ProjectElement CreateLinkedObject(IImportHolder holder)
        {
            var link = new MockProjectRootElementLink(this, holder);
            return holder.Linker.LinkFactory.Create(link);
        }

        public override ProjectElement ImportImpl(ProjectCollectionLinker remote)
        {
            return remote.Import<ProjectElement, MockProjectRootElementLinkRemoter>(this);
        }

        // ProjectRootElementLink remoting
        public int Version => this.ProjectXml.Version;
        public bool HasUnsavedChanges => this.ProjectXml.HasUnsavedChanges;
        public DateTime TimeLastChanged => this.ProjectXml.TimeLastChanged;
        public DateTime LastWriteTimeWhenRead => this.ProjectXml.LastWriteTimeWhenRead;
        public string DirectoryPath => this.ProjectXml.DirectoryPath;
        public string FullPath { get => this.ProjectXml.FullPath; set => this.ProjectXml.FullPath = value; }
        public ElementLocation ProjectFileLocation => this.ProjectXml.ProjectFileLocation;
        public Encoding Encoding => this.ProjectXml.Encoding; //!! more complicated in reality when passing cross process.
        public string RawXml => this.ProjectXml.RawXml;
        public bool PreserveFormatting => this.ProjectXml.PreserveFormatting;
        public MockProjectChooseElementLinkRemoter CreateChooseElement()
        {
            return (MockProjectChooseElementLinkRemoter)this.Export(this.ProjectXml.CreateChooseElement());
        }
        public MockProjectImportElementLinkRemoter CreateImportElement(string project)
        {
            return (MockProjectImportElementLinkRemoter)this.Export(this.ProjectXml.CreateImportElement(project));
        }
        public MockProjectItemElementLinkRemoter CreateItemElement(string itemType)
        {
            return (MockProjectItemElementLinkRemoter)this.Export(this.ProjectXml.CreateItemElement(itemType));
        }
        public MockProjectItemElementLinkRemoter CreateItemElement(string itemType, string include)
        {
            return (MockProjectItemElementLinkRemoter) this.Export(this.ProjectXml.CreateItemElement(itemType, include));
        }
        public MockProjectItemDefinitionElementLinkRemoter CreateItemDefinitionElement(string itemType)
        {
            return (MockProjectItemDefinitionElementLinkRemoter)this.Export(this.ProjectXml.CreateItemDefinitionElement(itemType));
        }
        public MockProjectItemDefinitionGroupElementLinkRemoter CreateItemDefinitionGroupElement()
        {
            return (MockProjectItemDefinitionGroupElementLinkRemoter)this.Export(this.ProjectXml.CreateItemDefinitionGroupElement());
        }
        public MockProjectItemGroupElementLinkRemoter CreateItemGroupElement()
        {
            return (MockProjectItemGroupElementLinkRemoter)this.Export(this.ProjectXml.CreateItemGroupElement());
        }
        public MockProjectImportGroupElementLinkRemoter CreateImportGroupElement()
        {
            return (MockProjectImportGroupElementLinkRemoter)this.Export(this.ProjectXml.CreateImportGroupElement());
        }
        public MockProjectMetadataElementLinkRemoter CreateMetadataElement(string name)
        {
            return (MockProjectMetadataElementLinkRemoter)this.Export(this.ProjectXml.CreateMetadataElement(name));
        }
        public MockProjectMetadataElementLinkRemoter CreateMetadataElement(string name, string unevaluatedValue)
        {
            return (MockProjectMetadataElementLinkRemoter)this.Export(this.ProjectXml.CreateMetadataElement(name, unevaluatedValue));
        }
        public MockProjectOnErrorElementLinkRemoter CreateOnErrorElement(string executeTargets)
        {
            return (MockProjectOnErrorElementLinkRemoter)this.Export(this.ProjectXml.CreateOnErrorElement(executeTargets));
        }
        public MockProjectOtherwiseElementLinkRemoter CreateOtherwiseElement()
        {
            return (MockProjectOtherwiseElementLinkRemoter)this.Export(this.ProjectXml.CreateOtherwiseElement());
        }
        public MockProjectOutputElementLinkRemoter CreateOutputElement(string taskParameter, string itemType, string propertyName)
        {
            return (MockProjectOutputElementLinkRemoter)this.Export(this.ProjectXml.CreateOutputElement(taskParameter, itemType, propertyName));
        }
        public MockProjectExtensionsElementLinkRemoter CreateProjectExtensionsElement()
        {
            return (MockProjectExtensionsElementLinkRemoter)this.Export(this.ProjectXml.CreateProjectExtensionsElement());
        }
        public MockProjectPropertyGroupElementLinkRemoter CreatePropertyGroupElement()
        {
            return (MockProjectPropertyGroupElementLinkRemoter)this.Export(this.ProjectXml.CreatePropertyGroupElement());
        }
        public MockProjectPropertyElementLinkRemoter CreatePropertyElement(string name)
        {
            return (MockProjectPropertyElementLinkRemoter)this.Export(this.ProjectXml.CreatePropertyElement(name));
        }
        public MockProjectTargetElementLinkRemoter CreateTargetElement(string name)
        {
            return (MockProjectTargetElementLinkRemoter)this.Export(this.ProjectXml.CreateTargetElement(name));
        }
        public MockProjectTaskElementLinkRemoter CreateTaskElement(string name)
        {
            return (MockProjectTaskElementLinkRemoter)this.Export(this.ProjectXml.CreateTaskElement(name));
        }
        public MockProjectUsingTaskElementLinkRemoter CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName, string runtime, string architecture)
        {
            return (MockProjectUsingTaskElementLinkRemoter)this.Export(this.ProjectXml.CreateUsingTaskElement(taskName, assemblyFile, assemblyName, runtime, architecture));
        }
        public MockUsingTaskParameterGroupElementLinkRemoter CreateUsingTaskParameterGroupElement()
        {
            return (MockUsingTaskParameterGroupElementLinkRemoter)this.Export(this.ProjectXml.CreateUsingTaskParameterGroupElement());
        }
        public MockProjectUsingTaskParameterElementLinkRemoter CreateUsingTaskParameterElement(string name, string output, string required, string parameterType)
        {
            return (MockProjectUsingTaskParameterElementLinkRemoter)this.Export(this.ProjectXml.CreateUsingTaskParameterElement(name, output, required, parameterType));
        }
        public MockProjectUsingTaskBodyElementLinkRemoter CreateUsingTaskBodyElement(string evaluate, string body)
        {
            return (MockProjectUsingTaskBodyElementLinkRemoter)this.Export(this.ProjectXml.CreateUsingTaskBodyElement(evaluate, body));
        }
        public MockProjectWhenElementLinkRemoter CreateWhenElement(string condition)
        {
            return (MockProjectWhenElementLinkRemoter)this.Export(this.ProjectXml.CreateWhenElement(condition));
        }
        public MockProjectSdkElementLinkRemoter CreateProjectSdkElement(string sdkName, string sdkVersion)
        {
            return (MockProjectSdkElementLinkRemoter)this.Export(this.ProjectXml.CreateProjectSdkElement(sdkName, sdkVersion));
        }

        public void Save(Encoding saveEncoding) { this.ProjectXml.Save(saveEncoding); }
        public void Save(TextWriter writer) { this.ProjectXml.Save(writer); }

        public void ReloadFrom(string path, bool throwIfUnsavedChanges, bool preserveFormatting) { this.ProjectXml.ReloadFrom(path, throwIfUnsavedChanges, preserveFormatting); }
        public void ReloadFrom(XmlReader reader, bool throwIfUnsavedChanges, bool preserveFormatting) { this.ProjectXml.ReloadFrom(reader, throwIfUnsavedChanges, preserveFormatting); }

        public void MarkDirty(string reason, string param) { ProjectElementLink.MarkDirty(this.Source, reason, param); }
    }


    internal class MockProjectRootElementLink : ProjectRootElementLink, ILinkMock, IProjectElementLinkHelper, IProjectElementContainerLinkHelper
    {
        public MockProjectRootElementLink(MockProjectRootElementLinkRemoter proxy, IImportHolder holder)
        {
            this.Holder = holder;
            this.Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => this.Holder.Linker;
        public MockProjectRootElementLinkRemoter Proxy { get; }
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

        // ProjectRootElementLink remoting
        public override int Version => Proxy.Version;
        public override bool HasUnsavedChanges => Proxy.HasUnsavedChanges;
        public override DateTime TimeLastChanged => Proxy.TimeLastChanged;
        public override DateTime LastWriteTimeWhenRead => Proxy.LastWriteTimeWhenRead;
        public override string DirectoryPath => Proxy.DirectoryPath;
        public override string FullPath { get => Proxy.FullPath; set => Proxy.FullPath = value; }
        public override ElementLocation ProjectFileLocation => Proxy.ProjectFileLocation;
        public override Encoding Encoding => Proxy.Encoding;
        public override string RawXml => Proxy.RawXml;
        public override bool PreserveFormatting => Proxy.PreserveFormatting;

        public override ProjectChooseElement CreateChooseElement()
        {
            return (ProjectChooseElement)this.Proxy.CreateChooseElement().Import(this.Linker);
        }

        public override ProjectImportElement CreateImportElement(string project)
        {
            return (ProjectImportElement)this.Proxy.CreateImportElement(project).Import(this.Linker);
        }

        public override ProjectItemElement CreateItemElement(string itemType)
        {
            return (ProjectItemElement)this.Proxy.CreateItemElement(itemType).Import(this.Linker);
        }

        public override ProjectItemElement CreateItemElement(string itemType, string include)
        {
            return (ProjectItemElement)this.Proxy.CreateItemElement(itemType, include).Import(this.Linker);
        }

        public override ProjectItemDefinitionElement CreateItemDefinitionElement(string itemType)
        {
            return (ProjectItemDefinitionElement)this.Proxy.CreateItemDefinitionElement(itemType).Import(this.Linker);
        }

        public override ProjectItemDefinitionGroupElement CreateItemDefinitionGroupElement()
        {
            return (ProjectItemDefinitionGroupElement)this.Proxy.CreateItemDefinitionGroupElement().Import(this.Linker);
        }

        public override ProjectItemGroupElement CreateItemGroupElement()
        {
            return (ProjectItemGroupElement)this.Proxy.CreateItemGroupElement().Import(this.Linker);
        }

        public override ProjectImportGroupElement CreateImportGroupElement()
        {
            return (ProjectImportGroupElement)this.Proxy.CreateImportGroupElement().Import(this.Linker);
        }

        public override ProjectMetadataElement CreateMetadataElement(string name)
        {
            return (ProjectMetadataElement)this.Proxy.CreateMetadataElement(name).Import(this.Linker);
        }

        public override ProjectMetadataElement CreateMetadataElement(string name, string unevaluatedValue)
        {
            return (ProjectMetadataElement)this.Proxy.CreateMetadataElement(name, unevaluatedValue).Import(this.Linker);
        }

        public override ProjectOnErrorElement CreateOnErrorElement(string executeTargets)
        {
            return (ProjectOnErrorElement)this.Proxy.CreateOnErrorElement(executeTargets).Import(this.Linker);
        }

        public override ProjectOtherwiseElement CreateOtherwiseElement()
        {
            return (ProjectOtherwiseElement)this.Proxy.CreateOtherwiseElement().Import(this.Linker);
        }

        public override ProjectOutputElement CreateOutputElement(string taskParameter, string itemType, string propertyName)
        {
            return (ProjectOutputElement)this.Proxy.CreateOutputElement(taskParameter, itemType, propertyName).Import(this.Linker);
        }
        public override ProjectExtensionsElement CreateProjectExtensionsElement()
        {
            return (ProjectExtensionsElement)this.Proxy.CreateProjectExtensionsElement().Import(this.Linker);
        }

        public override ProjectPropertyGroupElement CreatePropertyGroupElement()
        {
            return (ProjectPropertyGroupElement)this.Proxy.CreatePropertyGroupElement().Import(this.Linker);
        }

        public override ProjectPropertyElement CreatePropertyElement(string name)
        {
            return (ProjectPropertyElement)this.Proxy.CreatePropertyElement(name).Import(this.Linker);
        }

        public override ProjectTargetElement CreateTargetElement(string name)
        {
            return (ProjectTargetElement)this.Proxy.CreateTargetElement(name).Import(this.Linker);
        }
        public override ProjectTaskElement CreateTaskElement(string name)
        {
            return (ProjectTaskElement)this.Proxy.CreateTaskElement(name).Import(this.Linker);
        }
        public override ProjectUsingTaskElement CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName, string runtime, string architecture)
        {
            return (ProjectUsingTaskElement)this.Proxy.CreateUsingTaskElement(taskName, assemblyFile, assemblyName, runtime, architecture).Import(this.Linker);
        }
        public override UsingTaskParameterGroupElement CreateUsingTaskParameterGroupElement()
        {
            return (UsingTaskParameterGroupElement)this.Proxy.CreateUsingTaskParameterGroupElement().Import(this.Linker);
        }
        public override ProjectUsingTaskParameterElement CreateUsingTaskParameterElement(string name, string output, string required, string parameterType)
        {
            return (ProjectUsingTaskParameterElement)this.Proxy.CreateUsingTaskParameterElement(name, output, required, parameterType).Import(this.Linker);
        }
        public override ProjectUsingTaskBodyElement CreateUsingTaskBodyElement(string evaluate, string body)
        {
            return (ProjectUsingTaskBodyElement)this.Proxy.CreateUsingTaskBodyElement(evaluate, body).Import(this.Linker);
        }
        public override ProjectWhenElement CreateWhenElement(string condition)
        {
            return (ProjectWhenElement)this.Proxy.CreateWhenElement(condition).Import(this.Linker);
        }
        public override ProjectSdkElement CreateProjectSdkElement(string sdkName, string sdkVersion)
        {
            return (ProjectSdkElement)this.Proxy.CreateProjectSdkElement(sdkName, sdkVersion).Import(this.Linker);
        }
        public override void Save(Encoding saveEncoding)
        {
            this.Proxy.Save(saveEncoding);
        }
        public override void Save(TextWriter writer)
        {
            this.Proxy.Save(writer);
        }
        public override void ReloadFrom(string path, bool throwIfUnsavedChanges, bool preserveFormatting)
        {
            this.Proxy.ReloadFrom(path, throwIfUnsavedChanges, preserveFormatting);
        }
        public override void ReloadFrom(XmlReader reader, bool throwIfUnsavedChanges, bool preserveFormatting)
        {
            this.Proxy.ReloadFrom(reader, throwIfUnsavedChanges, preserveFormatting);
        }
        public override void MarkDirty(string reason, string param)
        {
            this.Proxy.MarkDirty(reason, param);
        }
    }
}
