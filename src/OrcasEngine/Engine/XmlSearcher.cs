// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Collections;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class has static methods to determine line numbers and column numbers for given
    /// XML nodes.
    /// </summary>
    /// <owner>RGoel</owner>
    internal static class XmlSearcher
    {
        /// <summary>
        /// Given an XmlNode belonging to a document that lives on disk, this method determines
        /// the line/column number of that node in the document.  It does this by re-reading the
        /// document from disk and searching for the given node.
        /// </summary>
        /// <param name="xmlNodeToFind">Any XmlElement or XmlAttribute node (preferably in a document that still exists on disk).</param>
        /// <param name="foundLineNumber">(out) The line number where the specified node begins.</param>
        /// <param name="foundColumnNumber">(out) The column number where the specified node begins.</param>
        /// <returns>true if found, false otherwise.  Should not throw any exceptions.</returns>
        /// <owner>RGoel</owner>
        internal static bool GetLineColumnByNode
            (
            XmlNode xmlNodeToFind,
            out int foundLineNumber,
            out int foundColumnNumber
            )
        {
            // Initialize the output parameters.
            foundLineNumber = 0;
            foundColumnNumber = 0;

            if (xmlNodeToFind == null)
            {
                return false;
            }

            // Get the filename where this XML node came from.  Make sure it still
            // exists on disk.  If not, there's nothing we can do.  Sorry.
            string fileName = XmlUtilities.GetXmlNodeFile(xmlNodeToFind, String.Empty);
            if ((fileName.Length == 0) || (!File.Exists(fileName)))
            {
                return false;
            }

            // Next, we need to compute the "element number" and "attribute number" of
            // the given XmlNode in its original container document.  Element number is
            // simply a 1-based number identifying a particular XML element starting from
            // the beginning of the document, ignoring depth.  As you're walking the tree,
            // visiting each node in order, and recursing deeper whenever possible, the Nth 
            // element you visit has element number N.  Attribute number is simply the 
            // 1-based index of the attribute within the given Xml element.  An attribute
            // number of zero indicates that we're not searching for a particular attribute,
            // and all we care about is the element as a whole.
            int elementNumber;
            int attributeNumber;
            if (!GetElementAndAttributeNumber(xmlNodeToFind, out elementNumber, out attributeNumber))
            {
                return false;
            }

            // Now that we know what element/attribute number we're searching for, find
            // it in the Xml document on disk, and grab the line/column number.
            return GetLineColumnByNodeNumber(fileName, elementNumber, attributeNumber, 
                out foundLineNumber, out foundColumnNumber);
        }

        /// <summary>
        /// Determines the element number and attribute number of a given XmlAttribute node,
        /// or just the element number for a given XmlElement node.
        /// </summary>
        /// <param name="xmlNodeToFind">Any XmlElement or XmlAttribute within an XmlDocument.</param>
        /// <param name="elementNumber">(out) The element number of the given node.</param>
        /// <param name="attributeNumber">(out) If the given node was an XmlAttribute node, then the attribute number of that node, otherwise zero.</param>
        /// <returns>true if found, false otherwise.  Should not throw any exceptions.</returns>
        /// <owner>RGoel</owner>
        internal static bool GetElementAndAttributeNumber
            (
            XmlNode xmlNodeToFind,
            out int elementNumber,
            out int attributeNumber
            )
        {
            ErrorUtilities.VerifyThrow(xmlNodeToFind != null, "No Xml node!");

            // Initialize output parameters.
            elementNumber = 0;
            attributeNumber = 0;

            XmlNode elementToFind;

            // First determine the XmlNode in the main hierarchy to search for.  If the passed-in 
            // node is already an XmlElement or Text node, then we already have the node 
            // that we're searching for.  But if the passed-in node is an XmlAttribute, then
            // we want to search for the XmlElement that contains that attribute.
            // If the node is any other type, try the parent node. It's a better line number than no line number.
            if ((xmlNodeToFind.NodeType != XmlNodeType.Element) &&
                (xmlNodeToFind.NodeType != XmlNodeType.Text) &&
                (xmlNodeToFind.NodeType != XmlNodeType.Attribute))
            {
                if (xmlNodeToFind.ParentNode != null)
                {
                    xmlNodeToFind = xmlNodeToFind.ParentNode;
                }
            }

            if ((xmlNodeToFind.NodeType == XmlNodeType.Element) || (xmlNodeToFind.NodeType == XmlNodeType.Text))
            {
                elementToFind = xmlNodeToFind;
            }
            else if (xmlNodeToFind.NodeType == XmlNodeType.Attribute)
            {
                elementToFind = ((XmlAttribute) xmlNodeToFind).OwnerElement;
                ErrorUtilities.VerifyThrow(elementToFind != null, "How can an xml attribute not have a parent?");
            }
            else
            {
                // We don't support searching for anything other than XmlAttribute, XmlElement, or text node.
                return false;
            }

            // Figure out the element number for this particular XML element, by iteratively
            // visiting every single node in the XmlDocument in sequence.  Start with the 
            // root node which is the XmlDocument node.
            XmlNode xmlNode = xmlNodeToFind.OwnerDocument;
            while (true)
            {
                // If the current node is an XmlElement or text node, bump up our variable which tracks the
                // number of XmlElements visited so far.
                if ((xmlNode.NodeType == XmlNodeType.Element) || (xmlNode.NodeType == XmlNodeType.Text))
                {
                    elementNumber++;

                    // If the current XmlElement node is actually the one the caller wanted
                    // us to search for, then we've found the element number.  Yippee.
                    if (xmlNode == elementToFind)
                    {
                        break;
                    }
                }

                // The rest of this is all about moving to the next node in the tree.
                if (xmlNode.HasChildNodes)
                {
                    // If the current node has any children, then the next node to visit
                    // is the first child.
                    xmlNode = xmlNode.FirstChild;
                }
                else
                {
                    // Current node has no children.  So we basically want its next
                    // sibling.  Unless of course it has no more siblings, in which
                    // case we want its parent's next sibling.  Unless of course its
                    // parent doesn't have any more siblings, in which case we want
                    // its parent's parent's sibling.  Etc, etc.
                    while ((xmlNode != null) && (xmlNode.NextSibling == null))
                    {
                        xmlNode = xmlNode.ParentNode;
                    }

                    if (xmlNode == null)
                    {
                        // Oops, we reached the end of the document, so bail.
                        break;
                    }
                    else
                    {
                        xmlNode = xmlNode.NextSibling;
                    }
                }
            }

            if (xmlNode == null)
            {
                // We visited every XmlElement in the document without finding the 
                // specific XmlElement we were supposed to.  Oh well, too bad.
                elementNumber = 0;
                return false;
            }

            // If we were originally asked to actually find an XmlAttribute within
            // an XmlElement, now comes Part 2.  We've already found the correct
            // element, so now we just need to iterate through the attributes within
            // the element in order until we find the desired one.
            if (xmlNodeToFind.NodeType == XmlNodeType.Attribute)
            {
                bool foundAttribute = false;

                XmlAttribute xmlAttributeToFind = xmlNodeToFind as XmlAttribute;
                foreach (XmlAttribute xmlAttribute in ((XmlElement)elementToFind).Attributes)
                {
                    attributeNumber++;

                    if (xmlAttribute == xmlAttributeToFind)
                    {
                        foundAttribute = true;
                        break;
                    }
                }

                if (!foundAttribute)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Read through the entire XML of a given project file, searching for the element/attribute 
        /// specified by element number and attribute number.  Return the line number and column 
        /// number where it was found.
        /// </summary>
        /// <param name="projectFile">Path to project file on disk.</param>
        /// <param name="xmlElementNumberToSearchFor">Which Xml element to search for.</param>
        /// <param name="xmlAttributeNumberToSearchFor">
        ///     Which Xml attribute within the above Xml element to search for.  Pass in zero
        ///     if you are searching for the Element as a whole and not a particular attribute.
        /// </param>
        /// <param name="foundLineNumber">(out) The line number where the given element/attribute begins.</param>
        /// <param name="foundColumnNumber">The column number where the given element/attribute begins.</param>
        /// <returns>true if found, false otherwise.  Should not throw any exceptions.</returns>
        /// <owner>RGoel</owner>
        internal static bool GetLineColumnByNodeNumber
            (
            string projectFile,
            int xmlElementNumberToSearchFor,
            int xmlAttributeNumberToSearchFor,
            out int foundLineNumber,
            out int foundColumnNumber
            )
        {
            ErrorUtilities.VerifyThrow(xmlElementNumberToSearchFor != 0, "No element to search for!");
            ErrorUtilities.VerifyThrow((projectFile != null) && (projectFile.Length != 0), "No project file!");

            // Initialize output parameters.
            foundLineNumber = 0;
            foundColumnNumber = 0;

            try
            {
                // We're going to need to re-read the file from disk in order to find
                // the line/column number of the specified node.
                using (XmlTextReader reader = new XmlTextReader(projectFile))
                {
                    reader.DtdProcessing = DtdProcessing.Ignore;
                    int currentXmlElementNumber = 0;

                    // While we haven't reached the end of the file, and we haven't found the 
                    // specified node ...
                    while (reader.Read() && (foundColumnNumber == 0) && (foundLineNumber == 0))
                    {
                        // Read to the next node.  If it is an XML element or Xml text node, then ...
                        if ((reader.NodeType == XmlNodeType.Element) || (reader.NodeType == XmlNodeType.Text))
                        {
                            // Bump up our current XML element count.
                            currentXmlElementNumber++;

                            // Check to see if this XML element is the one we've been searching for,
                            // based on if the numbers match.
                            if (currentXmlElementNumber == xmlElementNumberToSearchFor)
                            {
                                // We've found the desired XML element.  If the caller didn't care
                                // for a particular attribute, then we're done.  Return the current
                                // position of the XmlTextReader.
                                if (0 == xmlAttributeNumberToSearchFor)
                                {
                                    foundLineNumber = reader.LineNumber;
                                    foundColumnNumber = reader.LinePosition;

                                    if (reader.NodeType == XmlNodeType.Element) 
                                    {
                                        // Do a minus-one here, because the XmlTextReader points us at the first
                                        // letter of the tag name, whereas we would prefer to point at the opening
                                        // left-angle-bracket.  (Whitespace between the left-angle-bracket and
                                        // the tag name is not allowed in XML, so this is safe.)
                                        foundColumnNumber = foundColumnNumber - 1;
                                    }
                                }
                                else if (reader.MoveToFirstAttribute()) 
                                {
                                    // Caller wants a particular attribute within the element,
                                    // and the element does have 1 or more attributes.  So let's 
                                    // try to find the right one.
                                    int currentXmlAttributeNumber = 0;

                                    // Loop through all the XML attributes on the current element.
                                    do 
                                    {
                                        // Bump the current attribute number and check to see if this
                                        // is the one.
                                        currentXmlAttributeNumber++;

                                        if (currentXmlAttributeNumber == xmlAttributeNumberToSearchFor)
                                        {
                                            // We found the desired attribute.  Return the current
                                            // position of the XmlTextReader.
                                            foundLineNumber = reader.LineNumber;
                                            foundColumnNumber = reader.LinePosition;
                                        }

                                    } while (reader.MoveToNextAttribute() && (foundColumnNumber == 0) && (foundLineNumber == 0));
                                }
                            }
                        }
                    }
                }
            }
            catch (XmlException) 
            {
                // Eat the exception.  If anything fails, we simply don't surface the line/column number.
            }
            catch (IOException)
            {
                // Eat the exception.  If anything fails, we simply don't surface the line/column number.
            }
            catch (UnauthorizedAccessException)
            {
                // Eat the exception.  If anything fails, we simply don't surface the line/column number.
            }

            return ((foundColumnNumber != 0) && (foundLineNumber != 0));
        }
    }
}
