﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Execution;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectTargetElement represents the Target element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("Name={Name} #Children={Count} Condition={Condition}")]
    public class ProjectTargetElement : ProjectElementContainer
    {
        internal ProjectTargetElementLink TargetLink => (ProjectTargetElementLink)Link;

        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectTargetElement(ProjectTargetElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Target name cached for performance
        /// </summary>
        private string _name;

        /// <summary>
        /// Initialize a parented ProjectTargetElement
        /// </summary>
        internal ProjectTargetElement(XmlElementWithLocation xmlElement, ProjectRootElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        /// Initialize an unparented ProjectTargetElement
        /// </summary>
        private ProjectTargetElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        #region ChildEnumerators
        /// <summary>
        /// Get an enumerator over any child item groups
        /// </summary>
        public ICollection<ProjectItemGroupElement> ItemGroups => GetChildrenOfType<ProjectItemGroupElement>();

        /// <summary>
        /// Get an enumerator over any child property groups
        /// </summary>
        public ICollection<ProjectPropertyGroupElement> PropertyGroups => GetChildrenOfType<ProjectPropertyGroupElement>();

        /// <summary>
        /// Get an enumerator over any child tasks
        /// </summary>
        public ICollection<ProjectTaskElement> Tasks => GetChildrenOfType<ProjectTaskElement>();

        /// <summary>
        /// Get an enumerator over any child onerrors
        /// </summary>
        public ICollection<ProjectOnErrorElement> OnErrors => GetChildrenOfType<ProjectOnErrorElement>();

        #endregion

        /// <summary>
        /// Gets and sets the name of the target element.
        /// </summary>
        public string Name
        {
            [DebuggerStepThrough]
            get
            {
                if (Link != null) { return TargetLink.Name; }

                // No thread-safety lock required here because many reader threads would set the same value to the field.
                if (_name != null)
                {
                    return _name;
                }

                string unescapedValue = EscapingUtilities.UnescapeAll(GetAttributeValue(XMakeAttributes.name));
                return _name = unescapedValue;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, nameof(value));
                if (Link != null)
                {
                    TargetLink.Name = value;
                    return;
                }

                string unescapedValue = EscapingUtilities.UnescapeAll(value);

                int indexOfSpecialCharacter = unescapedValue.IndexOfAny(XMakeElements.InvalidTargetNameCharacters);
                if (indexOfSpecialCharacter >= 0)
                {
                    ErrorUtilities.ThrowArgument("OM_NameInvalid", unescapedValue, unescapedValue[indexOfSpecialCharacter]);
                }

                SetOrRemoveAttribute(XMakeAttributes.name, unescapedValue, "Set target Name {0}", value);
                _name = unescapedValue;
            }
        }

        /// <summary>
        /// Gets or sets the Inputs value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string Inputs
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.inputs);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, XMakeAttributes.inputs);
                SetOrRemoveAttribute(XMakeAttributes.inputs, value, "Set target Inputs {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the Outputs value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string Outputs
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.outputs);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, XMakeAttributes.outputs);
                SetOrRemoveAttribute(XMakeAttributes.outputs, value, "Set target Outputs {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the TrimDuplicateOutputs value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string KeepDuplicateOutputs
        {
            [DebuggerStepThrough]
            get
            {
                string value = GetAttributeValue(XMakeAttributes.keepDuplicateOutputs);
                if (String.IsNullOrEmpty(value) && !BuildParameters.KeepDuplicateOutputs)
                {
                    // In 4.0, by default we do NOT keep duplicate outputs unless they user has either set the attribute
                    // explicitly or overridden it globally with MSBUILDKEEPDUPLICATEOUTPUTS set to a non-empty value.                    
                    value = "False";
                }

                return value;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, XMakeAttributes.keepDuplicateOutputs);
                SetOrRemoveAttribute(XMakeAttributes.keepDuplicateOutputs, value, "Set target KeepDuplicateOutputs {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the DependsOnTargets value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string DependsOnTargets
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.dependsOnTargets);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, XMakeAttributes.dependsOnTargets);
                SetOrRemoveAttribute(XMakeAttributes.dependsOnTargets, value, "Set target DependsOnTargets {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the BeforeTargets value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string BeforeTargets
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.beforeTargets);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, XMakeAttributes.beforeTargets);
                SetOrRemoveAttribute(XMakeAttributes.beforeTargets, value, "Set target BeforeTargets {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the AfterTargets value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string AfterTargets
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.afterTargets);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, XMakeAttributes.afterTargets);
                SetOrRemoveAttribute(XMakeAttributes.afterTargets, value, "Set target AfterTargets {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the Returns value.
        /// Returns null if the attribute is not present -- empty string is an allowable
        /// value for both getting and setting.
        /// Removes the attribute only if the value is set to null.
        /// </summary>
        public string Returns
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.returns, true /* If the element is not there, return null */);
            }

            set
            {
                if (Link != null)
                {
                    TargetLink.Returns = value;
                    return;
                }

                XmlAttributeWithLocation returnsAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(
                        XmlElement,
                        XMakeAttributes.returns,
                        value,
                        true); /* only remove the element if the value is null -- setting to empty string is OK */

                // if this target's Returns attribute is non-null, then there is at least one target in the 
                // parent project that has the returns attribute.  
                // NOTE: As things are currently, if a project is created that has targets with Returns, but then 
                // all of those targets are set to not have Returns anymore, the PRE will still claim that it 
                // contains targets with the Returns attribute.  Do we care? 
                if (returnsAttribute != null)
                {
                    ((ProjectRootElement)Parent).ContainsTargetsWithReturnsAttribute = true;
                }

                MarkDirty("Set target Returns {0}", value);
            }
        }

        /// <summary>
        /// Location of the Name attribute
        /// </summary>
        public ElementLocation NameLocation => GetAttributeLocation(XMakeAttributes.name);

        /// <summary>
        /// Location of the Inputs attribute
        /// </summary>
        public ElementLocation InputsLocation => GetAttributeLocation(XMakeAttributes.inputs);

        /// <summary>
        /// Location of the Outputs attribute
        /// </summary>
        public ElementLocation OutputsLocation => GetAttributeLocation(XMakeAttributes.outputs);

        /// <summary>
        /// Location of the TrimDuplicateOutputs attribute
        /// </summary>
        public ElementLocation KeepDuplicateOutputsLocation
        {
            get
            {
                ElementLocation location = GetAttributeLocation(XMakeAttributes.keepDuplicateOutputs);
                if ((location == null) && !BuildParameters.KeepDuplicateOutputs)
                {
                    // In 4.0, by default we do NOT keep duplicate outputs unless they user has either set the attribute
                    // explicitly or overridden it globally with MSBUILDKEEPDUPLICATEOUTPUTS set to a non-empty value.                    
                    location = NameLocation;
                }

                return location;
            }
        }

        /// <summary>
        /// Location of the DependsOnTargets attribute
        /// </summary>
        public ElementLocation DependsOnTargetsLocation => GetAttributeLocation(XMakeAttributes.dependsOnTargets);

        /// <summary>
        /// Location of the BeforeTargets attribute
        /// </summary>
        public ElementLocation BeforeTargetsLocation => GetAttributeLocation(XMakeAttributes.beforeTargets);

        /// <summary>
        /// Location of the Returns attribute
        /// </summary>
        public ElementLocation ReturnsLocation => GetAttributeLocation(XMakeAttributes.returns);

        /// <summary>
        /// Location of the AfterTargets attribute
        /// </summary>
        public ElementLocation AfterTargetsLocation => GetAttributeLocation(XMakeAttributes.afterTargets);

        /// <summary>
        /// A cache of the last instance which was created from this target.
        /// </summary>
        internal ProjectTargetInstance TargetInstance { get; set; }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds an item group after the last child.
        /// </summary>
        public ProjectItemGroupElement AddItemGroup()
        {
            ProjectItemGroupElement itemGroup = ContainingProject.CreateItemGroupElement();

            AppendChild(itemGroup);

            return itemGroup;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds a property group after the last child.
        /// </summary>
        public ProjectPropertyGroupElement AddPropertyGroup()
        {
            ProjectPropertyGroupElement propertyGroup = ContainingProject.CreatePropertyGroupElement();

            AppendChild(propertyGroup);

            return propertyGroup;
        }

        /// <summary>
        /// Convenience method to add a task to this target.
        /// Adds after any existing task.
        /// </summary>
        public ProjectTaskElement AddTask(string taskName)
        {
            ErrorUtilities.VerifyThrowArgumentLength(taskName, nameof(taskName));

            ProjectTaskElement task = ContainingProject.CreateTaskElement(taskName);

            AppendChild(task);

            return task;
        }

        /// <inheritdoc />
        public override void CopyFrom(ProjectElement element)
        {
            base.CopyFrom(element);

            // Clear caching fields
            _name = null;
        }

        /// <summary>
        /// Creates an unparented ProjectTargetElement, wrapping an unparented XmlElement.
        /// Validates the name.
        /// Caller should then ensure the element is added to a parent.
        /// </summary>
        internal static ProjectTargetElement CreateDisconnected(string name, ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.target);

            return new ProjectTargetElement(element, containingProject) { Name = name };
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement, "OM_CannotAcceptParent");
        }

        /// <summary>
        /// Marks this element as dirty.
        /// </summary>
        internal override void MarkDirty(string reason, string param)
        {
            base.MarkDirty(reason, param);
            TargetInstance = null;
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateTargetElement(Name);
        }
    }
}
