// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text.RegularExpressions;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class contains utility methods for XML manipulation.
    /// </summary>
    /// <owner>SumedhK</owner>
    static internal class XmlUtilities
    {
        /// <summary>
        /// This method renames an XML element.  Well, actually you can't directly
        /// rename an XML element using the DOM, so what you have to do is create
        /// a brand new XML element with the new name, and copy over all the attributes
        /// and children.  This method returns the new XML element object.
        /// </summary>
        /// <param name="oldElement"></param>
        /// <param name="newElementName"></param>
        /// <param name="xmlNamespace">Can be null if global namespace.</param>
        /// <returns>new/renamed element</returns>
        /// <owner>RGoel</owner>
        internal static XmlElement RenameXmlElement(XmlElement oldElement, string newElementName, string xmlNamespace)
        {
            XmlElement newElement = (xmlNamespace == null)
                ? oldElement.OwnerDocument.CreateElement(newElementName)
                : oldElement.OwnerDocument.CreateElement(newElementName, xmlNamespace);

            // Copy over all the attributes.
            foreach (XmlAttribute oldAttribute in oldElement.Attributes)
            {
                XmlAttribute newAttribute = (XmlAttribute)oldAttribute.CloneNode(true);
                newElement.SetAttributeNode(newAttribute);
            }

            // Copy over all the child nodes.
            foreach (XmlNode oldChildNode in oldElement.ChildNodes)
            {
                XmlNode newChildNode = oldChildNode.CloneNode(true);
                newElement.AppendChild(newChildNode);
            }

               
            
                // Add the new element in the same place the old element was.
                oldElement.ParentNode?.ReplaceChild(newElement, oldElement);
            

            return newElement;
        }

        /// <summary>
        /// Retrieves the file that the given XML node was defined in. If the XML node is purely in-memory, it may not have a file
        /// associated with it, in which case the default file is returned.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="node"></param>
        /// <param name="defaultFile">Can be empty string.</param>
        /// <returns>The path to the XML node's file, or the default file.</returns>
        internal static string GetXmlNodeFile(XmlNode node, string defaultFile)
        {
            ErrorUtilities.VerifyThrow(node != null, "Need XML node.");
            ErrorUtilities.VerifyThrow(defaultFile != null, "Must specify the default file to use.");

            string file = defaultFile;

            // NOTE: the XML node may not have a filename if it's purely an in-memory node
            if (!string.IsNullOrEmpty(node.OwnerDocument.BaseURI))
            {
                file = new Uri(node.OwnerDocument.BaseURI).LocalPath;
            }

            return file;
        }

        /// <summary>
        /// An XML document can have many root nodes, but usually we want the single root 
        /// element. Callers can test each root node in turn with this method, until it returns
        /// true.
        /// </summary>
        /// <param name="node">Candidate root node</param>
        /// <returns>true if node is the root element</returns>
        /// <owner>danmose</owner>
        internal static bool IsXmlRootElement(XmlNode node)
        {
            // "A Document node can have the following child node types: XmlDeclaration,
            // Element (maximum of one), ProcessingInstruction, Comment, and DocumentType."
            return
                   (node.NodeType != XmlNodeType.Comment) &&
                   (node.NodeType != XmlNodeType.Whitespace) &&
                   (node.NodeType != XmlNodeType.XmlDeclaration) &&
                   (node.NodeType != XmlNodeType.ProcessingInstruction) &&
                   (node.NodeType != XmlNodeType.DocumentType)
                   ;
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
        /// <owner>danmose</owner>
        internal static void VerifyThrowValidElementName(string name)
        {
            int firstInvalidCharLocation = LocateFirstInvalidElementNameCharacter(name);

            if (-1 != firstInvalidCharLocation)
            {
                ErrorUtilities.VerifyThrowArgument(false, "NameInvalid", name, name[firstInvalidCharLocation]);
            }
        }

        /// <summary>
        /// Verifies that a name is valid for the name of an item, property, or piece of metadata.
        /// If it isn't, throws an InvalidProjectException indicating the invalid character.
        /// </summary>
        /// <remarks>
        /// Note that our restrictions are more stringent than the XML Standard's restrictions.
        /// </remarks>
        internal static void VerifyThrowProjectValidElementName(XmlElement element)
        {
            string name = element.Name;
            int firstInvalidCharLocation = LocateFirstInvalidElementNameCharacter(name);

            if (-1 != firstInvalidCharLocation)
            {
                ProjectErrorUtilities.ThrowInvalidProject(element, "NameInvalid", name, name[firstInvalidCharLocation]);
            }
        }

        /// <summary>
        /// Indicates if the given name is valid as the name of an item, property or metadatum.
        /// </summary>
        /// <remarks>
        /// Note that our restrictions are more stringent than those of the XML Standard.
        /// </remarks>
        /// <owner>SumedhK</owner>
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

        /// <summary>
        /// Load the xml file using XMLTextReader and locate the element and attribute specified and then 
        /// return the value. This is a quick way to peek at the xml file whithout having the go through 
        /// the XMLDocument (MSDN article (Chapter 9 - Improving XML Performance)).
        /// </summary>
        internal static string GetAttributeValueForElementFromFile
            (
            string projectFileName,
            string elementName,
            string attributeName
            )
        {
            string attributeValue = null;

            try
            {
                using (XmlTextReader xmlReader = new XmlTextReader(projectFileName))
                {
                    xmlReader.DtdProcessing = DtdProcessing.Ignore;
                    while (xmlReader.Read())
                    {
                        if (xmlReader.NodeType == XmlNodeType.Element)
                        {
                            if (String.Equals(xmlReader.Name, elementName, StringComparison.OrdinalIgnoreCase))
                            {
                                if (xmlReader.HasAttributes)
                                {
                                    for (int i = 0; i < xmlReader.AttributeCount; i++)
                                    {
                                        xmlReader.MoveToAttribute(i);
                                        if (String.Equals(xmlReader.Name, attributeName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            attributeValue = xmlReader.Value;
                                            break;
                                        }
                                    }
                                }
                                // if we have already located the element then we are done
                                break;
                            }
                        }
                    }
                }
            }
            catch(XmlException)
            {
                // Ignore any XML exceptions as it will be caught later on
            }
            catch (System.IO.IOException)
            {
                // Ignore any IO exceptions as it will be caught later on
            }

            return attributeValue;
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
