// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Xml;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Internal
{
    partial class ProjectXmlUtilities
    {
        // Iterating an element's nodes allocates an non-trivial amount of data in certain
        // large solutions with lots of targets, so we have our own struct-based iterator
        // that avoids unneeded GC pressure.
        //
        // Deliberately not implement IEnumerable/IEnumerator to avoid accidental boxing
        internal struct XmlElementChildIterator
        {
            private readonly XmlElementWithLocation _element;
            private readonly bool _throwForInvalidNodeTypes;
            private XmlElementWithLocation _current;
            private bool _isFirst;

            internal XmlElementChildIterator(XmlElementWithLocation element, bool throwForInvalidNodeTypes)
            {
                _element = element;
                _throwForInvalidNodeTypes = throwForInvalidNodeTypes;
                _current = null;
                _isFirst = true;
            }

            public bool MoveNext()
            {
                if (_isFirst)
                {
                    _isFirst = false;
                    _current = GetNextNode(_element.FirstChild);
                }
                else if (_current != null)
                {
                    _current = GetNextNode(_current.NextSibling);
                }

                return _current != null;
            }

            public XmlElementChildIterator GetEnumerator()
            {
                return this;
            }

            public XmlElementWithLocation Current
            {
                get
                {
                    if (_isFirst || _current == null)
                        throw new InvalidOperationException();

                    return _current;
                }
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            private XmlElementWithLocation GetNextNode(XmlNode child)
            {
                while (child != null)
                {
                    switch (child.NodeType)
                    {
                        case XmlNodeType.Comment:
                        case XmlNodeType.Whitespace:
                            // These are legal, and ignored
                            break;

                        case XmlNodeType.Element:
                            return (XmlElementWithLocation)child;

                        default:
                            if (child.NodeType == XmlNodeType.Text && String.IsNullOrWhiteSpace(child.InnerText))
                            {
                                // If the text is greather than 4k and only contains whitespace, the XML reader will assume it's a text node
                                // instead of ignoring it.  Our call to String.IsNullOrWhiteSpace() can be a little slow if the text is
                                // large but this should be extremely rare.
                                break;
                            }
                            if (_throwForInvalidNodeTypes)
                            {
                                ThrowProjectInvalidChildElement(child.Name, _element.Name, _element.Location);
                            }
                            break;
                    }

                    child = child.NextSibling;
                }

                // We're done
                return null;
            }
        }
    }
}
