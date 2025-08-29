﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Xml;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectOutputElement represents the Output element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("TaskParameter={TaskParameter} ItemType={ItemType} PropertyName={PropertyName} Condition={Condition}")]
    public class ProjectOutputElement : ProjectElement
    {
        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectOutputElement(ProjectOutputElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented ProjectOutputElement
        /// </summary>
        internal ProjectOutputElement(XmlElement xmlElement, ProjectTaskElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        /// Initialize an unparented ProjectOutputElement
        /// </summary>
        private ProjectOutputElement(XmlElement xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Gets or sets the TaskParameter value. 
        /// Returns empty string if it is not present.
        /// </summary>
        public string TaskParameter
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.taskParameter);
            }

            [DebuggerStepThrough]
            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, nameof(value));
                SetOrRemoveAttribute(XMakeAttributes.taskParameter, value, "Set Output TaskParameter {0}", value);
            }
        }

        /// <summary>
        /// Whether this represents an output item (as opposed to an output property)
        /// </summary>
        public bool IsOutputItem => ItemType.Length > 0;

        /// <summary>
        /// Whether this represents an output property (as opposed to an output item)
        /// </summary>
        public bool IsOutputProperty => PropertyName.Length > 0;

        /// <summary>
        /// Gets or sets the ItemType value. 
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        /// <remarks>
        /// Unfortunately the attribute name chosen in Whidbey was "ItemName" not ItemType.
        /// </remarks>
        public string ItemType
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.itemName);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(PropertyName), "OM_EitherAttributeButNotBoth", ElementName, XMakeAttributes.itemName, XMakeAttributes.propertyName);
                SetOrRemoveAttribute(XMakeAttributes.itemName, value, "Set Output ItemType {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the PropertyName value. 
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string PropertyName
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.propertyName);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(ItemType), "OM_EitherAttributeButNotBoth", ElementName, XMakeAttributes.itemName, XMakeAttributes.propertyName);
                SetOrRemoveAttribute(XMakeAttributes.propertyName, value, "Set Output PropertyName {0}", value);
            }
        }

        /// <summary>
        /// Location of the task parameter attribute
        /// </summary>
        public ElementLocation TaskParameterLocation => GetAttributeLocation(XMakeAttributes.taskParameter);

        /// <summary>
        /// Location of the property name attribute, if any
        /// </summary>
        public ElementLocation PropertyNameLocation => GetAttributeLocation(XMakeAttributes.propertyName);

        /// <summary>
        /// Location of the item type attribute, if any
        /// </summary>
        public ElementLocation ItemTypeLocation => GetAttributeLocation(XMakeAttributes.itemName);

        /// <summary>
        /// Creates an unparented ProjectOutputElement, wrapping an unparented XmlElement.
        /// Validates the parameters.
        /// Exactly one of item name and property name must have a value.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectOutputElement CreateDisconnected(string taskParameter, string itemType, string propertyName, ProjectRootElement containingProject)
        {
            ErrorUtilities.VerifyThrowArgument(
                String.IsNullOrEmpty(itemType) ^ String.IsNullOrEmpty(propertyName),
                "OM_EitherAttributeButNotBoth",
                XMakeElements.output,
                XMakeAttributes.propertyName,
                XMakeAttributes.itemName);

            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.output);

            var output = new ProjectOutputElement(element, containingProject) { TaskParameter = taskParameter };

            if (!String.IsNullOrEmpty(itemType))
            {
                output.ItemType = itemType;
            }
            else
            {
                output.PropertyName = propertyName;
            }

            return output;
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectTaskElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateOutputElement(TaskParameter, ItemType, PropertyName);
        }
    }
}
