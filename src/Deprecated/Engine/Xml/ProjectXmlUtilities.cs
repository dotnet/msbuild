// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Project-related Xml utilities
    /// </summary>
    internal class ProjectXmlUtilities
    {
        /// <summary>
        /// Gets child elements, ignoring whitespace and comments.
        /// </summary>
        /// <exception cref="InvalidProjectFileException">Thrown for elements in the wrong namespace, or unexpected XML node types</exception>
        internal static List<XmlElement> GetValidChildElements(XmlElement element)
        {
            List<XmlElement> children = new List<XmlElement>();

            foreach (XmlNode child in element)
            {
                switch (child.NodeType)
                {
                    case XmlNodeType.Comment:
                    case XmlNodeType.Whitespace:
                        // These are legal, and ignored
                        break;

                    case XmlNodeType.Element:
                        XmlElement childElement = (XmlElement)child;
                        VerifyThrowProjectValidNamespace(childElement);
                        children.Add(childElement);
                        break;

                    default:
                        ThrowProjectInvalidChildElement(child);
                        break;
                }
            }
            return children;
        }

        /// <summary>
        /// Throw an invalid project exception if the child is not an XmlElement
        /// </summary>
        /// <param name="childNode"></param>
        internal static void VerifyThrowProjectXmlElementChild(XmlNode childNode)
        {
            if (childNode.NodeType != XmlNodeType.Element)
            {
                ThrowProjectInvalidChildElement(childNode);
            }
        }

        /// <summary>
        /// Throw an invalid project exception if there are any child elements at all
        /// </summary>
        internal static void VerifyThrowProjectNoChildElements(XmlElement element)
        {
            List<XmlElement> childElements = GetValidChildElements(element);
            if (childElements.Count > 0)
            {
                ThrowProjectInvalidChildElement(element.FirstChild);
            }            
        }

        /// <summary>
        /// Throw an invalid project exception indicating that the child is not valid beneath the element
        /// </summary>
        internal static void ThrowProjectInvalidChildElement(XmlNode child)
        {
            ProjectErrorUtilities.ThrowInvalidProject(child, "UnrecognizedChildElement", child.Name, child.ParentNode.Name);
        }

        /// <summary>
        /// Throws an InternalErrorException if the name of the element is not the expected name.
        /// </summary>
        internal static void VerifyThrowElementName(XmlElement element, string expected)
        {
            ErrorUtilities.VerifyThrowNoAssert(String.Equals(element.Name, expected, StringComparison.Ordinal), "Expected " + expected + " element, got " + element.Name);
        }

        /// <summary>
        /// Verifies an element has a valid name, and is in the MSBuild namespace, otherwise throws an InvalidProjectFileException.
        /// </summary>
        internal static void VerifyThrowProjectValidNameAndNamespace(XmlElement element)
        {
            XmlUtilities.VerifyThrowProjectValidElementName(element);
            VerifyThrowProjectValidNamespace(element);
        }

        /// <summary>
        /// Verifies that an element is in the MSBuild namespace, otherwise throws an InvalidProjectFileException.
        /// </summary>
        internal static void VerifyThrowProjectValidNamespace(XmlElement element)
        {
            if (element.Prefix.Length > 0 ||
                !String.Equals(element.NamespaceURI, XMakeAttributes.defaultXmlNamespace, StringComparison.OrdinalIgnoreCase))
            {
                ProjectErrorUtilities.ThrowInvalidProject(element, "CustomNamespaceNotAllowedOnThisChildElement", element.Name, element.ParentNode.Name);
            }
        }

        /// <summary>
        /// If there are any attributes on the element, throws an InvalidProjectFileException complaining that the attribute is not valid on this element.
        /// </summary>
        internal static void VerifyThrowProjectNoAttributes(XmlElement element)
        {
            foreach(XmlAttribute attribute in element.Attributes)
            {
                ThrowProjectInvalidAttribute(attribute);
            }
        }

        /// <summary>
        /// If the condition is false, throws an InvalidProjectFileException complaining that the attribute is not valid on this element.
        /// </summary>
        internal static void VerifyThrowProjectInvalidAttribute(bool condition, XmlAttribute attribute)
        {
            if (!condition)
            {
                ThrowProjectInvalidAttribute(attribute);
            }
        }

        /// <summary>
        /// Throws an InvalidProjectFileException complaining that the attribute is not valid on this element.
        /// </summary>
        internal static void ThrowProjectInvalidAttribute(XmlAttribute attribute)
        {
            ProjectErrorUtilities.ThrowInvalidProject(attribute, "UnrecognizedAttribute", attribute.Name, attribute.OwnerElement.Name);
        }

        /// <summary>
        /// Get the Condition attribute, if any. Optionally, throw an invalid project exception if there are
        /// any other attributes.
        /// </summary>
        internal static XmlAttribute GetConditionAttribute(XmlElement element, bool verifySoleAttribute)
        {
            XmlAttribute condition = null;
            foreach (XmlAttribute attribute in element.Attributes)
            {
                switch (attribute.Name)
                {
                    case XMakeAttributes.condition:
                        condition = attribute;
                        break;

                    // Label  is only recognized by the new OM.  
                    // Ignore BUT ONLY if the caller of this function is a 
                    // PropertyGroup, ItemDefinitionGroup, or ItemGroup: the "Label"
                    // attribute is only legal on those element types.
                    case XMakeAttributes.label:
                        if (!(
                            String.Equals(element.Name, XMakeElements.propertyGroup, StringComparison.Ordinal) ||
                            String.Equals(element.Name, XMakeElements.itemDefinitionGroup, StringComparison.Ordinal) ||
                            String.Equals(element.Name, XMakeElements.itemGroup, StringComparison.Ordinal)
                            ))
                        {
                            ProjectErrorUtilities.VerifyThrowInvalidProject(!verifySoleAttribute, attribute, "UnrecognizedAttribute", attribute.Name, element.Name);
                        }
                        // otherwise, do nothing.
                        break;

                    default:
                        ProjectErrorUtilities.VerifyThrowInvalidProject(!verifySoleAttribute, attribute, "UnrecognizedAttribute", attribute.Name, element.Name);
                        break;
                }
            }
            return condition;
        }

        /// <summary>
        /// Sets the value of an attribute, but if the value to set is null or empty, just
        /// removes the element. Returns the attribute, or null if it was removed.
        /// </summary>
        internal static XmlAttribute SetOrRemoveAttribute(XmlElement element, string name, string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                // The caller passed in a null or an empty value.  So remove the attribute.
                element.RemoveAttribute(name);
                return null;
            }
            else
            {
                // Set the new attribute value
                element.SetAttribute(name, value);
                XmlAttribute attribute = element.Attributes[name];
                return attribute;
            }
        }

        /// <summary>
        /// Returns the value of the attribute. 
        /// If the attribute is null, returns an empty string.
        /// </summary>
        internal static string GetAttributeValue(XmlAttribute attribute)
        {
            return (attribute == null) ? String.Empty : attribute.Value;
        }
    }
}
