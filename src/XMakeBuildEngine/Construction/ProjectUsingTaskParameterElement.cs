// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectUsingTaskParameterElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using Microsoft.Build.Shared;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// UsingTaskParameterElement class represents the Parameter element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("Name={Name} ParameterType={ParameterType} Output={Output} Required={Required}")]
    public class ProjectUsingTaskParameterElement : ProjectElement
    {
        /// <summary>
        /// Initialize a parented UsingTaskParameterElement instance
        /// </summary>
        internal ProjectUsingTaskParameterElement(XmlElementWithLocation xmlElement, UsingTaskParameterGroupElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize an unparented UsingTaskParameterElement instance
        /// </summary>
        private ProjectUsingTaskParameterElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Condition should never be set, but the getter returns null instead of throwing 
        /// because a nonexistent condition is implicitly true
        /// </summary>
        public override string Condition
        {
            get
            {
                return null;
            }

            set
            {
                ErrorUtilities.ThrowInvalidOperation("OM_CannotGetSetCondition");
            }
        }

        /// <summary>
        /// Gets and sets the name of the parameter's name
        /// </summary>
        public string Name
        {
            [DebuggerStepThrough]
            get
            {
                return XmlElement.Name;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, "Name");
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.name, value);
                MarkDirty("Set usingtaskparameter {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the Type attribute returns "System.String" if not set.
        /// If null or empty is set the attribute will be removed from the element.
        /// </summary>
        public string ParameterType
        {
            get
            {
                string typeAttribute = ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.parameterType);

                if (String.IsNullOrEmpty(typeAttribute))
                {
                    return typeof(String).FullName;
                }

                return typeAttribute;
            }

            set
            {
                // If null or empty is passed in remove the attribute from the element
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.parameterType, value);
                MarkDirty("Set usingtaskparameter ParameterType {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the output attribute
        /// </summary>
        public string Output
        {
            get
            {
                string outputAttribute = ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.output);
                if (String.IsNullOrEmpty(outputAttribute))
                {
                    return bool.FalseString;
                }

                return outputAttribute;
            }

            set
            {
                XmlAttribute typeAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.output, value);
                MarkDirty("Set usingtaskparameter Output {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the required attribute
        /// </summary>
        public string Required
        {
            get
            {
                string requiredAttribute = ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.required);
                if (String.IsNullOrEmpty(requiredAttribute))
                {
                    return bool.FalseString;
                }

                return requiredAttribute;
            }

            set
            {
                XmlAttribute typeAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.required, value);
                MarkDirty("Set usingtaskparameter Required {0}", value);
            }
        }

        /// <summary>
        /// This does not allow conditions, so it should not be called.
        /// </summary>
        public override ElementLocation ConditionLocation
        {
            get
            {
                ErrorUtilities.ThrowInternalError("Should not evaluate this");
                return null;
            }
        }

        /// <summary>
        /// Location of the Type attribute.
        /// If there is no such attribute, returns the location of the element,
        /// in lieu of the default value it uses for the attribute.
        /// </summary>
        public ElementLocation ParameterTypeLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.parameterType) ?? Location; }
        }

        /// <summary>
        /// Location of the Output attribute.
        /// If there is no such attribute, returns the location of the element,
        /// in lieu of the default value it uses for the attribute.
        /// </summary>
        public ElementLocation OutputLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.output) ?? Location; }
        }

        /// <summary>
        /// Location of the Required attribute.
        /// If there is no such attribute, returns the location of the element,
        /// in lieu of the default value it uses for the attribute.
        /// </summary>
        public ElementLocation RequiredLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.required) ?? Location; }
        }

        /// <summary>
        /// Creates an unparented UsingTaskParameterElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent.
        /// </summary>
        internal static ProjectUsingTaskParameterElement CreateDisconnected(string parameterName, string output, string required, string parameterType, ProjectRootElement containingProject)
        {
            XmlUtilities.VerifyThrowArgumentValidElementName(parameterName);
            XmlElementWithLocation element = containingProject.CreateElement(parameterName);
            ProjectUsingTaskParameterElement parameter = new ProjectUsingTaskParameterElement(element, containingProject);
            parameter.Output = output;
            parameter.Required = required;
            parameter.ParameterType = parameterType;

            return parameter;
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is UsingTaskParameterGroupElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateUsingTaskParameterElement(this.Name, this.Output, this.Required, this.ParameterType);
        }
    }
}
