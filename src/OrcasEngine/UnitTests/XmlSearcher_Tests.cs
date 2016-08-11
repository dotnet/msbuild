// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class XmlSearcher_Tests
    {
        /// <summary>
        /// Tests to make sure we can compute the correct element/attribute numbers
        /// for given Xml nodes.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetElementAttributeNumberOfXmlNode()
        {
            string projectFileContents = @"
                <Project xmlns=`msbuild`>

                  <PropertyGroup Condition=`false`>
                    <Optimize>true</Optimize>
                    <WarningLevel>
                       4
                    </WarningLevel>
                    <DebugType/>
                    goo
                    <OutputPath/>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include=`a.cs` Condition=`true`/>
                    <Reference Include=`msbuildengine`>
                       <HintPath>c:\\msbengine.dll</HintPath>
                    </Reference>
                  </ItemGroup>

                </Project>
                ";

            projectFileContents = projectFileContents.Replace ("`", "\"");
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(projectFileContents);

            int elementNumber;
            int attributeNumber;

            // Find the element/attribute number of the <Optimize/> node.
            Assertion.Assert(XmlSearcher.GetElementAndAttributeNumber(
                //  <Project>  <PropertyGroup> <Optimize>
                doc.FirstChild.ChildNodes[0].  ChildNodes[0],
                out elementNumber,
                out attributeNumber));
            Assertion.AssertEquals(3, elementNumber);
            Assertion.AssertEquals(0, attributeNumber);

            // Find the element/attribute number of the "4" inside the <WarningLevel/> tag.
            Assertion.Assert(XmlSearcher.GetElementAndAttributeNumber(
                //  <Project>  <PropertyGroup> <WarningLevel>  4
                doc.FirstChild.ChildNodes[0].  ChildNodes[1].  ChildNodes[0],
                out elementNumber,
                out attributeNumber));
            Assertion.AssertEquals(6, elementNumber);
            Assertion.AssertEquals(0, attributeNumber);

            // Find the element/attribute number of the <DebugType/> node.
            Assertion.Assert(XmlSearcher.GetElementAndAttributeNumber(
                //  <Project>  <PropertyGroup> <DebugType>
                doc.FirstChild.ChildNodes[0].  ChildNodes[2],
                out elementNumber,
                out attributeNumber));
            Assertion.AssertEquals(7, elementNumber);
            Assertion.AssertEquals(0, attributeNumber);

            // Find the element/attribute number of the "goo" node.
            Assertion.Assert(XmlSearcher.GetElementAndAttributeNumber(
                //  <Project>  <PropertyGroup> goo
                doc.FirstChild.ChildNodes[0].  ChildNodes[3],
                out elementNumber,
                out attributeNumber));
            Assertion.AssertEquals(8, elementNumber);
            Assertion.AssertEquals(0, attributeNumber);

            // Find the element/attribute number of the <Reference> node.
            Assertion.Assert(XmlSearcher.GetElementAndAttributeNumber(
                //  <Project>  <ItemGroup>   <Reference>
                doc.FirstChild.ChildNodes[1].ChildNodes[1],
                out elementNumber,
                out attributeNumber));
            Assertion.AssertEquals(12, elementNumber);
            Assertion.AssertEquals(0, attributeNumber);

            // Find the element/attribute number of the "Condition" attribute on the <Compile> node.
            Assertion.Assert(XmlSearcher.GetElementAndAttributeNumber(
                //  <Project>  <ItemGroup>   <Compile>     Condition
                doc.FirstChild.ChildNodes[1].ChildNodes[0].Attributes[1],
                out elementNumber,
                out attributeNumber));
            Assertion.AssertEquals(11, elementNumber);
            Assertion.AssertEquals(2, attributeNumber);

            // Try passing in an Xml element that doesn't even exist in the above document.
            // This should fail.
            Assertion.Assert(!XmlSearcher.GetElementAndAttributeNumber(
                (new XmlDocument()).CreateElement("Project"),
                out elementNumber,
                out attributeNumber));
        }

        /// <summary>
        /// Find the line/column number of a particular XML element.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void GetLineColumnOfXmlNode()
        {
            string projectFileContents =
                "<Project xmlns=`msbuild`>"                         + "\r\n" +
                ""                                                  + "\r\n" +
                "  <PropertyGroup Condition=`false`"                + "\r\n" +
                "                 BogusAttrib=`foo`>"               + "\r\n" +
                "    <Optimize>true</Optimize>"                     + "\r\n" +
                "    <WarningLevel>"                                + "\r\n" +
                "       4"                                          + "\r\n" +
                "    </WarningLevel>"                               + "\r\n" +
                "    <DebugType/>"                                  + "\r\n" +
                "    goo"                                           + "\r\n" +
                "    <OutputPath/>"                                 + "\r\n" +
                "  </PropertyGroup>"                                + "\r\n" +
                ""                                                  + "\r\n" +
                "</Project>"                                        ;

            int foundLineNumber;
            int foundColumnNumber;

            // Okay, we're going to try and find the line/column number for the <DebugType> node.
            // This is the 7th element in the hierarchy.
            // Correct answer is:
            //      Line Number 9
            //      Column Number 5
            GetLineColumnFromProjectFileContentsHelper(projectFileContents, 
                7, 0, out foundLineNumber, out foundColumnNumber);
            Assertion.AssertEquals(9, foundLineNumber);
            Assertion.AssertEquals(5, foundColumnNumber);

            // Okay, we're going to try and find the line/column number for the "4" in the <WarningLevel> property.
            // This is the 6th element in the hierarchy.
            // Correct answer is:
            //      Line Number 6
            //      Column Number 19
            // This is because the text node actually begins immediately after the closing ">" in "<WarningLevel>".
            GetLineColumnFromProjectFileContentsHelper(projectFileContents, 
                6, 0, out foundLineNumber, out foundColumnNumber);
            Assertion.AssertEquals(6, foundLineNumber);
            Assertion.AssertEquals(19, foundColumnNumber);

            // Okay, we're going to try and find the line/column number for the BogusAttrib attribute.
            // This is on the 2nd element in the hierarchy, and it is the 2nd attribute in that element.
            // Correct answer is:
            //      Line Number 4
            //      Column Number 18
            GetLineColumnFromProjectFileContentsHelper(projectFileContents, 
                2, 2, out foundLineNumber, out foundColumnNumber);
            Assertion.AssertEquals(4, foundLineNumber);
            Assertion.AssertEquals(18, foundColumnNumber);

            // Okay, we're going to try and find the line/column number for the "goo" beneath <PropertyGroup>.
            // This is the 8th element in the hierarchy.
            // Correct answer is:
            //      Line Number 9
            //      Column Number 17
            GetLineColumnFromProjectFileContentsHelper(projectFileContents, 
                8, 0, out foundLineNumber, out foundColumnNumber);
            Assertion.AssertEquals(9, foundLineNumber);
            Assertion.AssertEquals(17, foundColumnNumber);

            // Let's try passing in a bogus element number.
            GetLineColumnFromProjectFileContentsHelper(projectFileContents, 
                25, 0, out foundLineNumber, out foundColumnNumber);
            Assertion.AssertEquals(0, foundLineNumber);
            Assertion.AssertEquals(0, foundColumnNumber);

            // And let's try passing in a bogus attribute number.
            GetLineColumnFromProjectFileContentsHelper(projectFileContents, 
                7, 4, out foundLineNumber, out foundColumnNumber);
            Assertion.AssertEquals(0, foundLineNumber);
            Assertion.AssertEquals(0, foundColumnNumber);
        }

        /// <summary>
        /// Given a string representing the contents of the project file, create a project file
        /// on disk with those contents.  Then call the method to find the line/column number of 
        /// a particular node in the project file, based on the element/attribute number of that node.
        /// </summary>
        /// <param name="projectFileContents"></param>
        /// <returns>an instance of our special line-number-enabled reader</returns>
        /// <owner>RGoel</owner>
        private void GetLineColumnFromProjectFileContentsHelper
            (
            string projectFileContents,
            int xmlElementNumberToSearchFor,
            int xmlAttributeNumberToSearchFor,
            out int foundLineNumber,
            out int foundColumnNumber
            )
        {
            string projectFile = ObjectModelHelpers.CreateTempFileOnDisk(projectFileContents);

            XmlSearcher.GetLineColumnByNodeNumber(projectFile, 
                xmlElementNumberToSearchFor, xmlAttributeNumberToSearchFor, 
                out foundLineNumber, out foundColumnNumber);

            // Delete the temp file.
            File.Delete(projectFile);
        }
    }
}
