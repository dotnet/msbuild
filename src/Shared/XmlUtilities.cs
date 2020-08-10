// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text.RegularExpressions;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for XML manipulation.
    /// </summary>
    static internal class XmlUtilities
    {
        /// <summary>
        /// This method renames an XML element.  Well, actually you can't directly
        /// rename an XML element using the DOM, so what you have to do is create
        /// a brand new XML element with the new name, and copy over all the attributes
        /// and children.  This method returns the new XML element object.
        /// If the name is the same, does nothing and returns the element passed in.
        /// </summary>
        /// <param name="oldElement"></param>
        /// <param name="newElementName"></param>
        /// <param name="xmlNamespace">Can be null if global namespace.</param>
        /// <returns>new/renamed element</returns>
        internal static XmlElementWithLocation RenameXmlElement(XmlElementWithLocation oldElement, string newElementName, string xmlNamespace)
        {
            if (String.Equals(oldElement.Name, newElementName, StringComparison.Ordinal) && String.Equals(oldElement.NamespaceURI, xmlNamespace, StringComparison.Ordinal))
            {
                return oldElement;
            }

            XmlElementWithLocation newElement = (xmlNamespace == null)
                ? (XmlElementWithLocation)oldElement.OwnerDocument.CreateElement(newElementName)
                : (XmlElementWithLocation)oldElement.OwnerDocument.CreateElement(newElementName, xmlNamespace);

            // Copy over all the attributes.
            foreach (XmlAttribute oldAttribute in oldElement.Attributes)
            {
                XmlAttribute newAttribute = (XmlAttribute)oldAttribute.CloneNode(true);
                newElement.SetAttributeNode(newAttribute);
            }

            // Move over all the child nodes - no need to change their identity
            while (oldElement.HasChildNodes)
            {
                // This conveniently updates FirstChild and HasChildNodes on oldElement.
                newElement.AppendChild(oldElement.FirstChild);
            }

               
            
                // Add the new element in the same place the old element was.
                oldElement.ParentNode?.ReplaceChild(newElement, oldElement);
            

            return newElement;
        }

        /// <summary>
        /// Verifies that a name is valid for the name of an item, property, or piece of metadata.
        /// If it isn't, throws an ArgumentException indicating the invalid character.
        /// </summary>
        /// <remarks>
        /// Note that our restrictions are more stringent than the XML Standard's restrictions.
        /// </remarks>
        /// <throws>ArgumentException</throws>
        /// <param name="name">name to validate</param>
        internal static void VerifyThrowArgumentValidElementName(string name)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, nameof(name));

            int firstInvalidCharLocation = LocateFirstInvalidElementNameCharacter(name);

            if (-1 != firstInvalidCharLocation)
            {
                ErrorUtilities.ThrowArgument("OM_NameInvalid", name, name[firstInvalidCharLocation]);
            }
        }

        /// <summary>
        /// Verifies that a name is valid for the name of an item, property, or piece of metadata.
        /// If it isn't, throws an InvalidProjectException indicating the invalid character.
        /// </summary>
        /// <remarks>
        /// Note that our restrictions are more stringent than the XML Standard's restrictions.
        /// </remarks>
        internal static void VerifyThrowProjectValidElementName(string name, IElementLocation location)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, nameof(name));
            int firstInvalidCharLocation = LocateFirstInvalidElementNameCharacter(name);

            if (-1 != firstInvalidCharLocation)
            {
                ProjectErrorUtilities.ThrowInvalidProject(location, "NameInvalid", name, name[firstInvalidCharLocation]);
            }
        }

        /// <summary>
        /// Verifies that a name is valid for the name of an item, property, or piece of metadata.
        /// If it isn't, throws an InvalidProjectException indicating the invalid character.
        /// </summary>
        /// <remarks>
        /// Note that our restrictions are more stringent than the XML Standard's restrictions.
        /// </remarks>
        internal static void VerifyThrowProjectValidElementName(XmlElementWithLocation element)
        {
            string name = element.Name;
            int firstInvalidCharLocation = LocateFirstInvalidElementNameCharacter(name);

            if (-1 != firstInvalidCharLocation)
            {
                ProjectErrorUtilities.ThrowInvalidProject(element.Location, "NameInvalid", name, name[firstInvalidCharLocation]);
            }
        }

        /// <summary>
        /// Indicates if the given name is valid as the name of an item, property or metadatum.
        /// </summary>
        /// <remarks>
        /// Note that our restrictions are more stringent than those of the XML Standard.
        /// </remarks>
        /// <param name="name"></param>
        /// <returns>true, if name is valid</returns>
        internal static bool IsValidElementName(string name)
        {
            return LocateFirstInvalidElementNameCharacter(name) == -1;
        }

        /// <summary>
        /// Finds the location of the first invalid character, if any, in the name of an 
        /// item, property, or piece of metadata. Returns the location of the first invalid character, or -1 if there are none. 
        /// Valid names must match this pattern:  [A-Za-z_][A-Za-z_0-9\-.]*
        /// Note, this is a subset of all possible valid XmlElement names: we use a subset because we also
        /// have to match this same set in our regular expressions, and allowing all valid XmlElement name
        /// characters in a regular expression would be impractical.
        /// </summary>
        /// <remarks>
        /// Note that our restrictions are more stringent than the XML Standard's restrictions.
        /// PERF: This method has to be as fast as possible, as it's called when any item, property, or piece
        /// of metadata is constructed.
        /// </remarks>
        internal static int LocateFirstInvalidElementNameCharacter(string name)
        {
            // Check the first character.
            // Try capital letters first.
            // Optimize slightly for success.
            if (!IsValidInitialElementNameCharacter(name[0]))
            {
                return 0;
            }

            // Check subsequent characters.
            // Try lower case letters first.
            // Optimize slightly for success.
            for (int i = 1; i < name.Length; i++)
            {
                if (!IsValidSubsequentElementNameCharacter(name[i]))
                {
                    return i;
                }
            }

            // If we got here, the name was valid.
            return -1;
        }

        internal static bool IsValidInitialElementNameCharacter(char c)
        {
            return (c >= 'A' && c <= 'Z') ||
                   (c >= 'a' && c <= 'z') ||
                   (c == '_');
        }

        internal static bool IsValidSubsequentElementNameCharacter(char c)
        {
            return (c >= 'A' && c <= 'Z') ||
                   (c >= 'a' && c <= 'z') ||
                   (c >= '0' && c <= '9') ||
                   (c == '_') ||
                   (c == '-');
        }
    }
}
