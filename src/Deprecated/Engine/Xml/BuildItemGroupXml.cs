// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.Collections.Generic;
using Microsoft.Build.BuildEngine.Shared;
using System.Xml;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Encapsulates, as far as possible, any XML behind a BuildItemGroup
    /// </summary>
    internal class BuildItemGroupXml
    {
        #region Fields

        // The <BuildItemGroup> element
        private XmlElement element;
        // The Condition attribute on it, if any, to save lookup
        private XmlAttribute conditionAttribute;

        #endregion

        #region Constructors

        /// <summary>
        /// BuildItemGroup element is provided
        /// </summary>
        internal BuildItemGroupXml(XmlElement element)
        {
            ErrorUtilities.VerifyThrowNoAssert(element != null, "Need a valid XML node.");
            ProjectXmlUtilities.VerifyThrowElementName(element, XMakeElements.itemGroup);

            this.element = element;
            this.conditionAttribute = ProjectXmlUtilities.GetConditionAttribute(element, true /*no other attributes allowed*/);
        }

        /// <summary>
        /// BuildItemGroup element is created given a document
        /// </summary>
        internal BuildItemGroupXml(XmlDocument owner)
        {
            ErrorUtilities.VerifyThrowNoAssert(owner != null, "Need valid XmlDocument owner for this item group.");
            this.element = owner.CreateElement(XMakeElements.itemGroup, XMakeAttributes.defaultXmlNamespace);
        }

        #endregion

        #region Properties

        internal string Condition
        {
            get
            {
                return conditionAttribute != null ? conditionAttribute.Value : String.Empty;
            }

            set
            {
                conditionAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(element, XMakeAttributes.condition, value);
            }
        }

        internal XmlElement ParentElement
        {
            get
            {
                if (element?.ParentNode is XmlElement)
                {
                    return (XmlElement)element.ParentNode;
                }
                return null;
            }
        }

        internal XmlElement Element
        {
            get { return element; }
        }

        internal XmlAttribute ConditionAttribute
        {
            get { return conditionAttribute; }
        }

        internal XmlDocument OwnerDocument
        {
            get { return element.OwnerDocument; }
        }

        #endregion

        #region Methods

        internal List<XmlElement> GetChildren()
        {
            List<XmlElement> children = ProjectXmlUtilities.GetValidChildElements(element);
            return children;
        }

        internal void AppendChild(XmlElement child)
        {
            element.AppendChild(child);
        }

        internal void InsertAfter(XmlElement parent, XmlElement child, XmlElement reference)
        {
            parent.InsertAfter(child, reference);
        }

        internal void InsertBefore(XmlElement parent, XmlElement child, XmlElement reference)
        {
            parent.InsertBefore(child, reference);
        }

        #endregion
    }
}
