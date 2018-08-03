// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Xml;
using System.Diagnostics;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Derivation of XmlElement to implement IXmlLineInfo
    /// </summary>
    /// <remarks>
    /// It would be nice to add some helper overloads of base class members that
    /// downcast their return values to XmlElement/AttributeWithLocation. However
    /// C# doesn't currently allow covariance in method overloading, only on delegates.
    /// The caller must bravely downcast.
    /// </remarks>
    internal class XmlElementWithLocation : XmlElement, IXmlLineInfo
    {
        /// <summary>
        /// Line, column, file information
        /// </summary>
        private ElementLocation _elementLocation;

        /// <summary>
        /// Constructor without location information
        /// </summary>
        public XmlElementWithLocation(string prefix, string localName, string namespaceURI, XmlDocumentWithLocation document)
            : this(prefix, localName, namespaceURI, document, 0, 0)
        {
        }

        /// <summary>
        /// Constructor with location information
        /// </summary>
        public XmlElementWithLocation(string prefix, string localName, string namespaceURI, XmlDocumentWithLocation document, int lineNumber, int columnNumber)
            : base(prefix, localName, namespaceURI, document)
        {
            // Subtract one, just to give the same value as the old code did.
            // In the past we pointed to the column of the open angle bracket whereas the XmlTextReader points to the first character of the element name.
            // In well formed XML these are always adjacent on the same line, so it's safe to subtract one.
            // If we're loading from a stream it's zero, so don't subtract one.
            XmlDocumentWithLocation documentWithLocation = (XmlDocumentWithLocation)document;

            int adjustedColumn = (columnNumber == 0) ? columnNumber : columnNumber - 1;
            _elementLocation = ElementLocation.Create(documentWithLocation.FullPath, lineNumber, adjustedColumn);
        }

        /// <summary>
        /// Returns the line number if available, else 0.
        /// IXmlLineInfo member.
        /// </summary>
        public int LineNumber
        {
            [DebuggerStepThrough]
            get
            { return Location.Line; }
        }

        /// <summary>
        /// Returns the column number if available, else 0.
        /// IXmlLineInfo member.
        /// </summary>
        public int LinePosition
        {
            [DebuggerStepThrough]
            get
            { return Location.Column; }
        }

        /// <summary>
        /// Provides an ElementLocation for this element, using the path to the file containing
        /// this element as the project file entry.
        /// Element location may be incorrect, if it was not loaded from disk.
        /// Does not return null.
        /// </summary>
        /// <remarks>
        /// Should have at least the file name if the containing project has been given a file name,
        /// even if it wasn't loaded from disk, or has been edited since. That's because we set that
        /// path on our XmlDocumentWithLocation wrapper class.
        /// </remarks>
        internal ElementLocation Location
        {
            get
            {
                // Caching the element location object saves significant memory
                XmlDocumentWithLocation ownerDocumentWithLocation = (XmlDocumentWithLocation)OwnerDocument;
                if (!String.Equals(_elementLocation.File, ownerDocumentWithLocation.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    _elementLocation = ElementLocation.Create(ownerDocumentWithLocation.FullPath, _elementLocation.Line, _elementLocation.Column);
                }

                return _elementLocation;
            }
        }

        /// <summary>
        /// Whether location is available.
        /// IXmlLineInfo member.
        /// </summary>
        public bool HasLineInfo()
        {
            return Location.Line != 0;
        }

        /// <summary>
        /// Returns the XmlAttribute with the specified name or null if a matching attribute was not found.
        /// </summary>
        public XmlAttributeWithLocation GetAttributeWithLocation(string name)
        {
            XmlAttribute attribute = GetAttributeNode(name);

            if (attribute == null)
            {
                return null;
            }

            return (XmlAttributeWithLocation)attribute;
        }

        /// <summary>
        /// Overridden to convert the display of the element from open form (separate open and closed tags) to closed form 
        /// (single closed tag) if the last child is being removed. This is simply for tidiness of the project file.
        /// For example, removing the only piece of metadata from an item will leave behind one tag instead of two.
        /// </summary>
        public override XmlNode RemoveChild(XmlNode oldChild)
        {
            XmlNode result = base.RemoveChild(oldChild);

            if (!HasChildNodes)
            {
                IsEmpty = true;
            }

            return result;
        }

        /// <summary>
        /// Gets the location of any attribute on this element with the specified name.
        /// If there is no such attribute, returns null.
        /// </summary>
        internal ElementLocation GetAttributeLocation(string name)
        {
            XmlAttributeWithLocation attributeWithLocation = GetAttributeWithLocation(name);

            return (attributeWithLocation != null) ? attributeWithLocation.Location : null;
        }
    }
}