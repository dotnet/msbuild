﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Internal;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectPropertyElement class represents the Property element in the MSBuild project.
    /// </summary>
    /// <remarks>
    /// We do not need to use or set the PropertyType enumeration in the CM. 
    /// The CM does not know about Environment or Global properties, and does not create Output properties.
    /// We can just verify that we haven't read a PropertyType.Reserved property ourselves.
    /// So the CM only represents Normal properties.
    /// </remarks>
    [DebuggerDisplay("{Name} Value={Value} Condition={Condition}")]
    public class ProjectPropertyElement : ProjectElement
    {
        internal ProjectPropertyElementLink PropertyLink => (ProjectPropertyElementLink)Link;

        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectPropertyElement(ProjectPropertyElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented ProjectPropertyElement
        /// </summary>
        internal ProjectPropertyElement(XmlElementWithLocation xmlElement, ProjectPropertyGroupElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        /// Initialize an unparented ProjectPropertyElement
        /// </summary>
        private ProjectPropertyElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Gets or sets the property name.
        /// </summary>
        public string Name
        {
            get => ElementName;
            set => ChangeName(value);
        }

        /// <summary>
        /// Gets or sets the unevaluated value. 
        /// Returns empty string if it is not present.
        /// </summary>
        public string Value
        {
            get => Link != null ? PropertyLink.Value : Internal.Utilities.GetXmlNodeInnerContents(XmlElement);

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(Value));
                if (Link != null)
                {
                    PropertyLink.Value = value;
                    return;
                }

                // Visual Studio has a tendency to set properties to their existing value.
                if (Value != value)
                {
                    Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
                    MarkDirty("Set property Value {0}", value);
                }
            }
        }

        /// <summary>
        /// Creates an unparented ProjectPropertyElement, wrapping an unparented XmlElement.
        /// Validates name.
        /// Caller should then ensure the element is added to the XmlDocument in the appropriate location.
        /// </summary>
        internal static ProjectPropertyElement CreateDisconnected(string name, ProjectRootElement containingProject)
        {
            XmlUtilities.VerifyThrowArgumentValidElementName(name);

            ErrorUtilities.VerifyThrowInvalidOperation(!XMakeElements.ReservedItemNames.Contains(name) && !ReservedPropertyNames.IsReservedProperty(name), "OM_CannotCreateReservedProperty", name);

            XmlElementWithLocation element = containingProject.CreateElement(name);

            return new ProjectPropertyElement(element, containingProject);
        }

        /// <summary>
        /// Changes the name.
        /// </summary>
        /// <remarks>
        /// The implementation has to actually replace the element to do this.
        /// </remarks>
        internal void ChangeName(string newName)
        {
            ErrorUtilities.VerifyThrowArgumentLength(newName, nameof(newName));
            XmlUtilities.VerifyThrowArgumentValidElementName(newName);
            ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newName), "CannotModifyReservedProperty", newName);
            if (Link != null)
            {
                PropertyLink.ChangeName(newName);
                return;
            }

            // Because the element was created from our special XmlDocument, we know it's
            // an XmlElementWithLocation.
            XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, newName, XmlElement.NamespaceURI);

            ReplaceElement(newElement);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectPropertyGroupElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreatePropertyElement(Name);
        }
    }
}
