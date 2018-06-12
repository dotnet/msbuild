// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectItemGroupElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectItemGroupElement represents the ItemGroup element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("#Items={Count} Condition={Condition} Label={Label}")]
    public class ProjectItemGroupElement : ProjectElementContainer
    {
        /// <summary>
        /// True if it is known that no child items have wildcards in their
        /// include. An optimization helping Project.AddItem.
        /// Only reliable if it is true.
        /// </summary>
        private bool _definitelyAreNoChildrenWithWildcards;

        /// <summary>
        /// Initialize a parented ProjectItemGroupElement
        /// </summary>
        internal ProjectItemGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize an unparented ProjectItemGroupElement
        /// </summary>
        private ProjectItemGroupElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Get any child items.
        /// This is a live collection.
        /// </summary>
        public ICollection<ProjectItemElement> Items
        {
            get
            {
                return new ReadOnlyCollection<ProjectItemElement>(Children.OfType<ProjectItemElement>());
            }
        }

        /// <summary>
        /// True if it is known that no child items have wildcards in their
        /// include. An optimization helping Project.AddItem.
        /// Only reliable if it is true.
        /// ONLY TO BE CALLED by ProjectItemElement.
        /// Should be protected+internal.
        /// </summary>
        internal bool DefinitelyAreNoChildrenWithWildcards
        {
            get
            {
                if (Count == 0)
                {
                    _definitelyAreNoChildrenWithWildcards = true;
                }

                return _definitelyAreNoChildrenWithWildcards;
            }

            set
            {
                _definitelyAreNoChildrenWithWildcards = value;
            }
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds a new item ordered by include.
        /// </summary>
        public ProjectItemElement AddItem(string itemType, string include)
        {
            return AddItem(itemType, include, null);
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds a new item ordered by include.
        /// Metadata may be null, indicating no metadata.
        /// </summary>
        public ProjectItemElement AddItem(string itemType, string include, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            ErrorUtilities.VerifyThrowArgumentLength(itemType, "itemType");
            ErrorUtilities.VerifyThrowArgumentLength(include, "include");

            // If there are no items, or it turns out that there are only items with 
            // item types that sort earlier, then we should go after the last child
            ProjectElement reference = LastChild;

            foreach (ProjectItemElement item in Items)
            {
                // If it's the same item type, and 
                if (MSBuildNameIgnoreCaseComparer.Default.Equals(itemType, item.ItemType))
                {
                    // the include sorts after us,
                    if (String.Compare(include, item.Include, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        // then insert before it (ie. after the previous sibling)
                        reference = item.PreviousSibling;
                        break;
                    }

                    // Otherwise go to the next item
                    continue;
                }

                // If it's an item type that sorts after us,
                if (String.Compare(itemType, item.ItemType, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    // then insert before it (ie. after the previous sibling)
                    reference = item.PreviousSibling;
                    break;
                }
            }

            ProjectItemElement newItem = ContainingProject.CreateItemElement(itemType, include);

            // If reference is null, this will prepend
            InsertAfterChild(newItem, reference);

            if (metadata != null)
            {
                foreach (KeyValuePair<string, string> metadatum in metadata)
                {
                    newItem.AddMetadata(metadatum.Key, metadatum.Value);
                }
            }

            return newItem;
        }

        /// <inheritdoc />
        public override void CopyFrom(ProjectElement element)
        {
            base.CopyFrom(element);

            // clear out caching fields.
            _definitelyAreNoChildrenWithWildcards = false;
        }

        /// <summary>
        /// Creates an unparented ProjectItemGroupElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to the XmlDocument in the appropriate location.
        /// </summary>
        internal static ProjectItemGroupElement CreateDisconnected(ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.itemGroup);

            return new ProjectItemGroupElement(element, containingProject);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement || parent is ProjectTargetElement || parent is ProjectWhenElement || parent is ProjectOtherwiseElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateItemGroupElement();
        }
    }
}
