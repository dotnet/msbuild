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
using System.IO;
using System.Reflection;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class BuildItemDefinitionGroupXml_Tests
    {
        [Test]
        public void Basic()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);

            Assertion.AssertEquals(1, definitions.GetDefaultedMetadataCount("CCompile"));
            Assertion.AssertEquals("DEBUG", definitions.GetDefaultMetadataValue("CCompile", "Defines"));
        }

        [Test]
        public void DuplicateMetadataLastOneWins()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlElement meta2 = XmlTestUtilities.AddChildElement(group.ChildNodes[0], "Defines");
            meta2.InnerText = "RETAIL";
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);

            Assertion.AssertEquals(1, definitions.GetDefaultedMetadataCount("CCompile"));
            Assertion.AssertEquals("RETAIL", definitions.GetDefaultMetadataValue("CCompile", "Defines"));
        }

        [Test]
        public void NoDefinitions()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            group.RemoveAll();
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);

            // No exception
            Assertion.AssertEquals(0, definitions.GetDefaultedMetadataCount("arbitrary"));
        }

        [Test]
        public void NoMetadata()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            group.ChildNodes[0].RemoveAll();
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);

            // No exception
            Assertion.AssertEquals(0, definitions.GetDefaultedMetadataCount("arbitrary"));
        }

        [Test]
        public void FalseConditionOnGroup()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlTestUtilities.AddAttribute(group, "Condition", "'v2'=='$(p1)'");
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);

            // No exception
            Assertion.AssertEquals(0, definitions.GetDefaultedMetadataCount("arbitrary"));
        }

        [Test]
        public void FalseConditionOnDefinition()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlTestUtilities.AddAttribute(group.ChildNodes[0], "Condition", "'v2'=='$(p1)'");
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);

            // No exception
            Assertion.AssertEquals(0, definitions.GetDefaultedMetadataCount("arbitrary"));
        }

        [Test]
        public void FalseConditionOnMetadatum()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlTestUtilities.AddAttribute(group.ChildNodes[0].ChildNodes[0], "Condition", "'v2'=='$(p1)'");
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);

            // No exception
            Assertion.AssertEquals(0, definitions.GetDefaultedMetadataCount("arbitrary"));
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidAttributeOnGroup()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlTestUtilities.AddAttribute(group, "XXXX", "YYY");
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidIncludeAttributeOnDefinition()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlTestUtilities.AddAttribute(group.ChildNodes[0], "Include", "YYY");
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidExcludeAttributeOnDefinition()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlTestUtilities.AddAttribute(group.ChildNodes[0], "Exclude", "YYY");
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidRemoveAttributeOnDefinition()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlTestUtilities.AddAttribute(group.ChildNodes[0], "Remove", "YYY");
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidAttributeOnMetadatum()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlTestUtilities.AddAttribute(group.ChildNodes[0].ChildNodes[0], "XXXX", "YYY");
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);
        }

        [Test]
        public void ExpandPropertiesInMetadatumValue()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            group["CCompile"]["Defines"].InnerText = "A_$(p1)_$(p2)_B";
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);

            Assertion.AssertEquals(1, definitions.GetDefaultedMetadataCount("CCompile"));
            Assertion.AssertEquals("A_v1__B", definitions.GetDefaultMetadataValue("CCompile", "Defines"));
        }

        [Test]
        public void TrueConditionOnEverything()
        {
            XmlElement group = GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum();
            XmlTestUtilities.AddAttribute(group.ChildNodes[0], "Condition", "'v1'=='$(p1)'");
            XmlTestUtilities.AddAttribute(group.ChildNodes[0].ChildNodes[0], "Condition", "'v1'=='$(p1)'");
            XmlTestUtilities.AddAttribute(group.ChildNodes[0].ChildNodes[0], "Condition", "'v1'=='$(p1)'");
            ItemDefinitionLibrary definitions = NewAndEvaluateItemDefinitionLibraryXml(group);

            Assertion.AssertEquals(1, definitions.GetDefaultedMetadataCount("CCompile"));
            Assertion.AssertEquals("DEBUG", definitions.GetDefaultMetadataValue("CCompile", "Defines"));
        }

        #region Project tests

        [Test]
        public void BasicItemDefinitionInProject()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <CppCompile Include='a.cpp'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines>DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <CppCompile Include='b.cpp'/>
                  </ItemGroup>
                  <Target Name=`t`>
                    <Message Text=`[%(CppCompile.Identity)==%(CppCompile.Defines)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[a.cpp==DEBUG]", "[b.cpp==DEBUG]");
        }

        [Test]
        public void EscapingInItemDefinitionInProject()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup Condition=`'%24'=='$'`>
                    <i Condition=`'%24'=='$'`>
                      <m Condition=`'%24'=='$'`>%24(xyz)</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[$(xyz)]");
        }


        [Test]
        public void ItemDefinitionForOtherItemType()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <j>
                      <m>m1</m>
                    </j>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[]");
        }

        [Test]
        public void RedefinitionLastOneWins()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i>
                      <m>m2</m>
                      <o>o1</o>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)-%(i.n)-%(i.o)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[m2-n1-o1]");
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemExpressionInDefaultMetadataValueErrors()
        {
            // We don't allow item expressions on an ItemDefinitionGroup because there are no items when IDG is evaluated.
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>@(x)</m>
                    </i>
                  </ItemDefinitionGroup> 
                </Project>
            ", logger);
            p.Build("t");
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void UnqualifiedMetadataConditionOnItemDefinitionGroupErrors()
        {
            // We don't allow unqualified metadata on an ItemDefinitionGroup because we don't know what item type it refers to.
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup Condition=`'%(m)'=='m1'`/>
                </Project>
            ", logger);
            p.Build("t");
        }

        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void QualifiedMetadataConditionOnItemDefinitionGroupErrors()
        {
            // We don't allow qualified metadata because it's not worth distinguishing from unqualified, when you can just move the condition to the child.
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup Condition=`'%(x.m)'=='m1'`/>
                </Project>
            ", logger);
            p.Build("t");
        }

        [Test]
        public void MetadataConditionOnItemDefinition()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                    <j Include='j1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                    <j>
                      <n>n1</n>
                    </j>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i Condition=`'%(m)'=='m1'`>
                      <m>m2</m>
                    </i>
                    <!-- verify j metadata is distinct -->
                    <j Condition=`'%(j.n)'=='n1' and '%(n)'=='n1'`>
                      <n>n2</n>   
                    </j>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                    <Message Text=`[%(j.n)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[m2]", "[n2]");
        }

        [Test]
        public void QualifiedMetadataConditionOnItemDefinitionBothQualifiedAndUnqualified()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i Condition=`'%(i.m)'=='m1' and '%(m)'=='m1'`>
                      <m>m2</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[m2]");
        }

        [Test]
        public void FalseMetadataConditionOnItemDefinitionBothQualifiedAndUnqualified()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i Condition=`'%(m)'=='m2' or '%(i.m)'!='m1'`>
                      <m>m3</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[m1]");
        }

        [Test]
        public void MetadataConditionOnItemDefinitionChildBothQualifiedAndUnqualified()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i>
                      <m Condition=`'%(m)'=='m1' and '%(n)'=='n1' and '%(i.m)'=='m1'`>m2</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[m2]");
        }

        [Test]
        public void FalseMetadataConditionOnItemDefinitionChildBothQualifiedAndUnqualified()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i>
                      <m Condition=`'%(m)'=='m2' or !('%(n)'=='n1') or '%(i.m)' != 'm1'`>m3</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[m1]");
        }

        [Test]
        public void MetadataConditionOnItemDefinitionAndChildQualifiedWithUnrelatedItemType()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemDefinitionGroup>
                    <i Condition=`'%(j.m)'=='' and '%(j.m)'!='x'`>
                      <m Condition=`'%(j.m)'=='' and '%(j.m)'!='x'`>m2</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[m2]");
        }

        /// <summary>
        /// Make ItemDefinitionGroup inside a target produce a nice error.
        /// It will normally produce an error due to the invalid child tag, but 
        /// we want to error even if there's no child tag. This will make it 
        /// easier to support it inside targets in a future version.
        /// </summary>
        [Test]
        public void ItemDefinitionInTargetErrors()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t`>
                    <ItemDefinitionGroup/>
                  </Target>
                </Project>
            ", logger);
            bool result = p.Build("t");

            Assertion.AssertEquals(false, result);
            logger.AssertLogContains("MSB4163");
        }

        // Verify that anyone with a task named "ItemDefinitionGroup" can still
        // use it by fully qualifying the name.
        [Test]
        public void ItemDefinitionGroupTask()
        {
            MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <UsingTask TaskName=`ItemDefinitionGroup` AssemblyFile=`{0}`/>
                        <Target Name=`Build`>
                            <Microsoft.Build.UnitTests.ItemDefinitionGroup/>
                        </Target>
                    </Project>
               ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            Assertion.Assert(ml.FullLog.Contains("In ItemDefinitionGroup task."));
        }

        [Test]
        public void MetadataOnItemWins()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <CppCompile Include='a.cpp'>
                      <Defines>RETAIL</Defines>
                    </CppCompile>
                    <CppCompile Include='b.cpp'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines>DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(CppCompile.Identity)==%(CppCompile.Defines)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[a.cpp==RETAIL]", "[b.cpp==DEBUG]");
        }

        [Test]
        public void MixtureOfItemAndDefaultMetadata()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <CppCompile Include='a.cpp'>
                      <WarningLevel>4</WarningLevel>
                    </CppCompile>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines>DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(CppCompile.Identity)==%(CppCompile.Defines)]`/>
                    <Message Text=`[%(CppCompile.Identity)==%(CppCompile.WarningLevel)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[a.cpp==DEBUG]", "[a.cpp==4]");
        }

        [Test]
        public void IntrinsicTaskModifyingDefaultMetadata()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <ItemGroup>
                      <i>
                        <m>m2</m>
                      </i>
                    </ItemGroup>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[m2]");

            BuildItemGroup group = p.GetEvaluatedItemsByName("i");
            BuildItem item = group[0];
            string metadataValue = item.GetMetadata("m");
            Assertion.AssertEquals("m2", metadataValue);

            p.ResetBuildStatus();

            // Should go back to definition
            group = p.GetEvaluatedItemsByName("i");
            item = group[0];
            metadataValue = item.GetMetadata("m");
            Assertion.AssertEquals("m1", metadataValue);
        }

        [Test]
        public void IntrinsicTaskConsumingDefaultMetadata()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <ItemGroup>
                      <i Condition=`'%(i.m)'=='m1'`>
                        <n Condition=`'%(m)'=='m1'`>n2</n>
                      </i>
                    </ItemGroup>
                    <Message Text=`[%(i.n)]`/>
                  </Target>
                </Project>
            ", logger);
            p.Build("t");

            logger.AssertLogContains("[n2]");
        }

        [Test]
        public void DefinitionInImportedFile()
        {
            MockLogger logger = new MockLogger();
            string importedFile = null;

            try
            {
                importedFile = Path.GetTempFileName();
                File.WriteAllText(importedFile, @"
                <Project ToolsVersion='3.5' xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines>DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                </Project>
            ");
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                      <ItemGroup>
                        <CppCompile Include='a.cpp'/>                      
                      </ItemGroup>
                      <Import Project='" + importedFile + @"'/>
                      <Target Name=`t`>
                        <Message Text=`[%(CppCompile.Identity)==%(CppCompile.Defines)]`/>
                      </Target>
                    </Project>
                ", logger);
                p.Build("t");

                logger.AssertLogContains("[a.cpp==DEBUG]");
            }
            finally
            {
                ObjectModelHelpers.DeleteTempFiles(new string[] { importedFile });
            }
        }

        /// <summary>
        /// Item added to project should pick up the item
        /// definitions that project has.
        [Test]
        public void ProjectAddNewItemPicksUpProjectItemDefinitions()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                </Project>
                ");

            BuildItem item = p.AddNewItem("i", "i1");

            Assertion.AssertEquals("m1", item.GetEvaluatedMetadata("m"));
        }

        /// <summary>
        /// Item added to project should pick up the item
        /// definitions that project has.
        [Test]
        public void ProjectAddNewItemExistingGroupPicksUpProjectItemDefinitions()
        {
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <i Include='i2'>
                      <m>m2</m>
                    </i>
                  </ItemGroup>
                </Project>
                ");

            BuildItem item1 = p.EvaluatedItems[0];
            BuildItem item2a = p.AddNewItem("i", "i1");
            BuildItem item2b = p.EvaluatedItems[0];

            Assertion.AssertEquals("m2", item1.GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("m1", item2a.GetEvaluatedMetadata("m"));
            Assertion.AssertEquals("m1", item2b.GetEvaluatedMetadata("m"));
        }

        [Test]
        public void ItemsEmittedByTaskPickUpItemDefinitions()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <CreateItem Include=`i1` AdditionalMetadata=`n=n2`>
                      <Output ItemName=`i` TaskParameter=`Include`/>
                    </CreateItem>
                    <Message Text=`[%(i.m)][%(i.n)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[m1][n2]");
        }

        [Test]
        public void ItemsEmittedByIntrinsicTaskPickUpItemDefinitions()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <ItemGroup>
                      <i Include=`i1`>
                        <n>n2</n>
                      </i>
                    </ItemGroup>
                    <Message Text=`[%(i.m)][%(i.n)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[m1][n2]");
        }

        [Test]
        public void MutualReferenceToDefinition1()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>~%(m)~</n>
                    </i>
                  </ItemDefinitionGroup> 
                    <ItemGroup>
                      <i Include=`i1`/>
                    </ItemGroup>   
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)][%(i.n)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[m1][~m1~]");
        }

        [Test]
        public void MutualReferenceToDefinition2()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>~%(n)~</m>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                    <ItemGroup>
                      <i Include=`i1`/>
                    </ItemGroup>   
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)][%(i.n)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[~~][n1]");
        }

        [Test]
        public void MutualReferenceToDefinition3()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                      <n>%(i.m)</n>
                      <o>%(j.m)</o>
                    </i>
                  </ItemDefinitionGroup> 
                    <ItemGroup>
                      <i Include=`i1`/>
                    </ItemGroup>   
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)][%(i.n)][%(i.o)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[m1][m1][]");
        }

        [Test]
        public void ProjectReevaluationReevaluatesItemDefinitions()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <PropertyGroup>
                    <Defines>CODEANALYSIS</Defines>
                  </PropertyGroup>
                  <ItemGroup>
                    <CppCompile Include='a.cpp'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <CppCompile>
                      <Defines Condition=`'$(BuildFlavor)'=='ret'`>$(Defines);RETAIL</Defines>
                      <Defines Condition=`'$(BuildFlavor)'=='chk'`>$(Defines);DEBUG</Defines>
                    </CppCompile>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(CppCompile.Identity)==%(CppCompile.Defines)]`/>
                  </Target>
                </Project>
            ", logger);

            p.SetProperty("BuildFlavor", "ret");

            p.Build("t");

            logger.AssertLogContains("[a.cpp==CODEANALYSIS;RETAIL]");

            BuildItemGroup group = p.GetEvaluatedItemsByName("CppCompile");
            BuildItem item = group[0];
            string metadataValue = item.GetMetadata("Defines");
            Assertion.AssertEquals("CODEANALYSIS;RETAIL", metadataValue);

            p.SetProperty("BuildFlavor", "chk");

            group = p.GetEvaluatedItemsByName("CppCompile");
            item = group[0];
            metadataValue = item.GetMetadata("Defines");

            Assertion.AssertEquals("CODEANALYSIS;DEBUG", metadataValue);
        }

        [Test]
        public void MSBuildCallDoesNotAffectCallingProjectsDefinitions()
        {
            string otherProject = null;

            try
            {
                otherProject = Path.GetTempFileName();
                string otherProjectContent = @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m2</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"">
                    <Message Text=""[CHILD:%(i.m)]""/>
                  </Target>
                </Project>";

                using (StreamWriter writer = new StreamWriter(otherProject))
                {
                    writer.Write(otherProjectContent);
                }

                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include='i1'/>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=`t`>
                    <Message Text=`[PARENT-before:%(i.m)]`/>
                    <MSBuild Projects=`" + otherProject + @"`/>
                    <Message Text=`[PARENT-after:%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);

                p.Build("t");

                logger.AssertLogContains("[PARENT-before:m1]", "[CHILD:m2]", "[PARENT-after:m1]");
            }
            finally
            {
                File.Delete(otherProject);
            }
        }

        [Test]
        public void DefaultMetadataTravelWithTargetOutputs()
        {
            string otherProject = null;

            try
            {
                otherProject = Path.GetTempFileName();
                string otherProjectContent = @"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                  <ItemGroup>
                    <i Include='i1'>
                       <m>m1</m>
                    </i>
                  </ItemGroup>
                  <ItemDefinitionGroup>
                    <i>
                      <n>n1</n>
                    </i>
                  </ItemDefinitionGroup> 
                  <Target Name=""t"" Outputs=""@(i)"">
                    <Message Text=""[CHILD:%(i.Identity):m=%(i.m),n=%(i.n)]""/>
                  </Target>
                </Project>";

                using (StreamWriter writer = new StreamWriter(otherProject))
                {
                    writer.Write(otherProjectContent);
                }

                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                  <Target Name=`t`>
                    <MSBuild Projects=`" + otherProject + @"`>
                       <Output TaskParameter='TargetOutputs' ItemName='i'/>
                    </MSBuild>
                    <Message Text=`[PARENT:%(i.Identity):m=%(i.m),n=%(i.n)]`/>
                  </Target>
                </Project>
            ", logger);

                p.Build("t");

                logger.AssertLogContains("[CHILD:i1:m=m1,n=n1]", "[PARENT:i1:m=m1,n=n1]");
            }
            finally
            {
                File.Delete(otherProject);
            }
        }

       #endregion

        #region Helpers

        private static ItemDefinitionLibrary NewAndEvaluateItemDefinitionLibraryXml(XmlElement group)
        {
            ItemDefinitionLibrary library = new ItemDefinitionLibrary(new Project());
            library.Add(group);

            BuildPropertyGroup properties = new BuildPropertyGroup();
            properties.SetProperty("p1", "v1");            
            library.Evaluate(properties);

            return library;
        }

        internal static XmlElement GetBasicItemDefinitionGroupWithOneDefinitionAndOneMetadatum()
        {
            XmlElement group = XmlTestUtilities.CreateBasicElement("ItemDefinitionGroup");
            XmlElement item = XmlTestUtilities.AddChildElement(group, "CCompile");
            XmlTestUtilities.AddChildElementWithInnerText(item, "Defines", "DEBUG");
            return group;
        }

        #endregion
    }

    public class ItemDefinitionGroup : Microsoft.Build.Utilities.Task
    {
        public override bool Execute()
        {
            Log.LogMessage("In ItemDefinitionGroup task.");
            return true;
        }
    }
}
