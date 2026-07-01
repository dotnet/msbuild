// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    /// <summary>
    /// A mock implementation of ProjectRootElementLink to be used to test ProjectInstance created from cache state does not access most state unless needed.
    /// </summary>
    internal sealed class FakeProjectRootElementLink : ProjectRootElementLink
    {
        public FakeProjectRootElementLink(string path)
        {
            FullPath = path;
        }

        public override int Version => throw new NotImplementedException();

        public override bool HasUnsavedChanges => throw new NotImplementedException();

        public override DateTime TimeLastChanged => throw new NotImplementedException();

        public override DateTime LastWriteTimeWhenRead => throw new NotImplementedException();

        public override string DirectoryPath => throw new NotImplementedException();

        public override string FullPath { get; set; }

        public override ElementLocation ProjectFileLocation => throw new NotImplementedException();

        public override Encoding Encoding => throw new NotImplementedException();

        public override string RawXml => throw new NotImplementedException();

        public override bool PreserveFormatting => throw new NotImplementedException();

        public override int Count => throw new NotImplementedException();

        public override ProjectElement FirstChild => throw new NotImplementedException();

        public override ProjectElement LastChild => throw new NotImplementedException();

        public override ProjectElementContainer Parent => throw new NotImplementedException();

        public override ProjectRootElement ContainingProject => throw new NotImplementedException();

        public override string ElementName => throw new NotImplementedException();

        public override string OuterElement => throw new NotImplementedException();

        public override bool ExpressedAsAttribute { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override ProjectElement PreviousSibling => throw new NotImplementedException();

        public override ProjectElement NextSibling => throw new NotImplementedException();

        public override ElementLocation Location => throw new NotImplementedException();

        public override IReadOnlyCollection<XmlAttributeLink> Attributes => throw new NotImplementedException();

        public override string PureText => throw new NotImplementedException();

        public override void AddInitialChild(ProjectElement child) => throw new NotImplementedException();

        public override void CopyFrom(ProjectElement element) => throw new NotImplementedException();

        public override ProjectChooseElement CreateChooseElement() => throw new NotImplementedException();

        public override ProjectImportElement CreateImportElement(string project) => throw new NotImplementedException();

        public override ProjectImportGroupElement CreateImportGroupElement() => throw new NotImplementedException();

        public override ProjectItemDefinitionElement CreateItemDefinitionElement(string itemType) => throw new NotImplementedException();

        public override ProjectItemDefinitionGroupElement CreateItemDefinitionGroupElement() => throw new NotImplementedException();

        public override ProjectItemElement CreateItemElement(string itemType) => throw new NotImplementedException();

        public override ProjectItemElement CreateItemElement(string itemType, string include) => throw new NotImplementedException();

        public override ProjectItemGroupElement CreateItemGroupElement() => throw new NotImplementedException();

        public override ProjectMetadataElement CreateMetadataElement(string name) => throw new NotImplementedException();

        public override ProjectMetadataElement CreateMetadataElement(string name, string unevaluatedValue) => throw new NotImplementedException();

        public override ProjectElement CreateNewInstance(ProjectRootElement owner) => throw new NotImplementedException();

        public override ProjectOnErrorElement CreateOnErrorElement(string executeTargets) => throw new NotImplementedException();

        public override ProjectOtherwiseElement CreateOtherwiseElement() => throw new NotImplementedException();

        public override ProjectOutputElement CreateOutputElement(string taskParameter, string itemType, string propertyName) => throw new NotImplementedException();

        public override ProjectExtensionsElement CreateProjectExtensionsElement() => throw new NotImplementedException();

        public override ProjectSdkElement CreateProjectSdkElement(string sdkName, string sdkVersion) => throw new NotImplementedException();

        public override ProjectPropertyElement CreatePropertyElement(string name) => throw new NotImplementedException();

        public override ProjectPropertyGroupElement CreatePropertyGroupElement() => throw new NotImplementedException();

        public override ProjectTargetElement CreateTargetElement(string name) => throw new NotImplementedException();

        public override ProjectTaskElement CreateTaskElement(string name) => throw new NotImplementedException();

        public override ProjectUsingTaskBodyElement CreateUsingTaskBodyElement(string evaluate, string body) => throw new NotImplementedException();

        public override ProjectUsingTaskElement CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName, string runtime, string architecture) => throw new NotImplementedException();

        public override ProjectUsingTaskParameterElement CreateUsingTaskParameterElement(string name, string output, string required, string parameterType) => throw new NotImplementedException();

        public override UsingTaskParameterGroupElement CreateUsingTaskParameterGroupElement() => throw new NotImplementedException();

        public override ProjectWhenElement CreateWhenElement(string condition) => throw new NotImplementedException();

        public override ProjectElementContainer DeepClone(ProjectRootElement factory, ProjectElementContainer parent) => throw new NotImplementedException();

        public override ElementLocation GetAttributeLocation(string attributeName) => throw new NotImplementedException();

        public override string GetAttributeValue(string attributeName, bool nullIfNotExists) => throw new NotImplementedException();

        public override void InsertAfterChild(ProjectElement child, ProjectElement reference) => throw new NotImplementedException();

        public override void InsertBeforeChild(ProjectElement child, ProjectElement reference) => throw new NotImplementedException();

        public override void MarkDirty(string reason, string param) => throw new NotImplementedException();

        public override void ReloadFrom(string path, bool throwIfUnsavedChanges, bool preserveFormatting) => throw new NotImplementedException();

        public override void ReloadFrom(XmlReader reader, bool throwIfUnsavedChanges, bool preserveFormatting) => throw new NotImplementedException();

        public override void RemoveChild(ProjectElement child) => throw new NotImplementedException();

        public override void Save(Encoding saveEncoding) => throw new NotImplementedException();

        public override void Save(TextWriter writer) => throw new NotImplementedException();

        public override void SetOrRemoveAttribute(string name, string value, bool clearAttributeCache, string reason, string param) => throw new NotImplementedException();
    }
}
