// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectPropertyElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using Microsoft.Build.Shared;
using Microsoft.Build.Internal;

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
        /// <summary>
        /// Initialize a parented ProjectPropertyElement
        /// </summary>
        internal ProjectPropertyElement(XmlElementWithLocation xmlElement, ProjectPropertyGroupElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
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
            get { return XmlElement.Name; }
            set { ChangeName(value); }
        }

        /// <summary>
        /// Gets or sets the unevaluated value. 
        /// Returns empty string if it is not present.
        /// </summary>
        public string Value
        {
            get
            {
                return Microsoft.Build.Internal.Utilities.GetXmlNodeInnerContents(XmlElement);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "Value");

                // Visual Studio has a tendency to set properties to their existing value.
                if (Value != value)
                {
                    Microsoft.Build.Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
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
            ErrorUtilities.VerifyThrowArgumentLength(newName, "newName");
            XmlUtilities.VerifyThrowArgumentValidElementName(newName);
            ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newName), "CannotModifyReservedProperty", newName);

            // Because the element was created from our special XmlDocument, we know it's
            // an XmlElementWithLocation.
            XmlElementWithLocation newElement = (XmlElementWithLocation)XmlUtilities.RenameXmlElement(XmlElement, newName, XmlElement.NamespaceURI);

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
            return owner.CreatePropertyElement(this.Name);
        }
    }
}
