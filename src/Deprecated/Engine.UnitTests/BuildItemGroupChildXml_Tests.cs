// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Xml;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class BuildItemGroupChildXml_Tests
    {
        [Test]
        public void ParseBasicRemoveOperation()
        {
            XmlElement xml = CreateBasicRemoveElement();
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemRemove);

            Assertion.AssertEquals("i1", child.Remove);
        }

        [Test]
        public void ExpectAnyGetModify()
        {
            XmlElement xml = XmlTestUtilities.CreateBasicElement("i");
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.Any);

            Assertion.AssertEquals(ChildType.BuildItemModify, child.ChildType);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpectAddGetRemove()
        {
            XmlElement xml = CreateBasicRemoveElement();
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemAdd);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpectRemoveGetAdd()
        {
            XmlElement xml = XmlTestUtilities.CreateBasicElementWithOneAttribute("i", "Include", "i1");;
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemRemove);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpectModifyGetAdd()
        {
            XmlElement xml = XmlTestUtilities.CreateBasicElementWithOneAttribute("i", "Include", "i1"); ;
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemModify);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpectModifyGetRemove()
        {
            XmlElement xml = XmlTestUtilities.CreateBasicElementWithOneAttribute("i", "Remove", "i1"); ;
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemModify);
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void ExpectInvalid()
        {
            XmlElement xml = XmlTestUtilities.CreateBasicElementWithOneAttribute("i", "Include", "i1");
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.Invalid);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidIncludeAndRemoveTogether()
        {
            XmlElement xml = CreateBasicRemoveElement();
            xml.SetAttribute("Include", "i2");
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemRemove);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidExcludeWithoutInclude()
        {
            XmlElement xml = XmlTestUtilities.CreateBasicElementWithOneAttribute("i", "Exclude", "i1");
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemAdd);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidRemoveWithSomeMetadataChildren()
        {
            XmlElement xml = CreateBasicRemoveElement();
            XmlElement child1 = xml.OwnerDocument.CreateElement("m", XMakeAttributes.defaultXmlNamespace);
            child1.InnerText = "m1";
            xml.AppendChild(child1);
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemRemove);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidlyNamedMetadata()
        {
            XmlElement xml = XmlTestUtilities.CreateBasicElementWithOneAttribute("i", "Include", "i1");
            XmlElement child1 = xml.OwnerDocument.CreateElement("m", XMakeAttributes.defaultXmlNamespace);
            XmlElement child2 = xml.OwnerDocument.CreateElement("Filename", XMakeAttributes.defaultXmlNamespace);
            xml.AppendChild(child1);
            xml.AppendChild(child2);
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemAdd);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void RemoveAttributeMissing()
        {
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("i", XMakeAttributes.defaultXmlNamespace);
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(element, ChildType.BuildItemRemove);
        }

        [Test]
        public void ParseModify()
        {
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("i", XMakeAttributes.defaultXmlNamespace);
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(element, ChildType.BuildItemModify);

            Assertion.AssertEquals(ChildType.BuildItemModify, child.ChildType);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidExcludeAndRemoveTogether()
        {
            XmlElement xml = CreateBasicRemoveElement();
            xml.SetAttribute("Exclude", "i2");
            BuildItemGroupChildXml child = new BuildItemGroupChildXml(xml, ChildType.BuildItemRemove);
        }

        private static XmlElement CreateBasicRemoveElement()
        {
            return XmlTestUtilities.CreateBasicElementWithOneAttribute("i", "Remove", "i1");
        }
    }
}
