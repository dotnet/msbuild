// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Collections;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Collections.Generic;

using Toolset = Microsoft.Build.Evaluation.Toolset;
using Project = Microsoft.Build.Evaluation.Project;
using ProjectCollection = Microsoft.Build.Evaluation.ProjectCollection;

using InternalUtilities = Microsoft.Build.Internal.Utilities;
using CommunicationsUtilities = Microsoft.Build.Internal.CommunicationsUtilities;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

using XmlDocumentWithLocation = Microsoft.Build.Construction.XmlDocumentWithLocation;
using XmlElementWithLocation = Microsoft.Build.Construction.XmlElementWithLocation;

using MSBuildApp = Microsoft.Build.CommandLine.MSBuildApp;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class UtilitiesTestStandard : UtilitiesTest
    {
        public UtilitiesTestStandard()
        {
            this.loadAsReadOnly = false;
        }

        [TestMethod]
        public void GetTextFromTextNodeWithXmlComment5()
        {
            string xmlText = "<MyXmlElement>&lt;<!-- bar; baz; --><x/><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("&lt;<!-- bar; baz; --><x /><!-- bar -->", xmlContents);
        }

        [TestMethod]
        public void GetTextFromTextNodeWithXmlComment6()
        {
            string xmlText = "<MyXmlElement><x/><!-- bar; baz; --><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("<x /><!-- bar; baz; --><!-- bar -->", xmlContents);
        }

        [TestMethod]
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
        [TestMethod]
        [Ignore]
        // Ignore: Flaky test
        public void CommentsInPreprocessing()
        {
            Microsoft.Build.Construction.XmlDocumentWithLocation.ClearReadOnlyFlags_UnitTestsOnly();

            string input = FileUtilities.GetTemporaryFile();
            string output = FileUtilities.GetTemporaryFile();

            try
            {
                string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets='Build' ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
  <Import Project='$(MSBuildToolsPath)\microsoft.csharp.targets'/>
</Project>");
                File.WriteAllText(input, content);

                Assert.AreEqual(MSBuildApp.ExitType.Success, MSBuildApp.Execute(@"c:\bin\msbuild.exe """ + input + @""" /pp:""" + output + @""""));

                bool foundDoNotModify = false;
                foreach (string line in File.ReadLines(output))
                {
                    if (line.Contains("<!---->")) // This is what it will look like if we're loading read/only
                    {
                        Assert.Fail();
                    }

                    if (line.Contains("DO NOT MODIFY")) // this is in a comment in our targets
                    {
                        foundDoNotModify = true;
                    }
                }

                Assert.AreEqual(true, foundDoNotModify);
            }
            finally
            {
                File.Delete(input);
                File.Delete(output);
            }
        }

        [TestMethod]
        public void GetTextFromTextNodeWithXmlComment5()
        {
            string xmlText = "<MyXmlElement>&lt;<!-- bar; baz; --><x/><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("&lt;<!----><x /><!---->", xmlContents);
        }

        [TestMethod]
        public void GetTextFromTextNodeWithXmlComment6()
        {
            string xmlText = "<MyXmlElement><x/><!-- bar; baz; --><!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            // Should get XML; note space after x added
            Assert.AreEqual("<x /><!----><!---->", xmlContents);
        }

        [TestMethod]
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
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void IllegalConditionOnProjectExtensions()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ProjectExtensions Condition=`'a'=='b'`/>
                    <Import Project=`$(MSBuildBinPath)\\Microsoft.CSharp.Targets` />
                </Project>
            ");
        }

        /// <summary>
        /// Verify ProjectExtensions cannot exist twice
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void RepeatedProjectExtensions()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`> 
                    <ProjectExtensions/>
                    <Import Project=`$(MSBuildBinPath)\\Microsoft.CSharp.Targets` />
                    <ProjectExtensions/>
                </Project>
            ");
        }

        /// <summary>
        /// Tests that we can correctly pass a CDATA tag containing less-than signs into a property value.
        /// </summary>
        [TestMethod]
        public void GetCDATAWithLessThanSignFromXmlNode()
        {
            string xmlText = "<MyXmlElement><![CDATA[<invalid<xml&&<]]></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("<invalid<xml&&<", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass an Xml element named "CDATA" into a property value.
        /// </summary>
        [TestMethod]
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
        [TestMethod]
        public void GetCDATAFromXmlNode()
        {
            string xmlText = "<MyXmlElement><![CDATA[whatever]]></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("whatever", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass a literal string called "CDATA" into a property value.
        /// </summary>
        [TestMethod]
        public void GetLiteralCDATAFromXmlNode()
        {
            string xmlText = "<MyXmlElement>This is not a real CDATA, just trying to fool the reader.</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("This is not a real CDATA, just trying to fool the reader.", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly parse a property that is Xml containing a CDATA tag.
        /// </summary>
        [TestMethod]
        public void GetCDATAOccurringDeeperWithMoreXml()
        {
            string xmlText = "<MyXmlElement><RootOfPropValue><![CDATA[foo]]></RootOfPropValue></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("<RootOfPropValue><![CDATA[foo]]></RootOfPropValue>", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly pass CDATA where the CDATA tag itself is surrounded by whitespace
        /// </summary>
        [TestMethod]
        public void GetCDATAWithSurroundingWhitespace()
        {
            string xmlText = "<MyXmlElement>    <![CDATA[foo]]>    </MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("foo", xmlContents);
        }

        /// <summary>
        /// Tests that we can correctly parse a property that is some text concatenated with some XML.
        /// </summary>
        [TestMethod]
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
        [TestMethod]
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
        [TestMethod]
        public void GetTextFromTextNodeWithXmlComment()
        {
            string xmlText = "<MyXmlElement>foo; <!-- bar; baz; -->biz; &amp; boz</MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("foo; biz; & boz", xmlContents);
        }

        [TestMethod]
        public void GetTextFromTextNodeWithXmlComment2()
        {
            string xmlText = "<MyXmlElement><!-- bar; baz; -->xyz<!-- bar --></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("xyz", xmlContents);
        }

        [TestMethod]
        public void GetTextFromTextNodeWithXmlComment3()
        {
            string xmlText = "<MyXmlElement><!----></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("", xmlContents);
        }

        [TestMethod]
        public void GetTextFromTextNodeWithXmlComment4()
        {
            string xmlText = "<MyXmlElement>--></MyXmlElement>";
            string xmlContents = GetXmlContents(xmlText);
            Assert.AreEqual("-->", xmlContents);
        }

        /// <summary>
        /// Check creating the tools version list for an error message
        /// </summary>
        [TestMethod]
        public void CreateToolsVersionString()
        {
            List<Toolset> toolsets = new List<Toolset>();
            toolsets.Add(new Toolset("66", "x", new ProjectCollection(), null));
            toolsets.Add(new Toolset("44", "y", new ProjectCollection(), null));

            string result = InternalUtilities.CreateToolsVersionListString(toolsets);

            Assert.AreEqual("\"66\", \"44\"", result);
        }

        /// <summary>
        /// Verify our custom way of getting env vars gives the same results as the BCL.
        /// </summary>
        [TestMethod]
        public void GetEnvVars()
        {
            IDictionary<string, string> envVars = CommunicationsUtilities.GetEnvironmentVariables();
            IDictionary referenceVars = Environment.GetEnvironmentVariables();
            IDictionary<string, string> referenceVars2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry item in referenceVars)
            {
                referenceVars2.Add((string)item.Key, (string)item.Value);
            }

            Helpers.AssertCollectionsValueEqual(envVars, referenceVars2);
        }

        protected string GetXmlContents(string xmlText)
        {
            XmlDocumentWithLocation xmldoc = new XmlDocumentWithLocation(loadAsReadOnly);
            xmldoc.LoadXml(xmlText);

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
