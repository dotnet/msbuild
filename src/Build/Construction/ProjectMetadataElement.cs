﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectMetadataElement class represents a Metadata element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("{Name} Value={Value} Condition={Condition}")]
    public class ProjectMetadataElement : ProjectElement
    {
        internal ProjectMetadataElementLink MetadataLink => (ProjectMetadataElementLink)Link;

        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectMetadataElement(ProjectMetadataElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented ProjectMetadataElement
        /// </summary>
        internal ProjectMetadataElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement project)
            : base(xmlElement, parent, project)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        /// Initialize an unparented ProjectMetadataElement
        /// </summary>
        private ProjectMetadataElement(XmlElementWithLocation xmlElement, ProjectRootElement project)
            : base(xmlElement, null, project)
        {
        }

        /// <summary>
        /// Gets or sets the metadata's type.
        /// </summary>
        public string Name
        {
            get => ElementName;
            set => ChangeName(value);
        }

        // Add a new property with the same name here because this attribute should be public for ProjectMetadataElement,
        //  but internal for ProjectElement, because we don't want it to be settable for arbitrary elements.
        /// <summary>
        /// Gets or sets whether this piece of metadata is expressed as an attribute.
        /// </summary>
        /// <remarks>
        /// If true, then the metadata will be expressed as an attribute instead of a child element, for example
        /// &lt;Reference Include="Libary.dll" HintPath="..\lib\Library.dll" Private="True" /&gt;
        /// </remarks>
        public new bool ExpressedAsAttribute
        {
            get => base.ExpressedAsAttribute;
            set
            {
                if (value)
                {
                    ValidateValidMetadataAsAttributeName(Name, Parent?.ElementName ?? "null", Parent?.Location);
                }
                base.ExpressedAsAttribute = value;
            }
        }

        /// <summary>
        /// Gets or sets the unevaluated value. 
        /// Returns empty string if it is not present.
        /// </summary>
        public string Value
        {
            get => Link != null ? MetadataLink.Value : Internal.Utilities.GetXmlNodeInnerContents(XmlElement);

            set
            {
                if (Link != null)
                {
                    MetadataLink.Value = value;
                    return;
                }

                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(Value));
                Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
                Parent?.UpdateElementValue(this);
                MarkDirty("Set metadata Value {0}", value);
            }
        }

        /// <summary>
        /// Creates an unparented ProjectMetadataElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent.
        /// </summary>
        internal static ProjectMetadataElement CreateDisconnected(string name, ProjectRootElement containingProject, ElementLocation location = null)
        {
            XmlUtilities.VerifyThrowArgumentValidElementName(name);
            ErrorUtilities.VerifyThrowArgument(!FileUtilities.ItemSpecModifiers.IsItemSpecModifier(name), "ItemSpecModifierCannotBeCustomMetadata", name);
            ErrorUtilities.VerifyThrowInvalidOperation(!XMakeElements.ReservedItemNames.Contains(name), "CannotModifyReservedItemMetadata", name);

            XmlElementWithLocation element = containingProject.CreateElement(name, location);

            return new ProjectMetadataElement(element, containingProject);
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
            ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newName), "CannotModifyReservedItemMetadata", newName);

            if (Link != null)
            {
                MetadataLink.ChangeName(newName);
                return;
            }

            if (ExpressedAsAttribute)
            {
                ValidateValidMetadataAsAttributeName(newName, Parent.ElementName, Parent.Location);
            }

            // Because the element was created from our special XmlDocument, we know it's
            // an XmlElementWithLocation.
            XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, newName, XmlElement.NamespaceURI);

            ReplaceElement(newElement);
        }

        internal static void ValidateValidMetadataAsAttributeName(string name, string parentName, IElementLocation parentLocation)
        {
            if (!AttributeNameIsValidMetadataName(name))
            {
                ProjectErrorUtilities.ThrowInvalidProject(parentLocation, "InvalidMetadataAsAttribute", name, parentName);
            }
        }

        internal static bool AttributeNameIsValidMetadataName(string name)
        {
            ProjectParser.CheckMetadataAsAttributeName(name, out bool isReservedAttributeName, out bool isValidMetadataNameInAttribute);

            return !isReservedAttributeName && isValidMetadataNameInAttribute;
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectItemElement || parent is ProjectItemDefinitionElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateMetadataElement(Name);
        }
    }
}
