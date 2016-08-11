// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Reflection;
using System.Collections;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

using NUnit.Framework;

using Microsoft.Build.Framework;
using BuildEngine = Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class UtilitiesTest
    {
        private static XmlAttribute fakeAttribute = (new XmlDocument()).CreateAttribute("foo");

        /// <summary>
        /// Tests our "Condition" parser's ability to extract certain property values
        /// out, for the purposes of VS populating the "Configuration" and "Platform" 
        /// dropdown boxes.  This one tests the following expression:
        /// 
        ///     '$(Configuration)' == 'Debug'
        /// 
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GatherReferencedPropertyNames1 ()
        {
            Hashtable conditionedProperties = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildEngine.Utilities.GatherReferencedPropertyNames(" '$(Configuration)' == 'Debug' ", fakeAttribute,
                new Expander(new BuildPropertyGroup ()), conditionedProperties);

            StringCollection configurations = (StringCollection) conditionedProperties["CONFIGURATION"];

            Assertion.AssertEquals(1, configurations.Count);
            Assertion.AssertEquals("Debug", configurations[0]);
        }

        /// <summary>
        /// Tests our "Condition" parser's ability to extract certain property values
        /// out, for the purposes of VS populating the "Configuration" and "Platform" 
        /// dropdown boxes.  This one tests the following expression:
        /// 
        ///     'Debug' != '$(Configuration)'
        /// 
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GatherReferencedPropertyNames2 ()
        {
            Hashtable conditionedProperties = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildEngine.Utilities.GatherReferencedPropertyNames(" 'Debug' != '$(Configuration)' ", fakeAttribute,
                new Expander(new BuildPropertyGroup ()), conditionedProperties);

            StringCollection configurations = (StringCollection) conditionedProperties["CONFIGURATION"];

            Assertion.AssertEquals(1, configurations.Count);
            Assertion.AssertEquals("Debug", configurations[0]);
        }
        
        /// <summary>
        /// Tests our "Condition" parser's ability to extract certain property values
        /// out, for the purposes of VS populating the "Configuration" and "Platform" 
        /// dropdown boxes.  This one tests the following expression:
        /// 
        ///     '$(Configuration)|$(Platform)' != 'Debug|x86'
        /// 
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GatherReferencedPropertyNames3 ()
        {
            Hashtable conditionedProperties = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildEngine.Utilities.GatherReferencedPropertyNames(" '$(Configuration)|$(Platform)' != 'Debug|x86' ", fakeAttribute,
                new Expander(new BuildPropertyGroup ()), conditionedProperties);

            StringCollection configurations = (StringCollection) conditionedProperties["CONFIGURATION"];
            StringCollection platforms = (StringCollection) conditionedProperties["PLATFORM"];

            Assertion.AssertEquals(1, configurations.Count);
            Assertion.AssertEquals(1, platforms.Count);
            Assertion.AssertEquals("Debug", configurations[0]);
            Assertion.AssertEquals("x86", platforms[0]);
        }
    
        /// <summary>
        /// Tests our "Condition" parser's ability to extract certain property values
        /// out, for the purposes of VS populating the "Configuration" and "Platform" 
        /// dropdown boxes.  This one tests the following expression:
        /// 
        ///     'Debug|x86' == '$(Configuration)|$(Platform)'
        /// 
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GatherReferencedPropertyNames4 ()
        {
            Hashtable conditionedProperties = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildEngine.Utilities.GatherReferencedPropertyNames(" 'Debug|x86' == '$(Configuration)|$(Platform)' ", fakeAttribute,
                new Expander(new BuildPropertyGroup ()), conditionedProperties);

            StringCollection configurations = (StringCollection) conditionedProperties["CONFIGURATION"];
            StringCollection platforms = (StringCollection) conditionedProperties["PLATFORM"];

            Assertion.AssertEquals(1, configurations.Count);
            Assertion.AssertEquals(1, platforms.Count);
            Assertion.AssertEquals("Debug", configurations[0]);
            Assertion.AssertEquals("x86", platforms[0]);
        }

        /// <summary>
        /// Tests our "Condition" parser's ability to extract certain property values
        /// out, for the purposes of VS populating the "Configuration" and "Platform" 
        /// dropdown boxes.  This one tests the following expression:
        /// 
        ///     '$(Configuration)|$(Platform)|$(Machine)' == 'Debug|x86|RGOEL3'
        /// 
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GatherReferencedPropertyNames5 ()
        {
            Hashtable conditionedProperties = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildEngine.Utilities.GatherReferencedPropertyNames(" '$(Configuration)|$(Platform)|$(Machine)' == 'Debug|x86|RGOEL3' ", fakeAttribute,
                new Expander(new BuildPropertyGroup ()), conditionedProperties);

            StringCollection configurations = (StringCollection) conditionedProperties["CONFIGURATION"];
            StringCollection platforms = (StringCollection) conditionedProperties["PLATFORM"];
            StringCollection machines = (StringCollection) conditionedProperties["MACHINE"];

            Assertion.AssertEquals(1, configurations.Count);
            Assertion.AssertEquals(1, platforms.Count);
            Assertion.AssertEquals(1, machines.Count);
            Assertion.AssertEquals("Debug", configurations[0]);
            Assertion.AssertEquals("x86", platforms[0]);
            Assertion.AssertEquals("RGOEL3", machines[0]);
        }

        /// <summary>
        /// Tests our "Condition" parser's ability to extract certain property values
        /// out, for the purposes of VS populating the "Configuration" and "Platform" 
        /// dropdown boxes.  This one tests the following expression:
        /// 
        ///     '$(Configuration)|$(Platform)|$(Machine)' == 'Debug'
        /// 
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GatherReferencedPropertyNames6 ()
        {
            Hashtable conditionedProperties = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildEngine.Utilities.GatherReferencedPropertyNames(" '$(Configuration)|$(Platform)|$(Machine)' == 'Debug' ", fakeAttribute,
                new Expander(new BuildPropertyGroup ()), conditionedProperties);

            StringCollection configurations = (StringCollection) conditionedProperties["CONFIGURATION"];

            Assertion.AssertEquals(1, configurations.Count);
            Assertion.AssertNull(conditionedProperties["PLATFORM"]);
            Assertion.AssertNull(conditionedProperties["MACHINE"]);
            Assertion.AssertEquals("Debug", configurations[0]);
        }

        /// <summary>
        /// Tests our "Condition" parser's ability to extract certain property values
        /// out, for the purposes of VS populating the "Configuration" and "Platform" 
        /// dropdown boxes.  This one tests the following expression:
        /// 
        ///     '$(Configuration)' == 'Debug|x86|RGOEL3'
        /// 
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GatherReferencedPropertyNames7 ()
        {
            Hashtable conditionedProperties = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildEngine.Utilities.GatherReferencedPropertyNames(" '$(Configuration)' == 'Debug|x86|RGOEL3' ", fakeAttribute,
                new Expander(new BuildPropertyGroup ()), conditionedProperties);

            StringCollection configurations = (StringCollection) conditionedProperties["CONFIGURATION"];

            Assertion.AssertEquals(1, configurations.Count);
            Assertion.AssertNull(conditionedProperties["PLATFORM"]);
            Assertion.AssertNull(conditionedProperties["MACHINE"]);
            Assertion.AssertEquals("Debug|x86|RGOEL3", configurations[0]);
        }

        /// <summary>
        /// Verify Condition is illegal on ProjectExtensions tag
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void IllegalConditionOnProjectExtensions()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                    <ProjectExtensions Condition=`'a'=='b'`/>
                    <Import Project=`$(MSBuildBinPath)\\Microsoft.CSharp.Targets` />
                </Project>
            ");
        }

        /// <summary>
        /// Verify ProjectExtensions cannot exist twice
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void RepeatedProjectExtensions()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`> 
                    <ProjectExtensions/>
                    <Import Project=`$(MSBuildBinPath)\\Microsoft.CSharp.Targets` />
                    <ProjectExtensions/>
                </Project>
            ");
        }

        /// <summary>
        /// Tests that we can correctly pass a CDATA tag containing less-than signs into a property value.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetCDATAWithLessThanSignFromXmlNode()
        {
            string xmlText = "<MyXmlElement><![CDATA[<invalid<xml&&<]]></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("<invalid<xml&&<", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass an Xml element named "CDATA" into a property value.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetLiteralCDATAWithLessThanSignFromXmlNode()
        {
            string xmlText = "<MyXmlElement>This is not a real <CDATA/>, just trying to fool the reader.</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);

            // Notice the extra space after "CDATA" because it normalized the XML.
            Assertion.AssertEquals("This is not a real <CDATA />, just trying to fool the reader.", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass a simple CDATA tag into a property value.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetCDATAFromXmlNode()
        {
            string xmlText = "<MyXmlElement><![CDATA[whatever]]></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("whatever", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass a literal string called "CDATA" into a property value.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetLiteralCDATAFromXmlNode()
        {
            string xmlText = "<MyXmlElement>This is not a real CDATA, just trying to fool the reader.</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("This is not a real CDATA, just trying to fool the reader.", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly parse a property that is Xml containing a CDATA tag.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetCDATAOccurringDeeperWithMoreXml()
        {
            string xmlText = "<MyXmlElement><RootOfPropValue><![CDATA[foo]]></RootOfPropValue></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("<RootOfPropValue><![CDATA[foo]]></RootOfPropValue>", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass CDATA where the CDATA tag itself is surrounded by whitespace
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetCDATAWithSurroundingWhitespace()
        {
            string xmlText = "<MyXmlElement>    <![CDATA[foo]]>    </MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("foo", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly parse a property that is some text concatenated with some XML.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetTextContainingLessThanSignFromXmlNode()
        {
            string xmlText = "<MyXmlElement>This is some text contain a node <xml a='&lt;'/>, &amp; an escaped character.</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);

            // Notice the extra space in the xml node because it normalized the XML, and the
            // change from single quotes to double-quotes.
            Assertion.AssertEquals("This is some text contain a node <xml a=\"&lt;\" />, &amp; an escaped character.", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly parse a property containing text with an escaped character.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetTextFromXmlNode()
        {
            string xmlText = "<MyXmlElement>This is some text &amp; an escaped character.</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("This is some text & an escaped character.", xmlContents);
        }

        /// <summary>
        /// Tests that comments are removed if there is no other XML in the value.
        /// In other words, .InnerText is used even if there are comments (as long as nothing else looks like XML in the string)
        /// </summary>
        [Test]
        public void GetTextFromTextNodeWithXmlComment()
        {
            string xmlText = "<MyXmlElement>foo; <!-- bar; baz; -->biz; &amp; boz</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("foo; biz; & boz", xmlContents);
        }

        [Test]
        public void GetTextFromTextNodeWithXmlComment2()
        {
            string xmlText = "<MyXmlElement><!-- bar; baz; -->xyz<!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("xyz", xmlContents);
        }

        [Test]
        public void GetTextFromTextNodeWithXmlComment3()
        {
            string xmlText = "<MyXmlElement><!----></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("", xmlContents);
        }

        [Test]
        public void GetTextFromTextNodeWithXmlComment4()
        {
            string xmlText = "<MyXmlElement>--></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assertion.AssertEquals("-->", xmlContents);
        }

        [Test]
        public void GetTextFromTextNodeWithXmlComment5()
        {
            string xmlText = "<MyXmlElement>&lt;<!-- bar; baz; --><x/><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assertion.AssertEquals("&lt;<!-- bar; baz; --><x /><!-- bar -->", xmlContents);
        }

        [Test]
        public void GetTextFromTextNodeWithXmlComment6()
        {
            string xmlText = "<MyXmlElement><x/><!-- bar; baz; --><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assertion.AssertEquals("<x /><!-- bar; baz; --><!-- bar -->", xmlContents);
        }

        [Test]
        public void GetTextFromTextNodeWithXmlComment7()
        {
            string xmlText = "<MyXmlElement><!-- bar; baz; --><!-- bar --><x/></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assertion.AssertEquals("<!-- bar; baz; --><!-- bar --><x />", xmlContents);
        }

        private static string GetXmlContents(string xmlText)
        {
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(xmlText);

            XmlElement rootElement = (XmlElement)xmldoc.FirstChild;
            Console.WriteLine("originalxml = " + xmlText);
            Console.WriteLine("innerText   = " + rootElement.InnerText);
            Console.WriteLine("innerXml    = " + rootElement.InnerXml);
            Console.WriteLine("-----------");

            string xmlContents = BuildEngine.Utilities.GetXmlNodeInnerContents(rootElement);
            return xmlContents;
        }
    }
}
