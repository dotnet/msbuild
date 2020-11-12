// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using System.Xml;
using System.IO;
using Xunit;

namespace Microsoft.Build.UnitTests.Construction
{
    public class XmlReaderWithoutLocation_Tests
    {
        private class XmlReaderNoIXmlLineInfo : XmlReader
        {
            private XmlReader _wrappedReader;

            public XmlReaderNoIXmlLineInfo(XmlReader wrappedReader)
            {
                _wrappedReader = wrappedReader;
            }

            public override int AttributeCount
            {
                get { return _wrappedReader.AttributeCount; }
            }

            public override string BaseURI
            {
                get { return _wrappedReader.BaseURI; }
            }

            protected override void Dispose(bool disposing)
            {
                _wrappedReader.Dispose();
            }

            public override int Depth
            {
                get { return _wrappedReader.Depth; }
            }

            public override bool EOF
            {
                get { return _wrappedReader.EOF; }
            }

            public override string GetAttribute(int i)
            {
                return _wrappedReader.GetAttribute(i);
            }

            public override string GetAttribute(string name, string namespaceURI)
            {
                return _wrappedReader.GetAttribute(name, namespaceURI);
            }

            public override string GetAttribute(string name)
            {
                return _wrappedReader.GetAttribute(name);
            }

            public override bool HasValue
            {
                get { return _wrappedReader.HasValue; }
            }

            public override bool IsEmptyElement
            {
                get { return _wrappedReader.IsEmptyElement; }
            }

            public override string LocalName
            {
                get { return _wrappedReader.LocalName; }
            }

            public override string LookupNamespace(string prefix)
            {
                return _wrappedReader.LookupNamespace(prefix);
            }

            public override bool MoveToAttribute(string name, string ns)
            {
                return _wrappedReader.MoveToAttribute(name, ns);
            }

            public override bool MoveToAttribute(string name)
            {
                return _wrappedReader.MoveToAttribute(name);
            }

            public override bool MoveToElement()
            {
                return _wrappedReader.MoveToElement();
            }

            public override bool MoveToFirstAttribute()
            {
                return _wrappedReader.MoveToFirstAttribute();
            }

            public override bool MoveToNextAttribute()
            {
                return _wrappedReader.MoveToNextAttribute();
            }

            public override XmlNameTable NameTable
            {
                get { return _wrappedReader.NameTable; }
            }

            public override string NamespaceURI
            {
                get { return _wrappedReader.NamespaceURI; }
            }

            public override XmlNodeType NodeType
            {
                get { return _wrappedReader.NodeType; }
            }

            public override string Prefix
            {
                get { return _wrappedReader.Prefix; }
            }

            public override bool Read()
            {
                return _wrappedReader.Read();
            }

            public override bool ReadAttributeValue()
            {
                return _wrappedReader.ReadAttributeValue();
            }

            public override ReadState ReadState
            {
                get { return _wrappedReader.ReadState; }
            }

            public override void ResolveEntity()
            {
                _wrappedReader.ResolveEntity();
            }

            public override string Value
            {
                get { return _wrappedReader.Value; }
            }
        }

        [Fact]
        public void CreateProjectWithoutLineInfo()
        {
            XmlReader reader = XmlReader.Create(new StringReader
                (
                @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                      <Target Name='foo'/>
                  </Project>"
                ));
            XmlReader noLineInfoReader = new XmlReaderNoIXmlLineInfo(reader);
            Project project = new Project(noLineInfoReader);
            Assert.Single(project.Targets);
        }
    }
}
