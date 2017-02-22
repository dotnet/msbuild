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
    internal static class XmlTestUtilities
    {
        internal static XmlElement CreateBasicElementWithOneAttribute(string elementName, string attributeName, string attributeValue)
        {
            XmlElement element = CreateBasicElement(elementName);
            AddAttribute(element, attributeName, attributeValue);
            return element;
        }

        internal static void AddAttribute(XmlNode element, string attributeName, string attributeValue)
        {
            XmlAttribute attribute = element.OwnerDocument.CreateAttribute(attributeName);
            element.Attributes.Append(attribute);
            attribute.Value = attributeValue;
        }

        internal static XmlElement AddChildElement(XmlNode parentElement, string childName)
        {
            XmlElement element = parentElement.OwnerDocument.CreateElement(childName, XMakeAttributes.defaultXmlNamespace);
            parentElement.AppendChild(element);
            return element;
        }

        internal static XmlElement AddChildElementWithInnerText(XmlNode parentElement, string childName, string innerText)
        {
            XmlElement element = AddChildElement(parentElement, childName);
            element.InnerText = innerText;
            return element;
        }

        internal static XmlElement CreateBasicElement(string elementName)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement(elementName, XMakeAttributes.defaultXmlNamespace);
            return element;
        }
    }
}
