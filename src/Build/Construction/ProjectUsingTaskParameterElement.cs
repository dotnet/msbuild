// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// UsingTaskParameterElement class represents the Parameter element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("Name={Name} ParameterType={ParameterType} Output={Output} Required={Required}")]
    public class ProjectUsingTaskParameterElement : ProjectElement
    {
        ProjectUsingTaskParameterElementLink TaskParameterLink => (ProjectUsingTaskParameterElementLink)Link;
        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectUsingTaskParameterElement(ProjectUsingTaskParameterElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented UsingTaskParameterElement instance
        /// </summary>
        internal ProjectUsingTaskParameterElement(XmlElementWithLocation xmlElement, UsingTaskParameterGroupElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
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
            get => null;
            set => ErrorUtilities.ThrowInvalidOperation("OM_CannotGetSetCondition");
        }

        /// <summary>
        /// Gets and sets the name of the parameter's name
        /// </summary>
        public string Name
        {
            get => ElementName;

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, nameof(Name));

                if (Link != null) { TaskParameterLink.Name = value; return; }
                // TODO: There seems to be a bug here
                // the Name returns the element name (consistent with XML view aka:)
                // <fooParam .../> => this.Name = fooParan
                // while setter will "set" the "Name" attribute, instead of renaming the XML element aka
                // <fooParam Name="newName".../> instead of (so this.Name still is "fooParam"
                // <newName .../>
                // i have changed the "link" to support either way, should check whether it has to be fix on real object as well.

                XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, value, XmlElement.NamespaceURI);
                ReplaceElement(newElement);

                 // SetOrRemoveAttribute(XMakeAttributes.name, value, "Set usingtaskparameter {0}", value);
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
                string typeAttribute = GetAttributeValue(XMakeAttributes.parameterType);
                return String.IsNullOrEmpty(typeAttribute) ? typeof(String).FullName : typeAttribute;
            }

            set
            {
                // If null or empty is passed in remove the attribute from the element
                SetOrRemoveAttribute(XMakeAttributes.parameterType, value, "Set usingtaskparameter ParameterType {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the output attribute
        /// </summary>
        public string Output
        {
            get
            {
                string outputAttribute = GetAttributeValue(XMakeAttributes.output);
                return String.IsNullOrEmpty(outputAttribute) ? bool.FalseString : outputAttribute;
            }

            set
            {
                SetOrRemoveAttribute(XMakeAttributes.output, value, "Set usingtaskparameter Output {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the required attribute
        /// </summary>
        public string Required
        {
            get
            {
                string requiredAttribute = GetAttributeValue(XMakeAttributes.required);
                return String.IsNullOrEmpty(requiredAttribute) ? bool.FalseString : requiredAttribute;
            }

            set
            {
                SetOrRemoveAttribute(XMakeAttributes.required, value, "Set usingtaskparameter Required {0}", value);
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
        public ElementLocation ParameterTypeLocation => GetAttributeLocation(XMakeAttributes.parameterType) ?? Location;

        /// <summary>
        /// Location of the Output attribute.
        /// If there is no such attribute, returns the location of the element,
        /// in lieu of the default value it uses for the attribute.
        /// </summary>
        public ElementLocation OutputLocation => GetAttributeLocation(XMakeAttributes.output) ?? Location;

        /// <summary>
        /// Location of the Required attribute.
        /// If there is no such attribute, returns the location of the element,
        /// in lieu of the default value it uses for the attribute.
        /// </summary>
        public ElementLocation RequiredLocation => GetAttributeLocation(XMakeAttributes.required) ?? Location;

        /// <summary>
        /// Creates an unparented UsingTaskParameterElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent.
        /// </summary>
        internal static ProjectUsingTaskParameterElement CreateDisconnected(string parameterName, string output, string required, string parameterType, ProjectRootElement containingProject)
        {
            XmlUtilities.VerifyThrowArgumentValidElementName(parameterName);
            XmlElementWithLocation element = containingProject.CreateElement(parameterName);
            var parameter =
                new ProjectUsingTaskParameterElement(element, containingProject)
                {
                    Output = output,
                    Required = required,
                    ParameterType = parameterType
                };

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
            return owner.CreateUsingTaskParameterElement(Name, Output, Required, ParameterType);
        }
    }
}
