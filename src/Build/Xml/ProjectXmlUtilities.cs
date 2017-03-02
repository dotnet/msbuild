// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Project-related Xml utilities
    /// </summary>
    internal class ProjectXmlUtilities
    {
        /// <summary>
        /// Gets child elements, ignoring whitespace and comments.
        /// Verifies xml namespace of elements is the MSBuild namespace.
        /// Throws InvalidProjectFileException for elements in the wrong namespace, and unexpected XML node types
        /// </summary>
        internal static List<XmlElementWithLocation> GetVerifyThrowProjectChildElements(XmlElementWithLocation element)
        {
            return GetChildElements(element, true /*throw for unexpected node types*/);
        }

        /// <summary>
        /// Gets child elements, ignoring whitespace and comments.
        /// Verifies xml namespace of elements is the MSBuild namespace.
        /// Throws InvalidProjectFileException for elements in the wrong namespace, and (if parameter is set) unexpected XML node types
        /// </summary>
        private static List<XmlElementWithLocation> GetChildElements(XmlElementWithLocation element, bool throwForInvalidNodeTypes)
        {
            List<XmlElementWithLocation> children = new List<XmlElementWithLocation>();

            foreach (XmlNode child in element)
            {
                switch (child.NodeType)
                {
                    case XmlNodeType.Comment:
                    case XmlNodeType.Whitespace:
                        // These are legal, and ignored
                        break;

                    case XmlNodeType.Element:
                        XmlElementWithLocation childElement = (XmlElementWithLocation)child;
                        VerifyThrowProjectValidNamespace(childElement);
                        children.Add(childElement);
                        break;

                    default:
                        if (child.NodeType == XmlNodeType.Text && String.IsNullOrWhiteSpace(child.InnerText))
                        {
                            // If the text is greather than 4k and only contains whitespace, the XML reader will assume it's a text node
                            // instead of ignoring it.  Our call to String.IsNullOrWhiteSpace() can be a little slow if the text is
                            // large but this should be extremely rare.
                            break;
                        }
                        if (throwForInvalidNodeTypes)
                        {
                            ThrowProjectInvalidChildElement(child.Name, element.Name, element.Location);
                        }
                        break;
                }
            }
            return children;
        }

        /// <summary>
        /// Throw an invalid project exception if there are any child elements at all
        /// </summary>
        internal static void VerifyThrowProjectNoChildElements(XmlElementWithLocation element)
        {
            List<XmlElementWithLocation> childElements = GetVerifyThrowProjectChildElements(element);
            if (childElements.Count > 0)
            {
                ThrowProjectInvalidChildElement(element.FirstChild.Name, element.Name, element.Location);
            }
        }


        /// <summary>
        /// Throw an invalid project exception indicating that the child is not valid beneath the element because it is a duplicate
        /// </summary>
        internal static void ThrowProjectInvalidChildElementDueToDuplicate(XmlElementWithLocation child)
        {
            ProjectErrorUtilities.ThrowInvalidProject(child.Location, "InvalidChildElementDueToDuplication", child.Name, child.ParentNode.Name);
        }

        /// <summary>
        /// Throw an invalid project exception indicating that the child is not valid beneath the element
        /// </summary>
        internal static void ThrowProjectInvalidChildElement(string name, string parentName, ElementLocation location)
        {
            ProjectErrorUtilities.ThrowInvalidProject(location, "UnrecognizedChildElement", name, parentName);
        }

        /// <summary>
        /// Verifies that an element is in the MSBuild namespace, otherwise throws an InvalidProjectFileException.
        /// </summary>
        internal static void VerifyThrowProjectValidNamespace(XmlElementWithLocation element)
        {
            // If a namespace was specified it must be the default MSBuild namespace.
            if (!VerifyValidProjectNamespace(element))
            {
                ProjectErrorUtilities.ThrowInvalidProject(element.Location,
                    "CustomNamespaceNotAllowedOnThisChildElement", element.Name, element.ParentNode?.Name);
            }
        }

        /// <summary>
        /// Verify that if a namespace is specified it matches the default MSBuild namespace.
        /// </summary>
        /// <param name="element">Element to check namespace.</param>
        /// <returns>True when the namespace is in the MSBuild namespace or no namespace.</returns>
        internal static bool VerifyValidProjectNamespace(XmlElementWithLocation element)
        {
            return
                // Prefix must be empty
                element.Prefix.Length == 0 &&

                // Namespace must equal to the MSBuild namespace or empty
                (string.Equals(element.NamespaceURI, XMakeAttributes.defaultXmlNamespace,
                     StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(element.NamespaceURI));
        }

        /// <summary>
        /// Verifies that if the attribute is present on the element, its value is not empty
        /// </summary>
        internal static void VerifyThrowProjectAttributeEitherMissingOrNotEmpty(XmlElementWithLocation xmlElement, string attributeName)
        {
            XmlAttributeWithLocation attribute = xmlElement.GetAttributeWithLocation(attributeName);

            ProjectErrorUtilities.VerifyThrowInvalidProject
                (
                    attribute == null || attribute.Value.Length > 0,
                    (attribute == null) ? null : attribute.Location,
                    "InvalidAttributeValue",
                    String.Empty,
                    attributeName,
                    xmlElement.Name
                );
        }

        /// <summary>
        /// If there are any attributes on the element, throws an InvalidProjectFileException complaining that the attribute is not valid on this element.
        /// </summary>
        internal static void VerifyThrowProjectNoAttributes(XmlElementWithLocation element)
        {
            if (element.HasAttributes)
            {
                foreach (XmlAttributeWithLocation attribute in element.Attributes)
                {
                    ThrowProjectInvalidAttribute(attribute);
                }
            }
        }

        /// <summary>
        /// If the condition is false, throws an InvalidProjectFileException complaining that the attribute is not valid on this element.
        /// </summary>
        internal static void VerifyThrowProjectInvalidAttribute(bool condition, XmlAttributeWithLocation attribute)
        {
            if (!condition)
            {
                ThrowProjectInvalidAttribute(attribute);
            }
        }

        /// <summary>
        /// Verify that the element has the specified required attribute on it and
        /// it has a value other than empty string
        /// </summary>
        internal static void VerifyThrowProjectRequiredAttribute(XmlElementWithLocation element, string attributeName)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject(element.GetAttribute(attributeName).Length > 0, element.Location, "MissingRequiredAttribute", attributeName, element.Name);
        }

        /// <summary>
        /// Verify  that all attributes on the element are on the list of legal attributes
        /// </summary>
        internal static void VerifyThrowProjectAttributes(XmlElementWithLocation element, string[] validAttributes)
        {
            foreach (XmlAttributeWithLocation attribute in element.Attributes)
            {
                bool valid = false;

                for (int i = 0; i < validAttributes.Length; i++)
                {
                    if (String.Equals(attribute.Name, validAttributes[i], StringComparison.Ordinal))
                    {
                        valid = true;
                        break;
                    }
                }

                ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute(valid, attribute);
            }
        }

        /// <summary>
        /// Throws an InvalidProjectFileException complaining that the attribute is not valid on this element.
        /// </summary>
        internal static void ThrowProjectInvalidAttribute(XmlAttributeWithLocation attribute)
        {
            ProjectErrorUtilities.ThrowInvalidProject(attribute.Location, "UnrecognizedAttribute", attribute.Name, attribute.OwnerElement.Name);
        }

        /// <summary>
        /// Sets the value of an attribute, but if the value to set is null or empty, just
        /// removes the attribute. Returns the attribute, or null if it was removed.
        /// UNDONE: Make this return a bool if the attribute did not change, so we can avoid dirtying.
        /// </summary>
        internal static XmlAttributeWithLocation SetOrRemoveAttribute(XmlElementWithLocation element, string name, string value)
        {
            return SetOrRemoveAttribute(element, name, value, false /* remove the attribute if setting to empty string */);
        }

        /// <summary>
        /// Sets the value of an attribute, removing the attribute if the value is null, but still setting it 
        /// if the value is the empty string. Returns the attribute, or null if it was removed.
        /// UNDONE: Make this return a bool if the attribute did not change, so we can avoid dirtying.
        /// </summary>
        internal static XmlAttributeWithLocation SetOrRemoveAttribute(XmlElementWithLocation element, string name, string value, bool allowSettingEmptyAttributes)
        {
            if (value == null || (!allowSettingEmptyAttributes && value.Length == 0))
            {
                // The caller passed in a null or an empty value.  So remove the attribute.
                element.RemoveAttribute(name);
                return null;
            }
            else
            {
                // Set the new attribute value
                element.SetAttribute(name, value);
                XmlAttributeWithLocation attribute = (XmlAttributeWithLocation)element.Attributes[name];
                return attribute;
            }
        }

        /// <summary>
        /// Returns the value of the attribute. 
        /// If the attribute is null, returns an empty string.
        /// </summary>
        internal static string GetAttributeValue(XmlAttributeWithLocation attribute, bool returnNullForNonexistentAttributes)
        {
            if (attribute == null)
            {
                return returnNullForNonexistentAttributes ? null : String.Empty;
            }
            else
            {
                return attribute.Value;
            }
        }

        /// <summary>
        /// Returns the value of the attribute. 
        /// If the attribute is not present, returns an empty string.
        /// </summary>
        internal static string GetAttributeValue(XmlElementWithLocation element, string attributeName)
        {
            return GetAttributeValue(element, attributeName, false /* if the attribute is not present, return an empty string */);
        }

        /// <summary>
        /// Returns the value of the attribute. 
        /// If the attribute is not present, returns either null or an empty string, depending on the value 
        /// of returnNullForNonexistentAttributes.
        /// </summary>
        internal static string GetAttributeValue(XmlElementWithLocation element, string attributeName, bool returnNullForNonexistentAttributes)
        {
            XmlAttributeWithLocation attribute = (XmlAttributeWithLocation)element.GetAttributeNode(attributeName);
            return GetAttributeValue(attribute, returnNullForNonexistentAttributes);
        }
    }
}
