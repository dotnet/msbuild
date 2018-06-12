// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Project-related Xml utilities
    /// </summary>
    internal static partial class ProjectXmlUtilities
    {
        /// <summary>
        /// Gets child elements, ignoring whitespace and comments.
        /// Throws InvalidProjectFileException for unexpected XML node types.
        /// </summary>
        internal static XmlElementChildIterator GetVerifyThrowProjectChildElements(XmlElementWithLocation element)
        {
            return GetChildElements(element, true /*throw for unexpected node types*/);
        }

        /// <summary>
        /// Gets child elements, ignoring whitespace and comments.
        /// Throws InvalidProjectFileException for unexpected XML node types if parameter is set.
        /// </summary>
        private static XmlElementChildIterator GetChildElements(XmlElementWithLocation element, bool throwForInvalidNodeTypes)
        {
            return new XmlElementChildIterator(element, throwForInvalidNodeTypes);
        }

        /// <summary>
        /// Throw an invalid project exception if there are any child elements at all
        /// </summary>
        internal static void VerifyThrowProjectNoChildElements(XmlElementWithLocation element)
        {
            foreach (var child in GetVerifyThrowProjectChildElements(element))
            {
                ThrowProjectInvalidChildElement(child.Name, element.Name, element.Location);
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
            VerifyThrowProjectAttributeEitherMissingOrNotEmpty(xmlElement, xmlElement.GetAttributeWithLocation(attributeName), attributeName);
        }

        /// <summary>
        /// Verifies that if the attribute is present on the element, its value is not empty
        /// </summary>
        internal static void VerifyThrowProjectAttributeEitherMissingOrNotEmpty(XmlElementWithLocation xmlElement, XmlAttributeWithLocation attribute, string attributeName)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject
            (
                attribute == null || attribute.Value.Length > 0,
                attribute?.Location,
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
        internal static void VerifyThrowProjectAttributes(XmlElementWithLocation element, HashSet<string> validAttributes)
        {
            foreach (XmlAttributeWithLocation attribute in element.Attributes)
            {
                ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute(validAttributes.Contains(attribute.Name), attribute);
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
        /// of nullIfNotExists.
        /// </summary>
        internal static string GetAttributeValue(XmlElementWithLocation element, string attributeName, bool nullIfNotExists)
        {
            XmlAttributeWithLocation attribute = (XmlAttributeWithLocation)element.GetAttributeNode(attributeName);
            return GetAttributeValue(attribute, nullIfNotExists);
        }
    }
}
