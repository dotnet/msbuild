// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Xml;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using Metadatum = System.Collections.Generic.KeyValuePair<string, string>;
using MetadataDictionary = System.Collections.Generic.Dictionary<string, string>;
using ItemDefinitionsDictionary = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ItemDefinitionLibrary_Tests
    {
        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void GetMetadataBeforeEvaluate()
        {
            XmlElement group = XmlTestUtilities.CreateBasicElement("ItemDefinitionGroup");

            ItemDefinitionLibrary library = new ItemDefinitionLibrary(new Project());
            library.Add(group);
            library.GetDefaultMetadataValue("ccompile", "defines");
        } 

        [Test]
        public void Basic()
        {
            XmlElement group = XmlTestUtilities.CreateBasicElement("ItemDefinitionGroup");
            XmlElement item = XmlTestUtilities.AddChildElement(group, "CCompile");
            XmlElement meta = XmlTestUtilities.AddChildElementWithInnerText(item, "Defines", "DEBUG");

            ItemDefinitionLibrary library = new ItemDefinitionLibrary(new Project());
            library.Add(group);
            library.Evaluate(null);

            Assertion.AssertEquals("DEBUG", library.GetDefaultMetadataValue("ccompile", "defines"));
        }        

        [Test]
        public void SameGroupTwoChildrenSameItemTypeDifferentMetadata()
        {
            XmlElement group = XmlTestUtilities.CreateBasicElement("ItemDefinitionGroup");
            XmlElement item1 = XmlTestUtilities.AddChildElement(group, "CCompile");
            XmlElement meta1 = XmlTestUtilities.AddChildElementWithInnerText(item1, "Defines", "DEBUG");
            XmlElement item2 = XmlTestUtilities.AddChildElement(group, "CCompile");
            XmlElement meta2 = XmlTestUtilities.AddChildElementWithInnerText(item1, "WarningLevel", "W4");

            ItemDefinitionLibrary library = new ItemDefinitionLibrary(new Project());
            library.Add(group);
            library.Evaluate(null);

            Assertion.AssertEquals("DEBUG", library.GetDefaultMetadataValue("ccompile", "defines"));
            Assertion.AssertEquals("W4", library.GetDefaultMetadataValue("ccompile", "warninglevel"));
        }

        [Test]
        public void SameGroupTwoChildrenDifferentItemType()
        {
            XmlElement group = XmlTestUtilities.CreateBasicElement("ItemDefinitionGroup");
            XmlElement item1 = XmlTestUtilities.AddChildElement(group, "CCompile");
            XmlElement meta1 = XmlTestUtilities.AddChildElementWithInnerText(item1, "Defines", "DEBUG");
            XmlElement item2 = XmlTestUtilities.AddChildElement(group, "CppCompile");
            XmlElement meta2 = XmlTestUtilities.AddChildElementWithInnerText(item2, "WarningLevel", "W4");

            ItemDefinitionLibrary library = new ItemDefinitionLibrary(new Project());
            library.Add(group);
            library.Evaluate(null);

            Assertion.AssertEquals("DEBUG", library.GetDefaultMetadataValue("ccompile", "defines"));
            Assertion.AssertEquals("W4", library.GetDefaultMetadataValue("CppCompile", "warninglevel"));
            Assertion.AssertEquals(null, library.GetDefaultMetadataValue("CppCompile", "defines"));
        }

        [Test]
        public void TwoGroups()
        {
            XmlElement group1 = XmlTestUtilities.CreateBasicElement("ItemDefinitionGroup");
            XmlElement item1 = XmlTestUtilities.AddChildElement(group1, "CCompile");
            XmlElement meta1 = XmlTestUtilities.AddChildElementWithInnerText(item1, "Defines", "DEBUG");
            XmlElement group2 = XmlTestUtilities.CreateBasicElement("ItemDefinitionGroup");
            XmlElement item2 = XmlTestUtilities.AddChildElement(group2, "CppCompile");
            XmlElement meta2 = XmlTestUtilities.AddChildElementWithInnerText(item2, "WarningLevel", "W4");

            ItemDefinitionLibrary library = new ItemDefinitionLibrary(new Project());
            library.Add(group1);
            library.Add(group2);
            library.Evaluate(null);

            Assertion.AssertEquals("DEBUG", library.GetDefaultMetadataValue("ccompile", "defines"));
            Assertion.AssertEquals("W4", library.GetDefaultMetadataValue("CppCompile", "warninglevel"));
            Assertion.AssertEquals(null, library.GetDefaultMetadataValue("CppCompile", "defines"));
        }

        [Test]
        public void PropertyInMetadataValue()
        {
            XmlElement group = XmlTestUtilities.CreateBasicElement("ItemDefinitionGroup");
            XmlElement item = XmlTestUtilities.AddChildElement(group, "CCompile");
            XmlElement meta = XmlTestUtilities.AddChildElementWithInnerText(item, "Defines", "$(p1)");

            ItemDefinitionLibrary library = new ItemDefinitionLibrary(new Project());
            library.Add(group);
            BuildPropertyGroup pg1 = new BuildPropertyGroup();
            pg1.SetProperty("p1", "v1");
            library.Evaluate(pg1);

            Assertion.AssertEquals("v1", library.GetDefaultMetadataValue("ccompile", "defines"));

            // Change the original property group -- should not affect the metadata value which was
            // already evaluated
            pg1.SetProperty("p1", "v1b");

            Assertion.AssertEquals("v1", library.GetDefaultMetadataValue("ccompile", "defines"));

            // Reevaluate with another property value
            BuildPropertyGroup pg2 = new BuildPropertyGroup();
            pg2.SetProperty("p1", "v2");
            library.Evaluate(pg2);

            Assertion.AssertEquals("v2", library.GetDefaultMetadataValue("ccompile", "defines"));
        }

        /// <summary>
        /// Verifies that, given metadata on an item definition, a corresponding item will pick up
        /// that item definition metadata.  
        /// </summary>
        [Test]
        public void ItemsPickUpItemDefinitionMetadata()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                <Project DefaultTargets=`t` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemDefinitionGroup>
                        <ItemWithDefinition>
                            <SomeMetadata>foo</SomeMetadata>
                        </ItemWithDefinition>
                    </ItemDefinitionGroup>
                    
                    <ItemGroup>
                        <ItemWithDefinition Include=`foo.cs` />
                    </ItemGroup>

                    <Target Name=`t`>
                        <Message Text=`[%(ItemWithDefinition.SomeMetadata)]` />
                    </Target>
                </Project>
            ");

            logger.AssertLogContains("[foo]");
        }

        /// <summary>
        /// Verifies that, given metadata on an item definition, a corresponding item will pick up
        /// that item definition metadata, even if the name of the item has since changed.  
        /// </summary>
        [Test]
        public void ItemsPickUpItemDefinitionMetadataWithTransforms()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                <Project DefaultTargets=`t` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <ItemDefinitionGroup>
                        <ItemWithDefinition>
                            <SomeMetadata>foo</SomeMetadata>
                        </ItemWithDefinition>
                    </ItemDefinitionGroup>
                    
                    <ItemGroup>
                        <ItemWithDefinition Include=`foo.cs` />
                    </ItemGroup>

                    <ItemGroup>
                        <TransformedItemWithDefinition Include=`@(ItemWithDefinition)` Condition=`true` />
                    </ItemGroup>

                    <Target Name=`t`>
                        <Message Text=`[%(TransformedItemWithDefinition.SomeMetadata)]` />
                    </Target>
                </Project>
            ");

            logger.AssertLogContains("[foo]");
        }

        /// <summary>
        /// Verifies that, given metadata on an item definition, a corresponding item will pick up
        /// that item definition metadata even if the definition is in a different project from the item.  
        /// </summary>
        [Test]
        public void ItemsPickUpItemDefinitionMetadataFromImportedProject()
        {
            try
            {
                ObjectModelHelpers.CreateFileInTempProjectDirectory("foo.proj", @"
                    <Project DefaultTargets=`t` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <ItemGroup>
                            <ItemWithDefinition Include=`foo.cs` />
                        </ItemGroup>

                        <Target Name=`t`>
                            <Message Text=`[%(ItemWithDefinition.SomeMetadata)]` />
                        </Target>

                        <Import Project=`foo2.proj` />
                    </Project>
                ");

                ObjectModelHelpers.CreateFileInTempProjectDirectory("foo2.proj", @"
                    <Project DefaultTargets=`t2` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <ItemDefinitionGroup>
                            <ItemWithDefinition>
                                <SomeMetadata>foo</SomeMetadata>
                            </ItemWithDefinition>
                        </ItemDefinitionGroup>

                        <Target Name=`t2` />
                    </Project>
                ");

                MockLogger logger = ObjectModelHelpers.BuildTempProjectFileExpectSuccess("foo.proj");

                logger.AssertLogContains("[foo]");

            }
            finally
            {
                ObjectModelHelpers.DeleteTempProjectDirectory();
            }
        }
    }
}
