// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using Microsoft.Build.Shared;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectUsingTaskElement represents the Import element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("TaskName={TaskName} AssemblyName={AssemblyName} AssemblyFile={AssemblyFile} Condition={Condition} Runtime={Runtime} Architecture={Architecture}")]
    public class ProjectUsingTaskElement : ProjectElementContainer
    {
        /// <summary>
        /// Initialize a parented ProjectUsingTaskElement
        /// </summary>
        internal ProjectUsingTaskElement(XmlElementWithLocation xmlElement, ProjectRootElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        /// Initialize an unparented ProjectUsingTaskElement
        /// </summary>
        private ProjectUsingTaskElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Gets the value of the AssemblyFile attribute.
        /// Returns empty string if it is not present.
        /// </summary>
        public string AssemblyFile
        {
            get => FileUtilities.FixFilePath(
                ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.assemblyFile));

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.assemblyName);
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(AssemblyName), "OM_EitherAttributeButNotBoth", XmlElement.Name, XMakeAttributes.assemblyFile, XMakeAttributes.assemblyName);
                value = FileUtilities.FixFilePath(value);
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.assemblyFile, value);
                MarkDirty("Set usingtask AssemblyFile {0}", value);
            }
        }

        /// <summary>
        /// Gets and sets the value of the AssemblyName attribute.
        /// Returns empty string if it is not present.
        /// </summary>
        public string AssemblyName
        {
            get => ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.assemblyName);

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.assemblyName);
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(AssemblyFile), "OM_EitherAttributeButNotBoth", XMakeElements.usingTask, XMakeAttributes.assemblyFile, XMakeAttributes.assemblyName);
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.assemblyName, value);
                MarkDirty("Set usingtask AssemblyName {0}", value);
            }
        }

        /// <summary>
        /// Gets and sets the value of the TaskName attribute.
        /// </summary>
        public string TaskName
        {
            get => ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.taskName);

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.taskName);
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.taskName, value);
                MarkDirty("Set usingtask TaskName {0}", value);
            }
        }

        /// <summary>
        /// Gets and sets the value of the TaskFactory attribute.
        /// </summary>
        public string TaskFactory
        {
            get => ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.taskFactory);

            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.taskFactory, value);
                MarkDirty("Set usingtask TaskFactory {0}", value);
            }
        }

        /// <summary>
        /// Gets and sets the value of the Runtime attribute.
        /// </summary>
        public string Runtime
        {
            get => ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.runtime);

            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.runtime, value);
                MarkDirty("Set usingtask Runtime {0}", value);
            }
        }

        /// <summary>
        /// Gets and sets the value of the Architecture attribute.
        /// </summary>
        public string Architecture
        {
            get => ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.architecture);

            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.architecture, value);
                MarkDirty("Set usingtask Architecture {0}", value);
            }
        }

        /// <summary>
        /// Get any contained TaskElement.
        /// </summary>
        public ProjectUsingTaskBodyElement TaskBody
        {
            get
            {
                ProjectUsingTaskBodyElement body = LastChild as ProjectUsingTaskBodyElement;
                return body;
            }
        }

        /// <summary>
        /// Get any contained ParameterGroup.
        /// </summary>
        public UsingTaskParameterGroupElement ParameterGroup
        {
            get
            {
                UsingTaskParameterGroupElement parameterGroup = FirstChild as UsingTaskParameterGroupElement;
                return parameterGroup;
            }
        }

        /// <summary>
        /// Location of the task name attribute
        /// </summary>
        public ElementLocation TaskNameLocation => XmlElement.GetAttributeLocation(XMakeAttributes.taskName);

        /// <summary>
        /// Location of the assembly file attribute, if any
        /// </summary>
        public ElementLocation AssemblyFileLocation => XmlElement.GetAttributeLocation(XMakeAttributes.assemblyFile);

        /// <summary>
        /// Location of the assembly name attribute, if any
        /// </summary>
        public ElementLocation AssemblyNameLocation => XmlElement.GetAttributeLocation(XMakeAttributes.assemblyName);

        /// <summary>
        /// Location of the Runtime attribute, if any
        /// </summary>
        public ElementLocation RuntimeLocation => XmlElement.GetAttributeLocation(XMakeAttributes.runtime);

        /// <summary>
        /// Location of the Architecture attribute, if any
        /// </summary>
        public ElementLocation ArchitectureLocation => XmlElement.GetAttributeLocation(XMakeAttributes.architecture);

        /// <summary>
        /// Location of the TaskFactory attribute, if any
        /// </summary>
        public ElementLocation TaskFactoryLocation => XmlElement.GetAttributeLocation(XMakeAttributes.taskFactory);

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        ///     Adds a new ParameterGroup to the using task to the end of the using task element
        /// </summary>
        public UsingTaskParameterGroupElement AddParameterGroup()
        {
            UsingTaskParameterGroupElement newParameterGroup = ContainingProject.CreateUsingTaskParameterGroupElement();
            PrependChild(newParameterGroup);
            return newParameterGroup;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        ///     Adds a new TaskBody to the using task to the end of the using task element
        /// </summary>
        public ProjectUsingTaskBodyElement AddUsingTaskBody(string evaluate, string taskBody)
        {
            ProjectUsingTaskBodyElement newTaskBody = ContainingProject.CreateUsingTaskBodyElement(evaluate, taskBody);
            AppendChild(newTaskBody);
            return newTaskBody;
        }

        /// <summary>
        /// Creates an unparented ProjectUsingTaskElement, wrapping an unparented XmlElement.
        /// Validates the parameters.
        /// Exactly one of assembly file and assembly name must have a value.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectUsingTaskElement CreateDisconnected(string taskName, string assemblyFile, string assemblyName, string runtime, string architecture, ProjectRootElement containingProject)
        {
            ErrorUtilities.VerifyThrowArgument(
                (String.IsNullOrEmpty(assemblyFile) ^ String.IsNullOrEmpty(assemblyName)),
                "OM_EitherAttributeButNotBoth",
                XMakeElements.usingTask,
                XMakeAttributes.assemblyFile,
                XMakeAttributes.assemblyName);

            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.usingTask);

            var usingTask = new ProjectUsingTaskElement(element, containingProject)
            {
                TaskName = taskName,
                Runtime = runtime,
                Architecture = architecture
            };

            if (!String.IsNullOrEmpty(assemblyFile))
            {
                usingTask.AssemblyFile = FileUtilities.FixFilePath(assemblyFile);
            }
            else
            {
                usingTask.AssemblyName = assemblyName;
            }

            return usingTask;
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateUsingTaskElement(TaskName, AssemblyFile, AssemblyName, Runtime, Architecture);
        }
    }
}
