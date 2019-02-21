// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Collections;
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
using Xunit;
using System.Text;

namespace Microsoft.Build.UnitTests.Construction
{
    /// <summary>
    /// Tests for the ElementLocation class
    /// </summary>
    public class ElementLocation_Tests
    {
        /// <summary>
        /// Path to the common targets
        /// </summary>
        private string _pathToCommonTargets =
#if FEATURE_INSTALLED_MSBUILD
            Path.Combine(FrameworkLocationHelper.PathToDotNetFrameworkV45, "Microsoft.Common.targets");
#else
            Path.Combine(AppContext.BaseDirectory, "Microsoft.Common.targets");
#endif

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [Fact]
        public void ConstructorTest1()
        {
            IElementLocation location = ElementLocation.Create("file", 65536, 0);
            Assert.Equal("file", location.File);
            Assert.Equal(65536, location.Line);
            Assert.Equal(0, location.Column);
            Assert.Contains("RegularElementLocation", location.GetType().FullName);
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [Fact]
        public void ConstructorTest2()
        {
            IElementLocation location = ElementLocation.Create("file", 0, 65536);
            Assert.Equal("file", location.File);
            Assert.Equal(0, location.Line);
            Assert.Equal(65536, location.Column);
            Assert.Contains("RegularElementLocation", location.GetType().FullName);
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [Fact]
        public void ConstructorTest3()
        {
            IElementLocation location = ElementLocation.Create("file", 65536, 65537);
            Assert.Equal("file", location.File);
            Assert.Equal(65536, location.Line);
            Assert.Equal(65537, location.Column);
            Assert.Contains("RegularElementLocation", location.GetType().FullName);
        }

        /// <summary>
        /// Test equality
        /// </summary>
        [Fact]
        public void Equality()
        {
            IElementLocation location1 = ElementLocation.Create("file", 65536, 65537);
            IElementLocation location2 = ElementLocation.Create("file", 0, 1);
            IElementLocation location3 = ElementLocation.Create("file", 0, 65537);
            IElementLocation location4 = ElementLocation.Create("file", 65536, 1);
            IElementLocation location5 = ElementLocation.Create("file", 0, 1);
            IElementLocation location6 = ElementLocation.Create("file", 65536, 65537);

            Assert.True(location1.Equals(location6));
            Assert.True(location2.Equals(location5));
            Assert.False(location3.Equals(location1));
            Assert.False(location4.Equals(location2));
            Assert.False(location4.Equals(location6));
        }

        /// <summary>
        /// Check it will use large element location when it should.
        /// Using file as BIZARRELY XmlTextReader+StringReader crops or trims.
        /// </summary>
        [Fact]
        public void TestLargeElementLocationUsedLargeColumn()
        {
            string file = null;

            try
            {
                file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents("<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>\r\n<ItemGroup>") + new string(' ', 70000) + @"<x/></ItemGroup></Project>");

                ProjectRootElement.Open(file);
            }
            catch (InvalidProjectFileException ex)
            {
                Assert.Equal(70012, ex.ColumnNumber);
                Assert.Equal(2, ex.LineNumber);
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
        [Fact]
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

                ProjectRootElement.Open(file);
            }
            catch (InvalidProjectFileException ex)
            {
                Assert.Equal(70002, ex.LineNumber);
                Assert.Equal(2, ex.ColumnNumber);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Tests serialization.
        /// </summary>
        [Fact]
        public void SerializationTest()
        {
            IElementLocation location = ElementLocation.Create("file", 65536, 65537);

            TranslationHelpers.GetWriteTranslator().Translate(ref location, ElementLocation.FactoryForDeserialization);
            IElementLocation deserializedLocation = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedLocation, ElementLocation.FactoryForDeserialization);

            Assert.Equal(location.File, deserializedLocation.File);
            Assert.Equal(location.Line, deserializedLocation.Line);
            Assert.Equal(location.Column, deserializedLocation.Column);
            Assert.Contains("RegularElementLocation", location.GetType().FullName);
        }

        /// <summary>
        /// Tests serialization of empty location.
        /// </summary>
        [Fact]
        public void SerializationTestForEmptyLocation()
        {
            IElementLocation location = ElementLocation.EmptyLocation;

            TranslationHelpers.GetWriteTranslator().Translate(ref location, ElementLocation.FactoryForDeserialization);
            IElementLocation deserializedLocation = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedLocation, ElementLocation.FactoryForDeserialization);

            Assert.Equal(location.File, deserializedLocation.File);
            Assert.Equal(location.Line, deserializedLocation.Line);
            Assert.Equal(location.Column, deserializedLocation.Column);
            Assert.Contains("SmallElementLocation", deserializedLocation.GetType().FullName);
        }

        /// <summary>
        /// Tests constructor specifying file, line and column.
        /// </summary>
        [Fact]
        public void ConstructorWithIndicesTest_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 65535, 65534);
            Assert.Equal("file", location.File);
            Assert.Equal(65535, location.Line);
            Assert.Equal(65534, location.Column);
            Assert.Contains("SmallElementLocation", location.GetType().FullName);
        }

