// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Xml;
    using Microsoft.Build.Construction;
    using Microsoft.Build.ObjectModelRemoting;

    internal sealed class MockProjectRootElementLinkRemoter : MockProjectElementContainerLinkRemoter
    {
        private ProjectRootElement ProjectXml => (ProjectRootElement)Source;

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
        public int Version => ProjectXml.Version;
        public bool HasUnsavedChanges => ProjectXml.HasUnsavedChanges;
        public DateTime TimeLastChanged => ProjectXml.TimeLastChanged;
        public DateTime LastWriteTimeWhenRead => ProjectXml.LastWriteTimeWhenRead;
        public string DirectoryPath => ProjectXml.DirectoryPath;
        public string FullPath { get => ProjectXml.FullPath; set => ProjectXml.FullPath = value; }
        public ElementLocation ProjectFileLocation => ProjectXml.ProjectFileLocation;
        public Encoding Encoding => ProjectXml.Encoding; // !! more complicated in reality when passing cross process.
        public string RawXml => ProjectXml.RawXml;
        public bool PreserveFormatting => ProjectXml.PreserveFormatting;
        public MockProjectChooseElementLinkRemoter CreateChooseElement()
        {
            return (MockProjectChooseElementLinkRemoter)Export(ProjectXml.CreateChooseElement());
        }
        public MockProjectImportElementLinkRemoter CreateImportElement(string project)
        {
            return (MockProjectImportElementLinkRemoter)Export(ProjectXml.CreateImportElement(project));
        }
        public MockProjectItemElementLinkRemoter CreateItemElement(string itemType)
        {
            return (MockProjectItemElementLinkRemoter)Export(ProjectXml.CreateItemElement(itemType));
        }
        public MockProjectItemElementLinkRemoter CreateItemElement(string itemType, string include)
        {
            return (MockProjectItemElementLinkRemoter)Export(ProjectXml.CreateItemElement(itemType, include));
        }
        public MockProjectItemDefinitionElementLinkRemoter CreateItemDefinitionElement(string itemType)
        {
            return (MockProjectItemDefinitionElementLinkRemoter)Export(ProjectXml.CreateItemDefinitionElement(itemType));
        }
        public MockProjectItemDefinitionGroupElementLinkRemoter CreateItemDefinitionGroupElement()
        {
            return (MockProjectItemDefinitionGroupElementLinkRemoter)Export(ProjectXml.CreateItemDefinitionGroupElement());
        }
        public MockProjectItemGroupElementLinkRemoter CreateItemGroupElement()
        {
            return (MockProjectItemGroupElementLinkRemoter)Export(ProjectXml.CreateItemGroupElement());
        }
        public MockProjectImportGroupElementLinkRemoter CreateImportGroupElement()
        {
            return (MockProjectImportGroupElementLinkRemoter)Export(ProjectXml.CreateImportGroupElement());
        }
        public MockProjectMetadataElementLinkRemoter CreateMetadataElement(string name)
        {
            return (MockProjectMetadataElementLinkRemoter)Export(ProjectXml.CreateMetadataElement(name));
        }
        public MockProjectMetadataElementLinkRemoter CreateMetadataElement(string name, string unevaluatedValue)
        {
            return (MockProjectMetadataElementLinkRemoter)Export(ProjectXml.CreateMetadataElement(name, unevaluatedValue));
        }
        public MockProjectOnErrorElementLinkRemoter CreateOnErrorElement(string executeTargets)
        {
            return (MockProjectOnErrorElementLinkRemoter)Export(ProjectXml.CreateOnErrorElement(executeTargets));
        }
        public MockProjectOtherwiseElementLinkRemoter CreateOtherwiseElement()
        {
            return (MockProjectOtherwiseElementLinkRemoter)Export(ProjectXml.CreateOtherwiseElement());
        }
        public MockProjectOutputElementLinkRemoter CreateOutputElement(string taskParameter, string itemType, string propertyName)
        {
            return (MockProjectOutputElementLinkRemoter)Export(ProjectXml.CreateOutputElement(taskParameter, itemType, propertyName));
        }
        public MockProjectExtensionsElementLinkRemoter CreateProjectExtensionsElement()
        {
            return (MockProjectExtensionsElementLinkRemoter)Export(ProjectXml.CreateProjectExtensionsElement());
        }
        public MockProjectPropertyGroupElementLinkRemoter CreatePropertyGroupElement()
        {
            return (MockProjectPropertyGroupElementLinkRemoter)Export(ProjectXml.CreatePropertyGroupElement());
        }
        public MockProjectPropertyElementLinkRemoter CreatePropertyElement(string name)
        {
            return (MockProjectPropertyElementLinkRemoter)Export(ProjectXml.CreatePropertyElement(name));
        }
        public MockProjectTargetElementLinkRemoter CreateTargetElement(string name)
        {
            return (MockProjectTargetElementLinkRemoter)Export(ProjectXml.CreateTargetElement(name));
        }
        public MockProjectTaskElementLinkRemoter CreateTaskElement(string name)
        {
            return (MockProjectTaskElementLinkRemoter)Export(ProjectXml.CreateTaskElement(name));
        }
        public MockProjectUsingTaskElementLinkRemoter CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName, string runtime, string architecture)
        {
            return (MockProjectUsingTaskElementLinkRemoter)Export(ProjectXml.CreateUsingTaskElement(taskName, assemblyFile, assemblyName, runtime, architecture));
        }
        public MockUsingTaskParameterGroupElementLinkRemoter CreateUsingTaskParameterGroupElement()
        {
            return (MockUsingTaskParameterGroupElementLinkRemoter)Export(ProjectXml.CreateUsingTaskParameterGroupElement());
        }
        public MockProjectUsingTaskParameterElementLinkRemoter CreateUsingTaskParameterElement(string name, string output, string required, string parameterType)
        {
            return (MockProjectUsingTaskParameterElementLinkRemoter)Export(ProjectXml.CreateUsingTaskParameterElement(name, output, required, parameterType));
        }
        public MockProjectUsingTaskBodyElementLinkRemoter CreateUsingTaskBodyElement(string evaluate, string body)
        {
            return (MockProjectUsingTaskBodyElementLinkRemoter)Export(ProjectXml.CreateUsingTaskBodyElement(evaluate, body));
        }
        public MockProjectWhenElementLinkRemoter CreateWhenElement(string condition)
        {
            return (MockProjectWhenElementLinkRemoter)Export(ProjectXml.CreateWhenElement(condition));
        }
        public MockProjectSdkElementLinkRemoter CreateProjectSdkElement(string sdkName, string sdkVersion)
        {
            return (MockProjectSdkElementLinkRemoter)Export(ProjectXml.CreateProjectSdkElement(sdkName, sdkVersion));
        }

        public void Save(Encoding saveEncoding) { ProjectXml.Save(saveEncoding); }
        public void Save(TextWriter writer) { ProjectXml.Save(writer); }

        public void ReloadFrom(string path, bool throwIfUnsavedChanges, bool preserveFormatting) { ProjectXml.ReloadFrom(path, throwIfUnsavedChanges, preserveFormatting); }
        public void ReloadFrom(XmlReader reader, bool throwIfUnsavedChanges, bool preserveFormatting) { ProjectXml.ReloadFrom(reader, throwIfUnsavedChanges, preserveFormatting); }

        public void MarkDirty(string reason, string param) { ProjectElementLink.MarkDirty(Source, reason, param); }
    }


    internal sealed class MockProjectRootElementLink : ProjectRootElementLink, ILinkMock, IProjectElementLinkHelper, IProjectElementContainerLinkHelper
    {
        public MockProjectRootElementLink(MockProjectRootElementLinkRemoter proxy, IImportHolder holder)
        {
            Holder = holder;
            Proxy = proxy;
        }

        public IImportHolder Holder { get; }
        public ProjectCollectionLinker Linker => Holder.Linker;
        public MockProjectRootElementLinkRemoter Proxy { get; }
        object ILinkMock.Remoter => Proxy;
        MockProjectElementLinkRemoter IProjectElementLinkHelper.ElementProxy => Proxy;
        MockProjectElementContainerLinkRemoter IProjectElementContainerLinkHelper.ContainerProxy => Proxy;

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
            return (ProjectChooseElement)Proxy.CreateChooseElement().Import(Linker);
        }

        public override ProjectImportElement CreateImportElement(string project)
        {
            return (ProjectImportElement)Proxy.CreateImportElement(project).Import(Linker);
        }

        public override ProjectItemElement CreateItemElement(string itemType)
        {
            return (ProjectItemElement)Proxy.CreateItemElement(itemType).Import(Linker);
        }

        public override ProjectItemElement CreateItemElement(string itemType, string include)
        {
            return (ProjectItemElement)Proxy.CreateItemElement(itemType, include).Import(Linker);
        }

        public override ProjectItemDefinitionElement CreateItemDefinitionElement(string itemType)
        {
            return (ProjectItemDefinitionElement)Proxy.CreateItemDefinitionElement(itemType).Import(Linker);
        }

        public override ProjectItemDefinitionGroupElement CreateItemDefinitionGroupElement()
        {
            return (ProjectItemDefinitionGroupElement)Proxy.CreateItemDefinitionGroupElement().Import(Linker);
        }

        public override ProjectItemGroupElement CreateItemGroupElement()
        {
            return (ProjectItemGroupElement)Proxy.CreateItemGroupElement().Import(Linker);
        }

        public override ProjectImportGroupElement CreateImportGroupElement()
        {
            return (ProjectImportGroupElement)Proxy.CreateImportGroupElement().Import(Linker);
        }

        public override ProjectMetadataElement CreateMetadataElement(string name)
        {
            return (ProjectMetadataElement)Proxy.CreateMetadataElement(name).Import(Linker);
        }

        public override ProjectMetadataElement CreateMetadataElement(string name, string unevaluatedValue)
        {
            return (ProjectMetadataElement)Proxy.CreateMetadataElement(name, unevaluatedValue).Import(Linker);
        }

        public override ProjectOnErrorElement CreateOnErrorElement(string executeTargets)
        {
            return (ProjectOnErrorElement)Proxy.CreateOnErrorElement(executeTargets).Import(Linker);
        }

        public override ProjectOtherwiseElement CreateOtherwiseElement()
        {
            return (ProjectOtherwiseElement)Proxy.CreateOtherwiseElement().Import(Linker);
        }

        public override ProjectOutputElement CreateOutputElement(string taskParameter, string itemType, string propertyName)
        {
            return (ProjectOutputElement)Proxy.CreateOutputElement(taskParameter, itemType, propertyName).Import(Linker);
        }
        public override ProjectExtensionsElement CreateProjectExtensionsElement()
        {
            return (ProjectExtensionsElement)Proxy.CreateProjectExtensionsElement().Import(Linker);
        }

        public override ProjectPropertyGroupElement CreatePropertyGroupElement()
        {
            return (ProjectPropertyGroupElement)Proxy.CreatePropertyGroupElement().Import(Linker);
        }

        public override ProjectPropertyElement CreatePropertyElement(string name)
        {
            return (ProjectPropertyElement)Proxy.CreatePropertyElement(name).Import(Linker);
        }

        public override ProjectTargetElement CreateTargetElement(string name)
        {
            return (ProjectTargetElement)Proxy.CreateTargetElement(name).Import(Linker);
        }
        public override ProjectTaskElement CreateTaskElement(string name)
        {
            return (ProjectTaskElement)Proxy.CreateTaskElement(name).Import(Linker);
        }
        public override ProjectUsingTaskElement CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName, string runtime, string architecture)
        {
            return (ProjectUsingTaskElement)Proxy.CreateUsingTaskElement(taskName, assemblyFile, assemblyName, runtime, architecture).Import(Linker);
        }
        public override UsingTaskParameterGroupElement CreateUsingTaskParameterGroupElement()
        {
            return (UsingTaskParameterGroupElement)Proxy.CreateUsingTaskParameterGroupElement().Import(Linker);
        }
        public override ProjectUsingTaskParameterElement CreateUsingTaskParameterElement(string name, string output, string required, string parameterType)
        {
            return (ProjectUsingTaskParameterElement)Proxy.CreateUsingTaskParameterElement(name, output, required, parameterType).Import(Linker);
        }
        public override ProjectUsingTaskBodyElement CreateUsingTaskBodyElement(string evaluate, string body)
        {
            return (ProjectUsingTaskBodyElement)Proxy.CreateUsingTaskBodyElement(evaluate, body).Import(Linker);
        }
        public override ProjectWhenElement CreateWhenElement(string condition)
        {
            return (ProjectWhenElement)Proxy.CreateWhenElement(condition).Import(Linker);
        }
        public override ProjectSdkElement CreateProjectSdkElement(string sdkName, string sdkVersion)
        {
            return (ProjectSdkElement)Proxy.CreateProjectSdkElement(sdkName, sdkVersion).Import(Linker);
        }
        public override void Save(Encoding saveEncoding)
        {
            Proxy.Save(saveEncoding);
        }
        public override void Save(TextWriter writer)
        {
            Proxy.Save(writer);
        }
        public override void ReloadFrom(string path, bool throwIfUnsavedChanges, bool preserveFormatting)
        {
            Proxy.ReloadFrom(path, throwIfUnsavedChanges, preserveFormatting);
        }
        public override void ReloadFrom(XmlReader reader, bool throwIfUnsavedChanges, bool preserveFormatting)
        {
            Proxy.ReloadFrom(reader, throwIfUnsavedChanges, preserveFormatting);
        }
        public override void MarkDirty(string reason, string param)
        {
            Proxy.MarkDirty(reason, param);
        }
    }
}
