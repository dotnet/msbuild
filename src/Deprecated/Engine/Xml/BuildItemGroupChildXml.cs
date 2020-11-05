// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.BuildEngine.Shared;
using System.Xml;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Encapsulates, as far as possible, any XML behind the child of a BuildItemGroup element
    /// </summary>
    internal class BuildItemGroupChildXml
    {
        #region Fields

        // The element itself
        private XmlElement element;
        // The Condition attribute on it, if any, to save lookup
        private XmlAttribute conditionAttribute;
        // The Include attribute on it, if any, to save lookup
        private XmlAttribute includeAttribute;
        // The Exclude attribute on it, if any, to save lookup
        private XmlAttribute excludeAttribute;
        // The Remove attribute on it, if any, to save lookup
        private XmlAttribute removeAttribute;
        // Whether this is represents an add, remove, or modify
        private ChildType childType;

        #endregion

        #region Constructors

        internal BuildItemGroupChildXml(XmlDocument ownerDocument, string name, string include)
        {
            this.element = ownerDocument.CreateElement(name, XMakeAttributes.defaultXmlNamespace);
            this.Include = include;
        }

        internal BuildItemGroupChildXml(XmlElement element, ChildType childTypeExpected)
        {
            ErrorUtilities.VerifyThrow(element != null, "Need an XML node.");
            ErrorUtilities.VerifyThrowNoAssert(childTypeExpected != ChildType.Invalid, "Can't expect invalid childtype");
            ProjectXmlUtilities.VerifyThrowProjectValidNameAndNamespace(element);

            this.element = element;
    
            // Loop through each of the attributes on the item element.
            foreach (XmlAttribute attribute in element.Attributes)
            {
                switch (attribute.Name)
                {
                    case XMakeAttributes.include:
                        this.includeAttribute = attribute;
                        break;

                    case XMakeAttributes.exclude:  
                        this.excludeAttribute = attribute;
                        break;

                    case XMakeAttributes.condition:
                        this.conditionAttribute = attribute;
                        break;

                    case XMakeAttributes.xmlns:
                        // We already verified that the namespace is correct
                        break;

                    case XMakeAttributes.remove:
                        this.removeAttribute = attribute;
                        break;

                    case XMakeAttributes.keepMetadata:
                    case XMakeAttributes.removeMetadata:
                    case XMakeAttributes.keepDuplicates:
                        // Ignore these - they are part of the new OM.
                        break;

                    default:
                        ProjectXmlUtilities.ThrowProjectInvalidAttribute(attribute);
                        break;
                }
            }

            this.childType = ChildType.Invalid;

            // Default to modify, if that's one of the child types we are told to expect.
            if ((childTypeExpected & ChildType.BuildItemModify) == ChildType.BuildItemModify)
            {
                this.childType = ChildType.BuildItemModify;
            }

            if (this.includeAttribute != null)
            {
                ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute((childTypeExpected & ChildType.BuildItemAdd) == ChildType.BuildItemAdd, includeAttribute);
                ProjectErrorUtilities.VerifyThrowInvalidProject(Include.Length > 0, element, "MissingRequiredAttribute", XMakeAttributes.include, element.Name);
                ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute(removeAttribute == null, removeAttribute);
                this.childType = ChildType.BuildItemAdd;
            }

            if (this.excludeAttribute != null)
            {
                ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute((childTypeExpected & ChildType.BuildItemAdd) == ChildType.BuildItemAdd, excludeAttribute);
                ProjectErrorUtilities.VerifyThrowInvalidProject(Include.Length > 0, element, "MissingRequiredAttribute", XMakeAttributes.include, element.Name);
                ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute(removeAttribute == null, removeAttribute);
                this.childType = ChildType.BuildItemAdd;
            }

            if (this.removeAttribute != null)
            {
                ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute((childTypeExpected & ChildType.BuildItemRemove) == ChildType.BuildItemRemove, removeAttribute);
                ProjectErrorUtilities.VerifyThrowInvalidProject(Remove.Length > 0, element, "MissingRequiredAttribute", XMakeAttributes.remove, element.Name);
                ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute(includeAttribute == null, includeAttribute);
                ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute(excludeAttribute == null, excludeAttribute);
                this.childType = ChildType.BuildItemRemove;
            }

            if (this.childType == ChildType.Invalid)
            {
                // So the xml wasn't consistent with any of the child types that we were told to expect.
                // Figure out the most reasonable message to produce.
                if ((childTypeExpected & ChildType.BuildItemAdd) == ChildType.BuildItemAdd)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(Include.Length > 0, element, "MissingRequiredAttribute", XMakeAttributes.include, element.Name);
                }
                else if ((childTypeExpected & ChildType.BuildItemRemove) == ChildType.BuildItemRemove)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(Remove.Length > 0, element, "MissingRequiredAttribute", XMakeAttributes.remove, element.Name);
                }
                else
                {
                    ErrorUtilities.ThrowInternalError("Unexpected child type");
                }
            }

            // Validate each of the child nodes beneath the item.
            List<XmlElement> children = ProjectXmlUtilities.GetValidChildElements(element);

            if (this.childType == ChildType.BuildItemRemove && children.Count != 0)
            {
                ProjectErrorUtilities.ThrowInvalidProject(element, "ChildElementsBelowRemoveNotAllowed", children[0].Name);
            }

            foreach (XmlElement child in children)
            {
                ProjectXmlUtilities.VerifyThrowProjectValidNameAndNamespace(child);

                ProjectErrorUtilities.VerifyThrowInvalidProject(!FileUtilities.IsItemSpecModifier(child.Name), child, "ItemSpecModifierCannotBeCustomMetadata", child.Name);
                ProjectErrorUtilities.VerifyThrowInvalidProject(XMakeElements.IllegalItemPropertyNames[child.Name] == null, child, "CannotModifyReservedItemMetadata", child.Name);
            }
        }

        #endregion


        #region Properties

        internal string Name
        {
            get
            { 
                return element.Name;
            }

            set
            {
                element = XmlUtilities.RenameXmlElement(element, value, XMakeAttributes.defaultXmlNamespace);

                // Because this actually created a new element, we have to find the attributes again
                includeAttribute = element.Attributes[XMakeAttributes.include];
                excludeAttribute = element.Attributes[XMakeAttributes.exclude];
                conditionAttribute = element.Attributes[XMakeAttributes.condition];
                removeAttribute = element.Attributes[XMakeAttributes.remove];
            }
        }

        internal string Include
        {
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(includeAttribute);
            }

            set
            {
                element.SetAttribute(XMakeAttributes.include, value);
                includeAttribute = element.Attributes[XMakeAttributes.include];
            }
        }

        internal string Exclude
        {
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(excludeAttribute);
            }

            set
            {
                excludeAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(element, XMakeAttributes.exclude, value);
            }
        }

        internal string Remove
        {
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(removeAttribute);
            }
        }

        internal string Condition
        {
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(conditionAttribute);
            }

            set
            {
                conditionAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(element, XMakeAttributes.condition, value);
            }
        }

        internal XmlElement Element
        {
            get { return element; }
        }

        internal XmlAttribute IncludeAttribute
        {
            get { return includeAttribute; }
        }

        internal XmlAttribute ExcludeAttribute
        {
            get { return excludeAttribute; }
        }

        internal XmlAttribute RemoveAttribute
        {
            get { return removeAttribute; }
        }

        internal XmlAttribute ConditionAttribute
        {
            get { return conditionAttribute; }
        }

        internal ChildType ChildType
        {
            get { return childType; }
        }
        
        #endregion

        #region Methods

        /// <summary>
        /// Gets all child elements, ignoring whitespace and comments, and any conditions
        /// </summary>
        internal List<XmlElement> GetChildren()
        {
            List<XmlElement> children = ProjectXmlUtilities.GetValidChildElements(element);
            return children;
        }

        /// <summary>
        /// Removes all child elements with the specified name.
        /// </summary>
        internal void RemoveChildrenByName(string name)
        {
            List<XmlElement> children = GetChildren();
            foreach (XmlElement child in children)
            {
                if (String.Equals(name, child.Name, StringComparison.OrdinalIgnoreCase))
                {
                    element.RemoveChild(child);
                }
            }
        }

        /// <summary>
        /// Ensures there's a child element with the specified name and value.
        /// Disregards any Condition attributes on the children.
        /// If several children are already present with the specified name, removes all except the last one.
        /// If a child is present with the specified name, does not modify it if the value is already as specified.
        /// Returns true if the XML was meaningfully modified.
        /// </summary>
        internal bool SetChildValue(string name, string value)
        {
            bool dirty = false;
            XmlElement childToModify = null;
            List<XmlElement> children = GetChildren();

            foreach (XmlElement child in children)
            {
                if (String.Equals(name, child.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (childToModify != null)
                    {
                        // We already found a matching child, remove that, we'll
                        // use this later one; the later one was winning anyway,
                        // so we don't consider this dirtying the item
                        element.RemoveChild(childToModify);
                    }

                    childToModify = child;
                }
            }

            if (childToModify == null)
            {
                // create a new child as specified
                childToModify = element.OwnerDocument.CreateElement(name, XMakeAttributes.defaultXmlNamespace);
                element.AppendChild(childToModify);
                dirty = true;
            }

            // if we just added this child, or the old value and new value are different...
            if (dirty || !String.Equals(Utilities.GetXmlNodeInnerContents(childToModify), value, StringComparison.Ordinal))
            {
                // give the child the new value
                Utilities.SetXmlNodeInnerContents(childToModify, value);
                dirty = true;
            }

            return dirty;
        }

        #endregion
    }

    /// <summary>
    /// Type of the item group child element
    /// </summary>
    internal enum ChildType
    {
        Invalid = 0,

        /// <summary>
        /// Regular item, with Include and possibly Exclude attributes
        /// </summary>
        BuildItemAdd = 1,

        /// <summary>
        /// Remove item, with Remove attribute
        /// </summary>
        BuildItemRemove = 2,

        /// <summary>
        /// Modify item, with no attributes (except possibly Condition)
        /// </summary>
        BuildItemModify = 4,

        /// <summary>
        /// Add, remove, or modify item expression
        /// </summary>
        Any = BuildItemAdd | BuildItemRemove | BuildItemModify
    }
}