        /// <summary>
        /// Tests constructor specifying file, negative line, column
        /// </summary>
        [Fact]
        public void ConstructorWithNegativeIndicesTest1()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ElementLocation.Create("file", -1, 2);
            }
           );
        }
        /// <summary>
        /// Tests constructor specifying file, line, negative column
        /// </summary>
        [Fact]
        public void ConstructorWithNegativeIndicesTest2n()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ElementLocation.Create("file", 1, -2);
            }
           );
        }
        /// <summary>
        /// Tests constructor with invalid null file.
        /// </summary>
        [Fact]
        public void ConstructorTestNullFile()
        {
            IElementLocation location = ElementLocation.Create(null);
            Assert.Equal(location.File, String.Empty);
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [Fact]
        public void ConstructorTest1_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 65535, 0);
            Assert.Equal("file", location.File);
            Assert.Equal(65535, location.Line);
            Assert.Equal(0, location.Column);
            Assert.Contains("SmallElementLocation", location.GetType().FullName);
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [Fact]
        public void ConstructorTest2_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 0, 65535);
            Assert.Equal("file", location.File);
            Assert.Equal(0, location.Line);
            Assert.Equal(65535, location.Column);
            Assert.Contains("SmallElementLocation", location.GetType().FullName);
        }

        /// <summary>
        /// Tests constructor specifying only file.
        /// </summary>
        [Fact]
        public void ConstructorTest3_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 65535, 65534);
            Assert.Equal("file", location.File);
            Assert.Equal(65535, location.Line);
            Assert.Equal(65534, location.Column);
            Assert.Contains("SmallElementLocation", location.GetType().FullName);
        }

        /// <summary>
        /// Tests serialization.
        /// </summary>
        [Fact]
        public void SerializationTest_SmallElementLocation()
        {
            IElementLocation location = ElementLocation.Create("file", 65535, 2);

            TranslationHelpers.GetWriteTranslator().Translate(ref location, ElementLocation.FactoryForDeserialization);
            IElementLocation deserializedLocation = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedLocation, ElementLocation.FactoryForDeserialization);

            Assert.Equal(location.File, deserializedLocation.File);
            Assert.Equal(location.Line, deserializedLocation.Line);
            Assert.Equal(location.Column, deserializedLocation.Column);
            Assert.Contains("SmallElementLocation", location.GetType().FullName);
        }

        /// <summary>
        /// Test many of the getters
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
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

        // Without save to file, this becomes identical to SaveReadOnly4
#if FEATURE_XML_LOADPATH
        /// <summary>
        /// Save read only fails
        /// </summary>
        [Fact]
        public void SaveReadOnly1()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                var doc = new XmlDocumentWithLocation(loadAsReadOnly: true);
                doc.Load(_pathToCommonTargets);
                Assert.True(doc.IsReadOnly);
                doc.Save(FileUtilities.GetTemporaryFile());
            }
           );
        }
#endif

        /// <summary>
        /// Save read only fails
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SaveReadOnly2()
        {
            var doc = new XmlDocumentWithLocation(loadAsReadOnly: true);
#if FEATURE_XML_LOADPATH
            doc.Load(_pathToCommonTargets);
#else
            using (
                XmlReader xmlReader = XmlReader.Create(
                    _pathToCommonTargets,
                    new XmlReaderSettings {DtdProcessing = DtdProcessing.Ignore}))
            {
                doc.Load(xmlReader);
            }
#endif
            Assert.True(doc.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => {
                doc.Save(new MemoryStream());
            });
        }

        /// <summary>
        /// Save read only fails
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SaveReadOnly3()
        {
            var doc = new XmlDocumentWithLocation(loadAsReadOnly: true);
#if FEATURE_XML_LOADPATH
            doc.Load(_pathToCommonTargets);
#else
            using (
                XmlReader xmlReader = XmlReader.Create(
                    _pathToCommonTargets,
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
            {
                doc.Load(xmlReader);
            }
#endif
            Assert.True(doc.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() =>
            {
                doc.Save(new StringWriter());
            });
        }

        /// <summary>
        /// Save read only fails
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void SaveReadOnly4()
        {
            var doc = new XmlDocumentWithLocation(loadAsReadOnly: true);
#if FEATURE_XML_LOADPATH
            doc.Load(_pathToCommonTargets);
#else
            using (
                XmlReader xmlReader = XmlReader.Create(
                    _pathToCommonTargets,
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
            {
                doc.Load(xmlReader);
            }
#endif
            Assert.True(doc.IsReadOnly);
            using (XmlWriter wr = XmlWriter.Create(new FileStream(FileUtilities.GetTemporaryFile(), FileMode.Create)))
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    doc.Save(wr);
                });
            }
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
#if FEATURE_XML_LOADPATH
                doc.Load(file);
#else
                using (
                    XmlReader xmlReader = XmlReader.Create(
                        file,
                        new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
                {
                    doc.Load(xmlReader);
                }
#endif
                Assert.Equal(readOnly, doc.IsReadOnly);
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
