// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.Construction;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectRootElement"/>
    /// </summary>
    public abstract class ProjectRootElementLink : ProjectElementContainerLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.Version"/>.
        /// </summary>
        public abstract int Version { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.HasUnsavedChanges"/>.
        /// </summary>
        public abstract bool HasUnsavedChanges { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.TimeLastChanged"/>.
        /// </summary>
        public abstract DateTime TimeLastChanged { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.LastWriteTimeWhenRead "/>.
        /// </summary>
        public abstract DateTime LastWriteTimeWhenRead { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.DirectoryPath"/>.
        /// </summary>
        public abstract string DirectoryPath { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.FullPath"/>.
        /// </summary>
        public abstract string FullPath { get; set; }

        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.ProjectFileLocation"/>.
        /// </summary>
        public abstract ElementLocation ProjectFileLocation { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.Encoding"/>.
        /// </summary>
        public abstract Encoding Encoding { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.RawXml"/>.
        /// </summary>
        public abstract string RawXml { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectRootElement.PreserveFormatting"/>.
        /// </summary>
        public abstract bool PreserveFormatting { get; }

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateChooseElement"/>.
        /// </summary>
        public abstract ProjectChooseElement CreateChooseElement();

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateImportElement"/>.
        /// </summary>
        public abstract ProjectImportElement CreateImportElement(string project);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateItemElement(string)"/>.
        /// </summary>
        public abstract ProjectItemElement CreateItemElement(string itemType);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateItemElement(string, string)"/>.
        /// </summary>
        public abstract ProjectItemElement CreateItemElement(string itemType, string include);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateItemDefinitionElement"/>.
        /// </summary>
        public abstract ProjectItemDefinitionElement CreateItemDefinitionElement(string itemType);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateItemDefinitionGroupElement"/>.
        /// </summary>
        public abstract ProjectItemDefinitionGroupElement CreateItemDefinitionGroupElement();

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateItemGroupElement"/>.
        /// </summary>
        public abstract ProjectItemGroupElement CreateItemGroupElement();

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateImportGroupElement"/>.
        /// </summary>
        public abstract ProjectImportGroupElement CreateImportGroupElement();

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateMetadataElement(string)"/>.
        /// </summary>
        public abstract ProjectMetadataElement CreateMetadataElement(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateMetadataElement(string, string)"/>.
        /// </summary>
        public abstract ProjectMetadataElement CreateMetadataElement(string name, string unevaluatedValue);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateOnErrorElement"/>.
        /// </summary>
        public abstract ProjectOnErrorElement CreateOnErrorElement(string executeTargets);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateOtherwiseElement"/>.
        /// </summary>
        public abstract ProjectOtherwiseElement CreateOtherwiseElement();

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateOutputElement"/>.
        /// </summary>
        public abstract ProjectOutputElement CreateOutputElement(string taskParameter, string itemType, string propertyName);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateProjectExtensionsElement"/>.
        /// </summary>
        public abstract ProjectExtensionsElement CreateProjectExtensionsElement();

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreatePropertyGroupElement"/>.
        /// </summary>
        public abstract ProjectPropertyGroupElement CreatePropertyGroupElement();

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreatePropertyElement"/>.
        /// </summary>
        public abstract ProjectPropertyElement CreatePropertyElement(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateTargetElement"/>.
        /// </summary>
        public abstract ProjectTargetElement CreateTargetElement(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateTaskElement"/>.
        /// </summary>
        public abstract ProjectTaskElement CreateTaskElement(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateUsingTaskElement(string, string, string, string, string)"/>.
        /// </summary>
        public abstract ProjectUsingTaskElement CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName, string runtime, string architecture);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateUsingTaskParameterGroupElement"/>.
        /// </summary>
        public abstract UsingTaskParameterGroupElement CreateUsingTaskParameterGroupElement();

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateUsingTaskParameterGroupElement"/>.
        /// </summary>
        public abstract ProjectUsingTaskParameterElement CreateUsingTaskParameterElement(string name, string output, string required, string parameterType);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateUsingTaskBodyElement"/>.
        /// </summary>
        public abstract ProjectUsingTaskBodyElement CreateUsingTaskBodyElement(string evaluate, string body);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateWhenElement"/>.
        /// </summary>
        public abstract ProjectWhenElement CreateWhenElement(string condition);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.CreateProjectSdkElement"/>.
        /// </summary>
        public abstract ProjectSdkElement CreateProjectSdkElement(string sdkName, string sdkVersion);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.Save(Encoding)"/>.
        /// </summary>
        public abstract void Save(Encoding saveEncoding);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.Save(TextWriter)"/>.
        /// </summary>
        public abstract void Save(TextWriter writer);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.ReloadFrom(string, bool, bool?)"/>.
        /// </summary>
        public abstract void ReloadFrom(string path, bool throwIfUnsavedChanges, bool preserveFormatting);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.ReloadFrom(XmlReader, bool, bool?)"/>.
        /// </summary>
        public abstract void ReloadFrom(XmlReader reader, bool throwIfUnsavedChanges, bool preserveFormatting);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectRootElement.MarkDirty"/>.
        /// </summary>
        public abstract void MarkDirty(string reason, string param);
    }
}
