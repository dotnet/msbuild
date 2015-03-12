// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the ElementLocation class</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Framework;
using System.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;
using Microsoft.Build.Construction;
using Microsoft.Build.UnitTests.BackEnd;
using System.Xml;
using System.IO;
using System.Reflection;

namespace Microsoft.Build.UnitTests.Construction
{
    /// <summary>
    /// Tests for the ElementLocation class
    /// </summary>
    [TestClass]
    public class ElementLocation_Tests
    {
        /// <summary>
        /// Path to the common targets
        /// </summary>
        private string _pathToCommonTargets = Path.Combine(FrameworkLocationHelper.PathToDotNetFrameworkV45, "microsoft.common.targets");

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [TestMethod]
        public void ConstructorTest1()
        {
            IElementLocation location = ElementLocation.Create("file", 65536, 0);
            Assert.AreEqual("file", location.File);
            Assert.AreEqual(65536, location.Line);
            Assert.AreEqual(0, location.Column);
            Assert.IsTrue(location.GetType().FullName.Contains("RegularElementLocation"));
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [TestMethod]
        public void ConstructorTest2()
        {
            IElementLocation location = ElementLocation.Create("file", 0, 65536);
            Assert.AreEqual("file", location.File);
            Assert.AreEqual(0, location.Line);
            Assert.AreEqual(65536, location.Column);
            Assert.IsTrue(location.GetType().FullName.Contains("RegularElementLocation"));
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [TestMethod]
        public void ConstructorTest3()
        {
            IElementLocation location = ElementLocation.Create("file", 65536, 65537);
            Assert.AreEqual("file", location.File);
            Assert.AreEqual(65536, location.Line);
            Assert.AreEqual(65537, location.Column);
            Assert.IsTrue(location.GetType().FullName.Contains("RegularElementLocation"));
        }

        /// <summary>
        /// Test equality
        /// </summary>
        [TestMethod]
        public void Equality()
        {
            IElementLocation location1 = ElementLocation.Create("file", 65536, 65537);
            IElementLocation location2 = ElementLocation.Create("file", 0, 1);
            IElementLocation location3 = ElementLocation.Create("file", 0, 65537);
            IElementLocation location4 = ElementLocation.Create("file", 65536, 1);
            IElementLocation location5 = ElementLocation.Create("file", 0, 1);
            IElementLocation location6 = ElementLocation.Create("file", 65536, 65537);

            Assert.AreEqual(true, location1.Equals(location6));
            Assert.AreEqual(true, location2.Equals(location5));
            Assert.AreEqual(false, location3.Equals(location1));
            Assert.AreEqual(false, location4.Equals(location2));
            Assert.AreEqual(false, location4.Equals(location6));
        }

        /// <summary>
        /// Check it will use large element location when it should.
        /// Using file as BIZARRELY XmlTextReader+StringReader crops or trims.
        /// </summary>
        [TestMethod]
        public void TestLargeElementLocationUsedLargeColumn()
        {
            string file = null;

            try
            {
                file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents("<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>\r\n<ItemGroup>") + new string(' ', 70000) + @"<x/></ItemGroup></Project>");

                ProjectRootElement projectXml = ProjectRootElement.Open(file);
            }
            catch (InvalidProjectFileException ex)
            {
                Assert.AreEqual(70012, ex.ColumnNumber);
                Assert.AreEqual(2, ex.LineNumber);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Check it will use large element location when it should.
        /// Using file as BIZARRELY XmlTextReader+StringReader crops or trims.
        /// </summary>
        [TestMethod]
        public void TestLargeElementLocationUsedLargeLine()
        {
            string file = null;

            try
            {
                string longstring = String.Empty;

                for (int i = 0; i < 7000; i++)
                {
                    longstring += "\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n";
                }

                file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents("<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>\r\n<ItemGroup>") + longstring + @" <x/></ItemGroup></Project>");

                ProjectRootElement projectXml = ProjectRootElement.Open(file);
            }
            catch (InvalidProjectFileException ex)
            {
                Assert.AreEqual(70002, ex.LineNumber);
                Assert.AreEqual(2, ex.ColumnNumber);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Tests serialization.
        /// </summary>
        [TestMethod]
        public void SerializationTest()
        {
            IElementLocation location = ElementLocation.Create("file", 65536, 65537);

            TranslationHelpers.GetWriteTranslator().Translate(ref location, ElementLocation.FactoryForDeserialization);
            IElementLocation deserializedLocation = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedLocation, ElementLocation.FactoryForDeserialization);

            Assert.AreEqual(location.File, deserializedLocation.File);
            Assert.AreEqual(location.Line, deserializedLocation.Line);
            Assert.AreEqual(location.Column, deserializedLocation.Column);
            Assert.IsTrue(location.GetType().FullName.Contains("RegularElementLocation"));
        }

        /// <summary>
        /// Tests constructor specifying file, line and column.
        /// </summary>
        [TestMethod]
        public void ConstructorWithIndicesTest_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 65535, 65534);
            Assert.AreEqual("file", location.File);
            Assert.AreEqual(65535, location.Line);
            Assert.AreEqual(65534, location.Column);
            Assert.IsTrue(location.GetType().FullName.Contains("SmallElementLocation"));
        }

        /// <summary>
        /// Tests constructor specifying file, negative line, column
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void ConstructorWithNegativeIndicesTest1()
        {
            IElementLocation location = ElementLocation.Create("file", -1, 2);
        }

        /// <summary>
        /// Tests constructor specifying file, line, negative column
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void ConstructorWithNegativeIndicesTest2n()
        {
            IElementLocation location = ElementLocation.Create("file", 1, -2);
        }

        /// <summary>
        /// Tests constructor with invalid null file.
        /// </summary>
        [TestMethod]
        public void ConstructorTestNullFile()
        {
            IElementLocation location = ElementLocation.Create(null);
            Assert.AreEqual(location.File, String.Empty);
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [TestMethod]
        public void ConstructorTest1_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 65535, 0);
            Assert.AreEqual("file", location.File);
            Assert.AreEqual(65535, location.Line);
            Assert.AreEqual(0, location.Column);
            Assert.IsTrue(location.GetType().FullName.Contains("SmallElementLocation"));
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [TestMethod]
        public void ConstructorTest2_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 0, 65535);
            Assert.AreEqual("file", location.File);
            Assert.AreEqual(0, location.Line);
            Assert.AreEqual(65535, location.Column);
            Assert.IsTrue(location.GetType().FullName.Contains("SmallElementLocation"));
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [TestMethod]
        public void ConstructorTest3_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 65535, 65534);
            Assert.AreEqual("file", location.File);
            Assert.AreEqual(65535, location.Line);
            Assert.AreEqual(65534, location.Column);
            Assert.IsTrue(location.GetType().FullName.Contains("SmallElementLocation"));
        }

        /// <summary>
        /// Tests serialization.
        /// </summary>
        [TestMethod]
        public void SerializationTest_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 65535, 2);

            TranslationHelpers.GetWriteTranslator().Translate(ref location, ElementLocation.FactoryForDeserialization);
            IElementLocation deserializedLocation = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedLocation, ElementLocation.FactoryForDeserialization);

            Assert.AreEqual(location.File, deserializedLocation.File);
            Assert.AreEqual(location.Line, deserializedLocation.Line);
            Assert.AreEqual(location.Column, deserializedLocation.Column);
            Assert.IsTrue(location.GetType().FullName.Contains("SmallElementLocation"));
        }

        /// <summary>
        /// Test many of the getters
        /// </summary>
        [TestMethod]
        public void LocationStringsMedleyReadOnlyLoad()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
            <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                    <UsingTask TaskName='t' AssemblyName='a' Condition='true'/>
                    <UsingTask TaskName='t' AssemblyFile='a' Condition='true'/>
                    <ItemDefinitionGroup Condition='true' Label='l'>
                        <m Condition='true'>  foo  bar
  </m>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <i Include='i' Condition='true' Exclude='r'>
                            <m Condition='true'/>
                        </i>
                    </ItemGroup>
                    <PropertyGroup>
                        <p Condition='true'/>
                    </PropertyGroup>
                   <!-- A comment -->
                    <Target Name='Build' Condition='true' Inputs='i' Outputs='o'>
                        <ItemGroup>
                            <i Include='i' Condition='true' Exclude='r'>
                                <m Condition='true'/>
                            </i>
                            <i Remove='r'/>
                        </ItemGroup>
                        <PropertyGroup xml:space= 'preserve'>             <x/>
                            <p     Condition='true'/>
                        </PropertyGroup>
                        <Error Text='xyz' ContinueOnError='true' Importance='high'/>
                    </Target>
                    <Import Project='p' Condition='false'/>
                </Project>
                ");

            string readWriteLoadLocations = GetLocations(content, readOnly: false);
            string readOnlyLoadLocations = GetLocations(content, readOnly: true);

            Console.WriteLine(readWriteLoadLocations);

            Helpers.VerifyAssertLineByLine(readWriteLoadLocations, readOnlyLoadLocations);
        }

        /// <summary>
        /// Save read only fails
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SaveReadOnly1()
        {
            var doc = new XmlDocumentWithLocation(loadAsReadOnly: true);
            doc.Load(_pathToCommonTargets);
            doc.Save(FileUtilities.GetTemporaryFile());
        }

        /// <summary>
        /// Save read only fails
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SaveReadOnly2()
        {
            var doc = new XmlDocumentWithLocation(loadAsReadOnly: true);
            doc.Load(_pathToCommonTargets);
            doc.Save(new MemoryStream());
        }

        /// <summary>
        /// Save read only fails
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SaveReadOnly3()
        {
            var doc = new XmlDocumentWithLocation(loadAsReadOnly: true);
            doc.Load(_pathToCommonTargets);
            doc.Save(new StringWriter());
        }

        /// <summary>
        /// Save read only fails
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SaveReadOnly4()
        {
            var doc = new XmlDocumentWithLocation(loadAsReadOnly: true);
            doc.Load(_pathToCommonTargets);
            doc.Save(XmlWriter.Create(FileUtilities.GetTemporaryFile()));
        }

        /// <summary>
        /// Get location strings for the content, loading as readonly if specified
        /// </summary>
        private string GetLocations(string content, bool readOnly)
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();
                File.WriteAllText(file, content);
                var doc = new XmlDocumentWithLocation(loadAsReadOnly: readOnly);
                doc.Load(file);
                var allNodes = doc.SelectNodes("//*|//@*");

                string locations = String.Empty;
                foreach (var node in allNodes)
                {
                    foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (property.Name.Equals("Location"))
                        {
                            var value = ((ElementLocation)property.GetValue(node, null));

                            if (value != null) // null means attribute is not present
                            {
                                locations += ((XmlNode)node).Name + "==" + ((XmlNode)node).Value ?? String.Empty + ":  " + value.LocationString + "\r\n";
                            }
                        }
                    }
                }

                locations = locations.Replace(file, "c:\\foo\\bar.csproj");

                return locations;
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
