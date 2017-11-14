// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Xml;
using Microsoft.Build.Construction;
using System.Diagnostics;

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

            // -1: Not yet called GetEnumerator
            //  0: First element
            //  1: After first element
            private int _state;

            internal XmlElementChildIterator(XmlElementWithLocation element, bool throwForInvalidNodeTypes)
            {
                Debug.Assert(element != null);

                _element = element;
                _throwForInvalidNodeTypes = throwForInvalidNodeTypes;
                _current = null;
                _state = -1;
            }

            public bool MoveNext()
            {
                Debug.Assert(_state > -1);

                if (_state == 0)
                {
                    _state = 1;
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
                Debug.Assert(_state == -1);

                _state = 0;
                return this;
            }

            public XmlElementWithLocation Current
            {
                get
                {
                    Debug.Assert(_state == 1 && _current != null);

                    return _current;
                }
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
