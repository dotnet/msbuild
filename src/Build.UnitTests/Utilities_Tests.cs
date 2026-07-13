// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Shouldly;
using InternalUtilities = Microsoft.Build.Internal.Utilities;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using MSBuildApp = Microsoft.Build.CommandLine.MSBuildApp;
using ProjectCollection = Microsoft.Build.Evaluation.ProjectCollection;
using Toolset = Microsoft.Build.Evaluation.Toolset;
using XmlDocumentWithLocation = Microsoft.Build.Construction.XmlDocumentWithLocation;
using XmlElementWithLocation = Microsoft.Build.Construction.XmlElementWithLocation;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class UtilitiesTestStandard : UtilitiesTest
    {
        public UtilitiesTestStandard()
        {
            this.loadAsReadOnly = false;
        }

        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment5()
        {
            string xmlText = "<MyXmlElement>&lt;<!-- bar; baz; --><x/><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("&lt;<!-- bar; baz; --><x /><!-- bar -->", xmlContents);
        }

        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment6()
        {
            string xmlText = "<MyXmlElement><x/><!-- bar; baz; --><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("<x /><!-- bar; baz; --><!-- bar -->", xmlContents);
        }

        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment7()
        {
            string xmlText = "<MyXmlElement><!-- bar; baz; --><!-- bar --><x/></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("<!-- bar; baz; --><!-- bar --><x />", xmlContents);
        }
    }

    [TestClass]
    public class UtilitiesTestReadOnlyLoad : UtilitiesTest
    {
        public UtilitiesTestReadOnlyLoad()
        {
            this.loadAsReadOnly = true;
        }

        /// <summary>
        /// Comments should not be stripped when doing /pp.
        /// This is really testing msbuild.exe but it's here because it needs to
        /// call the internal reset method on the engine
        /// </summary>
        [MSBuildTestMethod]
        public void CommentsInPreprocessing()
        {
            using TestEnvironment env = TestEnvironment.Create();
            XmlDocumentWithLocation.ClearReadOnlyFlags_UnitTestsOnly();

            TransientTestFile inputFile = env.CreateFile("tempInput.tmp", ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets='Build'>
<Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets'/>
</Project>"));
            TransientTestFile outputFile = env.CreateFile("tempOutput.tmp");

            env.SetEnvironmentVariable("MSBUILDLOADALLFILESASWRITEABLE", "1");

            Assert.AreEqual(
                MSBuildApp.ExitType.Success,
                MSBuildApp.Execute([ @"c:\bin\msbuild.exe", '"' + inputFile.Path + '"', '"' + (NativeMethodsShared.IsUnixLike ? "-pp:" : "/pp:") + outputFile.Path + '"']));

            bool foundDoNotModify = false;
            foreach (string line in File.ReadLines(outputFile.Path))
            {
                line.ShouldNotContain("<!---->", customMessage: "This is what it will look like if we're loading read/only");

                if (line.Contains("DO NOT MODIFY")) // this is in a comment in our targets
                {
                    foundDoNotModify = true;
                }
            }

            foundDoNotModify.ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment5()
        {
            string xmlText = "<MyXmlElement>&lt;<!-- bar; baz; --><x/><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("&lt;<!----><x /><!---->", xmlContents);
        }

        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment6()
        {
            string xmlText = "<MyXmlElement><x/><!-- bar; baz; --><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("<x /><!----><!---->", xmlContents);
        }

        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment7()
        {
            string xmlText = "<MyXmlElement><!-- bar; baz; --><!-- bar --><x/></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("<!----><!----><x />", xmlContents);
        }
    }

    [TestClass]
    public abstract class UtilitiesTest
    {
        public bool loadAsReadOnly;

        /// <summary>
        /// Verify Condition is illegal on ProjectExtensions tag
        /// </summary>
        [MSBuildTestMethod]
        public void IllegalConditionOnProjectExtensions()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ObjectModelHelpers.CreateInMemoryProject(@"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ProjectExtensions Condition=`'a'=='b'`/>
                    <Import Project=`$(MSBuildBinPath)\\Microsoft.CSharp.Targets` />
                </Project>
            ");
            });
        }
        /// <summary>
        /// Verify ProjectExtensions cannot exist twice
        /// </summary>
        [MSBuildTestMethod]
        public void RepeatedProjectExtensions()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ProjectExtensions/>
                    <Import Project=`$(MSBuildBinPath)\\Microsoft.CSharp.Targets` />
                    <ProjectExtensions/>
                </Project>
            ");
            });
        }
        /// <summary>
        /// Tests that we can correctly pass a CDATA tag containing less-than signs into a property value.
        /// </summary>
        [MSBuildTestMethod]
        public void GetCDATAWithLessThanSignFromXmlNode()
        {
            string xmlText = "<MyXmlElement><![CDATA[<invalid<xml&&<]]></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("<invalid<xml&&<", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass an Xml element named "CDATA" into a property value.
        /// </summary>
        [MSBuildTestMethod]
        public void GetLiteralCDATAWithLessThanSignFromXmlNode()
        {
            string xmlText = "<MyXmlElement>This is not a real <CDATA/>, just trying to fool the reader.</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);

            // Notice the extra space after "CDATA" because it normalized the XML.
            Assert.AreEqual("This is not a real <CDATA />, just trying to fool the reader.", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass a simple CDATA tag into a property value.
        /// </summary>
        [MSBuildTestMethod]
        public void GetCDATAFromXmlNode()
        {
            string xmlText = "<MyXmlElement><![CDATA[whatever]]></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("whatever", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass a literal string called "CDATA" into a property value.
        /// </summary>
        [MSBuildTestMethod]
        public void GetLiteralCDATAFromXmlNode()
        {
            string xmlText = "<MyXmlElement>This is not a real CDATA, just trying to fool the reader.</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("This is not a real CDATA, just trying to fool the reader.", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly parse a property that is Xml containing a CDATA tag.
        /// </summary>
        [MSBuildTestMethod]
        public void GetCDATAOccurringDeeperWithMoreXml()
        {
            string xmlText = "<MyXmlElement><RootOfPropValue><![CDATA[foo]]></RootOfPropValue></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("<RootOfPropValue><![CDATA[foo]]></RootOfPropValue>", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass CDATA where the CDATA tag itself is surrounded by whitespace
        /// </summary>
        [MSBuildTestMethod]
        public void GetCDATAWithSurroundingWhitespace()
        {
            string xmlText = "<MyXmlElement>    <![CDATA[foo]]>    </MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("foo", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly parse a property that is some text concatenated with some XML.
        /// </summary>
        [MSBuildTestMethod]
        public void GetTextContainingLessThanSignFromXmlNode()
        {
            string xmlText = "<MyXmlElement>This is some text contain a node <xml a='&lt;'/>, &amp; an escaped character.</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);

            // Notice the extra space in the xml node because it normalized the XML, and the
            // change from single quotes to double-quotes.
            Assert.AreEqual("This is some text contain a node <xml a=\"&lt;\" />, &amp; an escaped character.", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly parse a property containing text with an escaped character.
        /// </summary>
        [MSBuildTestMethod]
        public void GetTextFromXmlNode()
        {
            string xmlText = "<MyXmlElement>This is some text &amp; an escaped character.</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("This is some text & an escaped character.", xmlContents);
        }

        /// <summary>
        /// Tests that comments are removed if there is no other XML in the value.
        /// In other words, .InnerText is used even if there are comments (as long as nothing else looks like XML in the string)
        /// </summary>
        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment()
        {
            string xmlText = "<MyXmlElement>foo; <!-- bar; baz; -->biz; &amp; boz</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("foo; biz; & boz", xmlContents);
        }

        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment2()
        {
            string xmlText = "<MyXmlElement><!-- bar; baz; -->xyz<!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("xyz", xmlContents);
        }

        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment3()
        {
            string xmlText = "<MyXmlElement><!----></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("", xmlContents);
        }

        [MSBuildTestMethod]
        public void GetTextFromTextNodeWithXmlComment4()
        {
            string xmlText = "<MyXmlElement>--></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("-->", xmlContents);
        }

        /// <summary>
        /// Check creating the tools version list for an error message
        /// </summary>
        [MSBuildTestMethod]
        public void CreateToolsVersionString()
        {
            List<Toolset> toolsets = new List<Toolset>();

            using var colletionX = new ProjectCollection();
            using var colletionY = new ProjectCollection();
            toolsets.Add(new Toolset("66", "x", colletionX, null));
            toolsets.Add(new Toolset("44", "y", colletionY, null));

            string result = InternalUtilities.CreateToolsVersionListString(toolsets);

            Assert.AreEqual("\"66\", \"44\"", result);
        }

        protected string GetXmlContents(string xmlText)
        {
            XmlDocumentWithLocation xmldoc = new XmlDocumentWithLocation(loadAsReadOnly);
            using (StringReader sreader = new StringReader(xmlText))
            using (XmlReader reader = XmlReader.Create(sreader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                xmldoc.Load(reader);
            }

            XmlElementWithLocation rootElement = (XmlElementWithLocation)xmldoc.FirstChild;
            Console.WriteLine("originalxml = " + xmlText);
            Console.WriteLine("innerText   = " + rootElement.InnerText);
            Console.WriteLine("innerXml    = " + rootElement.InnerXml);
            Console.WriteLine("-----------");

            string xmlContents = InternalUtilities.GetXmlNodeInnerContents(rootElement);
            return xmlContents;
        }
    }
}
