// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using System.Xml;
using System.IO;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class PropertyTest
    {
        [Test]
        public void Constructors()
        {
            BuildProperty p1 = new BuildProperty("name", "value", PropertyType.NormalProperty);
            Assertion.AssertEquals("", p1.Name, "name");
            Assertion.AssertEquals("", p1.Value, "value");

            BuildProperty p2 = new BuildProperty("name", "value");
            Assertion.AssertEquals("", p2.Name, "name");
            Assertion.AssertEquals("", p2.Value, "value");

            BuildProperty p3 = new BuildProperty(new XmlDocument(), "name", "value", PropertyType.NormalProperty);
            Assertion.AssertEquals("", p3.Name, "name");
            Assertion.AssertEquals("", p3.Value, "value");

            BuildProperty p4 = new BuildProperty(null, "name", "value", PropertyType.NormalProperty);
            Assertion.AssertEquals("", p4.Name, "name");
            Assertion.AssertEquals("", p4.Value, "value");

            XmlDocument xmldoc = new XmlDocument();
            XmlElement xmlel = xmldoc.CreateElement("name");
            xmlel.InnerXml = "value";

            BuildProperty p5 = new BuildProperty(xmlel, PropertyType.NormalProperty);
            Assertion.AssertEquals("", p5.Name, "name");
            Assertion.AssertEquals("", p5.Value, "value");
        }

        [Test]
        public void SetValueForPropertyInXMLDoc()
        {
            XmlDocument xmldoc = new XmlDocument();
            XmlElement xmlel = xmldoc.CreateElement("name");
            xmlel.InnerXml = "value";

            BuildProperty p1 = new BuildProperty(xmlel, PropertyType.NormalProperty);
            Assertion.AssertEquals("", p1.Value, "value");

            p1.Value = "modified value";
            Assertion.AssertEquals("", p1.Value, "modified value");
            Assertion.AssertEquals("", xmlel.InnerXml, "modified value");
            Assertion.AssertEquals("", xmlel.InnerText, "modified value");

            p1.Value = "modified <value/>";
            Assertion.AssertEquals("", p1.Value, "modified <value />");
            Assertion.AssertEquals("", xmlel.InnerXml, "modified <value />");
            Assertion.AssertEquals("", xmlel.InnerText, "modified ");

            p1.Value = "modified & value";
            Assertion.AssertEquals("", p1.Value, "modified & value");
            Assertion.AssertEquals("", xmlel.InnerXml, "modified &amp; value");
            Assertion.AssertEquals("", xmlel.InnerText, "modified & value");

            p1.Value = "modified &lt;value/&gt;";
            Assertion.AssertEquals("", p1.Value, "modified &lt;value/&gt;");
            Assertion.AssertEquals("", xmlel.InnerXml, "modified &amp;lt;value/&amp;gt;");
            Assertion.AssertEquals("", xmlel.InnerText, "modified &lt;value/&gt;");
        }

        [Test]
        public void SetValueForPropertyWithoutXMLDoc()
        {
            BuildProperty p1 = new BuildProperty("name", "value");
            Assertion.AssertEquals("", p1.Value, "value");

            p1.Value = "modified value";
            Assertion.AssertEquals("", p1.Value, "modified value");
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemGroupInAPropertyCondition()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"

                <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <x Include=`x1`/>
                    </ItemGroup>

                    <PropertyGroup>
                        <a Condition=`@(x)=='x1'`>@(x)</a>
                    </PropertyGroup>

                    <Target Name=`t`>
                        <Message Text=`[$(a)]`/>
                    </Target>

                </Project>

            ");

            p.Build(new string[] { "t" }, null);
        }

        /// <summary>
        /// Verify we can't create properties with reserved names in projects
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void ReservedPropertyNameInProject()
        {
            bool fExceptionCaught = false;
            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject
                (
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "  <PropertyGroup Condition=\"false\"><MSBuildBinPath/></PropertyGroup>"
                    + "  <Target Name=\"t\">"
                    + "    <Message Text=\"aa\"/>"
                    + "  </Target>"
                    + "</Project>"
                );
            }
            catch (InvalidProjectFileException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create properties with invalid names in projects
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidPropertyNameInProject()
        {
            bool fExceptionCaught = false;
            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject
                (
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "  <PropertyGroup Condition=\"false\"><Choose/></PropertyGroup>"
                    + "  <Target Name=\"t\">"
                    + "    <Message Text=\"aa\"/>"
                    + "  </Target>"
                    + "</Project>"
                );
            }
            catch (InvalidProjectFileException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify invalid property names are caught, where the names are valid Xml Element names.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void InvalidCharInPropertyNameInProject()
        {
            bool exceptionCaught = false;
            try
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject
                (
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
                    + "  <PropertyGroup Condition=\"false\"><\u03A3/></PropertyGroup>"
                    + "  <Target Name=\"t\">"
                    + "    <Message Text=\"aa\"/>"
                    + "  </Target>"
                    + "</Project>"
                );
            }
            catch (InvalidProjectFileException)
            {
                exceptionCaught = true;
            }
            Assertion.Assert(exceptionCaught);
        }

        /// <summary>
        /// Verify we can't create properties with invalid names directly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidPropertyNameDirectPrivateCreate()
        {
            bool fExceptionCaught = false;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<Choose/>");
                XmlElement element = doc.DocumentElement;
                BuildProperty property = new BuildProperty(doc.DocumentElement, PropertyType.ReservedProperty);
            }
            catch (InvalidProjectFileException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create properties with invalid names directly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidPropertyNameDirectPublicCreate()
        {
            bool fExceptionCaught = false;
            try
            {
                BuildProperty property = new BuildProperty("Choose", "to be or not to be");
            }
            catch (InvalidOperationException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create properties with invalid names directly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidPropertyNameDirectPublicCreate2()
        {
            bool fExceptionCaught = false;
            try
            {
                BuildProperty property = new BuildProperty("Choose", "to be or not to be", PropertyType.ReservedProperty);
            }
            catch (InvalidOperationException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify we can't create properties with invalid names directly
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void InvalidPropertyNameDirectPublicCreate3()
        {
            bool fExceptionCaught = false;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<Choose/>");
                BuildProperty property = new BuildProperty(doc, "Choose", "value", PropertyType.ReservedProperty);
            }
            catch (InvalidOperationException)
            {
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// Verify some valid property names are accepted
        /// </summary>
        [Test]
        public void ValidName()
        {
            foreach (string candidate in Item_Tests.validItemPropertyMetadataNames)
            {
                TryValidPropertyName(candidate);
            }
        }

        /// <summary>
        /// Verify invalid property names are rejected
        /// </summary>
        [Test]
        public void InvalidNames()
        {
            foreach (string candidate in Item_Tests.invalidItemPropertyMetadataNames)
            {
                TryInvalidPropertyName(candidate);
            }

            // For the other BuildProperty ctor, it has to be an xml-valid name since it takes an element
            // Just try one case
            XmlDocument doc = new XmlDocument();
            XmlElement element = doc.CreateElement("foo.bar");
            element.InnerText = "foo";
            bool caughtException = false;
            try
            {
                BuildProperty item = new BuildProperty(element, PropertyType.NormalProperty);
            }
            catch (InvalidProjectFileException ex)
            {
                Console.WriteLine(ex.Message);
                caughtException = true;
            }
            Assertion.Assert("foo.bar", caughtException);
        }

        /// <summary>
        /// Helper for trying invalid property names
        /// </summary>
        /// <param name="name"></param>
        private void TryInvalidPropertyName(string name)
        {
            XmlDocument doc = new XmlDocument();
            bool caughtException = false;

            // Test the first BuildProperty ctor
            try
            {
                BuildProperty item = new BuildProperty(doc, name, "someValue", PropertyType.NormalProperty);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                caughtException = true;
            }
            Assertion.Assert(name, caughtException);
        }

        /// <summary>
        /// Helper for trying valid property names
        /// </summary>
        /// <param name="name"></param>
        private void TryValidPropertyName(string name)
        {
            XmlDocument doc = new XmlDocument();

            BuildProperty property = new BuildProperty(doc, name, "someValue", PropertyType.NormalProperty);
            Assertion.AssertEquals(name, property.Name);
            Assertion.AssertEquals("someValue", property.Value);
        }

        [Test]
        public void TestCustomSerialization()
        {
            BuildProperty p1 = new BuildProperty("name", "value", PropertyType.GlobalProperty);
            BuildProperty p2 = new BuildProperty("name2", "value2", PropertyType.OutputProperty);
            BuildProperty p3 = new BuildProperty("name3", "value3", PropertyType.EnvironmentProperty);

            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                stream.Position = 0;
                p1.WriteToStream(writer);
                p2.WriteToStream(writer);
                p3.WriteToStream(writer);
                long streamWriteEndPosition = stream.Position;

                stream.Position = 0;
                BuildProperty p4 = BuildProperty.CreateFromStream(reader);
                BuildProperty p5 = BuildProperty.CreateFromStream(reader);
                BuildProperty p6 = BuildProperty.CreateFromStream(reader);
                long streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream end positions should be equal");
                CompareBuildProperty(p1, p4);
                CompareBuildProperty(p2, p5);
                CompareBuildProperty(p3, p6);
            }
            finally
            {
                reader.Close();
                writer = null;
                stream = null;
            }
        }

        /// <summary>
        /// Often a property has a value and an expanded value that are string identical. In such cases,
        /// we should not transmit both across the wire, and at the other end the property should end up
        /// with reference identical values for each, saving memory too.
        /// </summary>
        [Test]
        public void TestCustomSerializationCompressesPropertyValueAndExpandedValue()
        {
            // Create a non-literal string, so the CLR won't intern it (in real builds,
            // the strings are not literals, and the CLR won't intern them)
            string v1 = "non_expandable_property" + new Random().Next();
            int i = new Random().Next();
            string v2 = "expandable_$(property)" + i;
            string v2Expanded = "expandable_" + i;

            // Verify it is not interned
            Assertion.Assert(null == String.IsInterned(v1));
            Assertion.Assert(null == String.IsInterned(v2));

            // Property with finalValue == Value
            BuildProperty p = new BuildProperty("name", v1);
            Assertion.Assert(Object.ReferenceEquals(p.FinalValueEscaped, p.Value));

            // Property with finalValue != Value
            BuildProperty q = new BuildProperty("name", v2);
            q.Evaluate(new Expander(new BuildPropertyGroup()));
            Assertion.Assert(!Object.ReferenceEquals(q.FinalValueEscaped, q.Value));
            Assertion.AssertEquals(v2, q.Value);
            Assertion.AssertEquals(v2Expanded, q.FinalValueEscaped);

            // "Transmit across the wire"
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                stream.Position = 0;
                p.WriteToStream(writer);
                q.WriteToStream(writer);

                // The property that had identical value and finalvalueescaped should
                // be deserialized into a property that has identical references for its
                // value and finalvalueescaped.
                stream.Position = 0;
                BuildProperty p2 = BuildProperty.CreateFromStream(reader);
                Assertion.Assert(Object.ReferenceEquals(p2.FinalValueEscaped, p2.Value));
                Assertion.AssertEquals(v1, p2.Value);

                // The property that had different value and finalvalueescaped should be deserialized
                // normally
                BuildProperty q2 = BuildProperty.CreateFromStream(reader);
                Assertion.Assert(!Object.ReferenceEquals(q2.FinalValueEscaped, q2.Value));
                Assertion.AssertEquals(v2, q2.Value);
                Assertion.AssertEquals(v2Expanded, q2.FinalValueEscaped);
            }
            finally
            {
                reader.Close();
            }
        }

        private static void CompareBuildProperty(BuildProperty a, BuildProperty b)
        {
            Assert.IsTrue(string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase) == 0, "PropertyValue should be equal");
            Assert.IsTrue(string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) == 0, "Name should be equal");
            Assert.IsTrue(string.Compare(a.FinalValueEscaped, b.FinalValueEscaped, StringComparison.OrdinalIgnoreCase) == 0, "FinalValueEscaped should be equal");
            Assert.AreEqual(a.Type, b.Type, "Type should be equal");
        }
    }
}
