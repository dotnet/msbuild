// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Diagnostics;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Derivation of XmlAttribute to implement IXmlLineInfo
    /// </summary>
    internal class XmlAttributeWithLocation : XmlAttribute, IXmlLineInfo
    {
        /// <summary>
        /// Line, column, file information
        /// </summary>
        private ElementLocation _elementLocation;

        /// <summary>
        /// Constructor without location information
        /// </summary>
        public XmlAttributeWithLocation(string prefix, string localName, string namespaceURI, XmlDocument document)
            : this(prefix, localName, namespaceURI, document, 0, 0)
        {
        }

        /// <summary>
        /// Constructor with location information
        /// </summary>
        public XmlAttributeWithLocation(string prefix, string localName, string namespaceURI, XmlDocument document, int lineNumber, int columnNumber)
            : base(prefix, localName, namespaceURI, document)
        {
            XmlDocumentWithLocation documentWithLocation = (XmlDocumentWithLocation)document;

            _elementLocation = ElementLocation.Create(documentWithLocation.FullPath, lineNumber, columnNumber);
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
        /// Provides an ElementLocation for this attribute.
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
    }
}