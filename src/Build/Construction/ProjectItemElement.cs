// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectItemElement class represents the Item element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("{ItemType} Include={Include} Exclude={Exclude} #Metadata={Count} Condition={Condition}")]
    public class ProjectItemElement : ProjectElementContainer
    {
        internal ProjectItemElementLink ItemLink => (ProjectItemElementLink)Link;

        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectItemElement(ProjectItemElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Include value cached for performance
        /// </summary>
        private string _include;

        /// <summary>
        /// Exclude value cached for performance
        /// </summary>
        private string _exclude;

        /// <summary>
        /// Remove value cached for performance
        /// </summary>
        private string _remove;

        /// <summary>
        /// MatchOnMetadata value cached for performance
        /// </summary>
        private string _matchOnMetadata;

        /// <summary>
        /// Update value cached for performance
        /// </summary>
        private string _update;

        /// <summary>
        /// Whether the include value has wildcards, 
        /// cached for performance.
        /// </summary>
        private bool? _includeHasWildcards;

        /// <summary>
        /// Initialize a parented ProjectItemElement instance
        /// </summary>
        internal ProjectItemElement(XmlElementWithLocation xmlElement, ProjectItemGroupElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        /// Initialize an unparented ProjectItemElement instance
        /// </summary>
        private ProjectItemElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Gets the item's type.
        /// </summary>
        public string ItemType
        {
            [DebuggerStepThrough]
            get => ElementName;
            set => ChangeItemType(value);
        }

        /// <summary>
        /// Gets or sets the Include value. 
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty or null.
        /// </summary>
        public string Include
        {
            [DebuggerStepThrough]
            get
            {
                // No thread-safety lock required here because many reader threads would set the same value to the field.
                return GetAttributeValue(XMakeAttributes.include, ref _include);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(value) || (Remove.Length == 0 && Update.Length == 0) , "OM_OneOfAttributeButNotMore", ElementName, XMakeAttributes.include, XMakeAttributes.remove, XMakeAttributes.update);
                SetOrRemoveAttribute(XMakeAttributes.include, value, ref _include, "Set item Include {0}", value);
                _includeHasWildcards = null;
            }
        }

        /// <summary>
        /// Gets or sets the Exclude value. 
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty or null.
        /// </summary>
        public string Exclude
        {
            [DebuggerStepThrough]
            get
            {
                // No thread-safety lock required here because many reader threads would set the same value to the field.
                return GetAttributeValue(XMakeAttributes.exclude, ref _exclude);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(value) || Remove.Length == 0, "OM_EitherAttributeButNotBoth", ElementName, XMakeAttributes.exclude, XMakeAttributes.remove);
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(value) || Update.Length == 0, "OM_EitherAttributeButNotBoth", ElementName, XMakeAttributes.exclude, XMakeAttributes.update);
                SetOrRemoveAttribute(XMakeAttributes.exclude, value, ref _exclude, "Set item Exclude {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the Remove value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty or null.
        /// </summary>
        public string Remove
        {
            [DebuggerStepThrough]
            get
            {
                // No thread-safety lock required here because many reader threads would set the same value to the field.
                return GetAttributeValue(XMakeAttributes.remove, ref _remove);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(value) || (Include.Length == 0 && Update.Length == 0), "OM_OneOfAttributeButNotMore", ElementName, XMakeAttributes.include, XMakeAttributes.remove, XMakeAttributes.update);
                SetOrRemoveAttribute(XMakeAttributes.remove, value, ref _remove, "Set item Remove {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the Update value.
        /// </summary>
        public string Update
        {
            [DebuggerStepThrough]
            get
            {
                // No thread-safety lock required here because many reader threads would set the same value to the field.
                return GetAttributeValue(XMakeAttributes.update, ref _update);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(value) || (Remove.Length == 0 && Include.Length == 0), "OM_OneOfAttributeButNotMore", ElementName, XMakeAttributes.include, XMakeAttributes.remove, XMakeAttributes.update);
                SetOrRemoveAttribute(XMakeAttributes.update, value, ref _update, "Set item Update {0}", value);
            }
        }

        /// <summary>
        /// Gets of sets the MatchOnMetadata value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty or null.
        /// </summary>
        public string MatchOnMetadata
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.matchOnMetadata, ref _matchOnMetadata);
            }

            set
            {
                // MatchOnMetadata must be inside of a target
                ErrorUtilities.VerifyThrowInvalidOperation(Parent == null || Parent.Parent is ProjectTargetElement || Parent.Parent is ProjectRootElement, "OM_NoMatchOnMetadataOutsideTargets");
                // MatchOnMetadata must be inside of a remove item
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(value) || RemoveMetadata.Length != 0, "OM_MatchOnMetadataOnlyApplicableToRemoveItems", ElementName, XMakeAttributes.matchOnMetadata);
                SetOrRemoveAttribute(XMakeAttributes.matchOnMetadata, value, ref _matchOnMetadata, "Set item MatchOnMetadata {0}", value);
            }
        }

        /// <summary>
        /// Gets of sets the MatchOnMetadataOptions value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty or null.
        /// </summary>
        public string MatchOnMetadataOptions
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.matchOnMetadataOptions);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(value) || MatchOnMetadata.Length != 0, "OM_MatchOnMetadataOptionsOnlyApplicableToItemsWithMatchOnMetadata", ElementName, XMakeAttributes.matchOnMetadataOptions);
                SetOrRemoveAttribute(XMakeAttributes.matchOnMetadataOptions, value, "Set item MatchOnMetadataOptions {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the KeepMetadata value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty or null.
        /// </summary>
        public string KeepMetadata
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.keepMetadata);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(Parent == null || Parent.Parent is ProjectTargetElement, "OM_NoKeepMetadataOutsideTargets");
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(value) || RemoveMetadata.Length == 0, "OM_EitherAttributeButNotBoth", ElementName, XMakeAttributes.removeMetadata, XMakeAttributes.keepMetadata);
                SetOrRemoveAttribute(XMakeAttributes.keepMetadata, value, "Set item KeepMetadata {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the RemoveMetadata value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty or null.
        /// </summary>
        public string RemoveMetadata
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.removeMetadata);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(Parent == null || Parent.Parent is ProjectTargetElement, "OM_NoRemoveMetadataOutsideTargets");
                ErrorUtilities.VerifyThrowInvalidOperation(String.IsNullOrEmpty(value) || KeepMetadata.Length == 0, "OM_EitherAttributeButNotBoth", ElementName, XMakeAttributes.keepMetadata, XMakeAttributes.removeMetadata);
                SetOrRemoveAttribute(XMakeAttributes.removeMetadata, value, "Set item RemoveMetadata {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the KeepDuplicates value.
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty or null.
        /// </summary>
        public string KeepDuplicates
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.keepDuplicates);
            }

            set
            {
                ErrorUtilities.VerifyThrowInvalidOperation(Parent == null || Parent.Parent is ProjectTargetElement, "OM_NoKeepDuplicatesOutsideTargets");
                SetOrRemoveAttribute(XMakeAttributes.keepDuplicates, value, "Set item KeepDuplicates {0}", value);
            }
        }

        /// <summary>
        /// Whether there are any child metadata elements
        /// </summary>
        public bool HasMetadata => FirstChild != null;

        /// <summary>
        /// Get any child metadata.
        /// </summary>
        public ICollection<ProjectMetadataElement> Metadata => new ReadOnlyCollection<ProjectMetadataElement>(Children.OfType<ProjectMetadataElement>());

        /// <summary>
        /// Location of the include attribute
        /// </summary>
        public ElementLocation IncludeLocation => GetAttributeLocation(XMakeAttributes.include);

        /// <summary>
        /// Location of the exclude attribute
        /// </summary>
        public ElementLocation ExcludeLocation => GetAttributeLocation(XMakeAttributes.exclude);

        /// <summary>
        /// Location of the remove attribute
        /// </summary>
        public ElementLocation RemoveLocation => GetAttributeLocation(XMakeAttributes.remove);

        /// <summary>
        /// Location of the update attribute
        /// </summary>
        public ElementLocation UpdateLocation => GetAttributeLocation(XMakeAttributes.update);

        /// <summary>
        /// Location of the MatchOnMetadata attribute
        /// </summary>
        public ElementLocation MatchOnMetadataLocation => GetAttributeLocation(XMakeAttributes.matchOnMetadata);

        /// <summary>
        /// Location of the MatchOnMetadataOptions attribute
        /// </summary>
        public ElementLocation MatchOnMetadataOptionsLocation => GetAttributeLocation(XMakeAttributes.matchOnMetadataOptions);

        /// <summary>
        /// Location of the keepMetadata attribute
        /// </summary>
        public ElementLocation KeepMetadataLocation => GetAttributeLocation(XMakeAttributes.keepMetadata);

        /// <summary>
        /// Location of the removeMetadata attribute
        /// </summary>
        public ElementLocation RemoveMetadataLocation => GetAttributeLocation(XMakeAttributes.removeMetadata);

        /// <summary>
        /// Location of the keepDuplicates attribute
        /// </summary>
        public ElementLocation KeepDuplicatesLocation => GetAttributeLocation(XMakeAttributes.keepDuplicates);

        /// <summary>
        /// Whether the include value has wildcards, 
        /// cached for performance.
        /// </summary>
        internal bool IncludeHasWildcards
        {
            get
            {
                // No thread-safety lock required here because many reader threads would set the same value to the field.
                if (!_includeHasWildcards.HasValue)
                {
                    _includeHasWildcards = (Include != null) && FileMatcher.HasWildcards(_include);
                }

                return _includeHasWildcards.Value;
            }
        }

        /// <summary>
        /// Internal helper to get the next ProjectItemElement sibling.
        /// If there is none, returns null.
        /// </summary>
        internal ProjectItemElement NextItem
        {
            get
            {
                ProjectItemElement result = null;
                ProjectElement sibling = NextSibling;

                while (sibling != null && result == null)
                {
                    result = NextSibling as ProjectItemElement;
                    sibling = sibling.NextSibling;
                }

                return result;
            }
        }

        /// <summary>
        /// Convenience method to add a piece of metadata to this item.
        /// Adds after any existing metadata. Does not modify any existing metadata.
        /// </summary>
        public ProjectMetadataElement AddMetadata(string name, string unevaluatedValue)
        {
            return AddMetadata(name, unevaluatedValue, false);
        }

        /// <summary>
        /// Convenience method to add a piece of metadata to this item.
        /// Adds after any existing metadata. Does not modify any existing metadata.
        /// </summary>
        /// <param name="name">The name of the metadata to add</param>
        /// <param name="unevaluatedValue">The value of the metadata to add</param>
        /// <param name="expressAsAttribute">If true, then the metadata will be expressed as an attribute instead of a child element, for example
        /// &lt;Reference Include="Libary.dll" HintPath="..\lib\Library.dll" Private="True" /&gt;
        /// </param>
        public ProjectMetadataElement AddMetadata(string name, string unevaluatedValue, bool expressAsAttribute)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, nameof(name));
            ErrorUtilities.VerifyThrowArgumentNull(unevaluatedValue, nameof(unevaluatedValue));

            if (expressAsAttribute)
            {
                ProjectMetadataElement.ValidateValidMetadataAsAttributeName(name, ElementName, Location);
            }

            ProjectMetadataElement metadata = ContainingProject.CreateMetadataElement(name);
            metadata.Value = unevaluatedValue;
            metadata.ExpressedAsAttribute = expressAsAttribute;

            AppendChild(metadata);

            return metadata;
        }

        /// <inheritdoc />
        public override void CopyFrom(ProjectElement element)
        {
            base.CopyFrom(element);

            // clear cached fields
            this.ClearAttributeCache();
        }

        /// <summary>
        /// Creates an unparented ProjectItemElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent.
        /// </summary>
        internal static ProjectItemElement CreateDisconnected(string itemType, ProjectRootElement containingProject)
        {
            XmlUtilities.VerifyThrowArgumentValidElementName(itemType);
            ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(itemType), "CannotModifyReservedItem", itemType);

            XmlElementWithLocation element = containingProject.CreateElement(itemType);

            ProjectItemElement item = new ProjectItemElement(element, containingProject);

            return item;
        }

        /// <summary>
        /// Changes the item type.
        /// </summary>
        /// <remarks>
        /// The implementation has to actually replace the element to do this.
        /// </remarks>
        internal void ChangeItemType(string newItemType)
        {
            ErrorUtilities.VerifyThrowArgumentLength(newItemType, nameof(newItemType));
            XmlUtilities.VerifyThrowArgumentValidElementName(newItemType);
            ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newItemType), "CannotModifyReservedItem", newItemType);
            if (Link != null)
            {
                ItemLink.ChangeItemType(newItemType);
                return;
            }

            // Because the element was created from our special XmlDocument, we know it's
            // an XmlElementWithLocation.
            XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, newItemType, XmlElement.NamespaceURI);

            ReplaceElement(newElement);
        }

        internal override void ClearAttributeCache()
        {
            base.ClearAttributeCache();
            _include = null;
            _exclude = null;
            _remove = null;
            _matchOnMetadata = null;
            _update = null;
            _includeHasWildcards = null;
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent.Parent is ProjectTargetElement || (Include.Length > 0 || Update.Length > 0 || Remove.Length > 0), "OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove");
            ErrorUtilities.VerifyThrowInvalidOperation(parent.Parent is ProjectRootElement || parent.Parent is ProjectTargetElement || parent.Parent is ProjectWhenElement || parent.Parent is ProjectOtherwiseElement, "OM_CannotAcceptParent");
        }

        /// <summary>
        /// Overridden to update the parent's children-have-no-wildcards flag.
        /// </summary>
        internal override void OnAfterParentChanged(ProjectElementContainer parent)
        {
            base.OnAfterParentChanged(parent);

            if (parent != null)
            {
                // This is our indication that we just got attached to a parent
                // Update its children-with-wildcards flag
                if (parent is ProjectItemGroupElement groupParent && groupParent.DefinitelyAreNoChildrenWithWildcards && IncludeHasWildcards)
                {
                    groupParent.DefinitelyAreNoChildrenWithWildcards = false;
                }
            }
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateItemElement(ItemType, Include);
        }

        /// <summary>
        /// Do not clone attributes which can be metadata. The corresponding expressed as attribute project elements are responsible for adding their attribute
        /// </summary>
        protected override bool ShouldCloneXmlAttribute(XmlAttribute attribute) => !ProjectMetadataElement.AttributeNameIsValidMetadataName(attribute.LocalName);

        internal override bool ShouldCloneXmlAttribute(XmlAttributeLink attributeLink) => !ProjectMetadataElement.AttributeNameIsValidMetadataName(attributeLink.LocalName);
    }
}
