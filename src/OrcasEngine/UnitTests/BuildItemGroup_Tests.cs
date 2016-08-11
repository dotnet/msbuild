// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Xml;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class BuildItemGroup_Tests
    {
        [Test]
        public void ParameterlessConstructor()
        {
            BuildItemGroup group = new BuildItemGroup();
            Assertion.AssertEquals(String.Empty, group.Condition);
            Assertion.AssertEquals(false, group.IsPersisted);
            Assertion.AssertEquals(0, group.Count);
            Assertion.AssertEquals(false, group.IsImported);
        }

        [Test]
        public void XmlDocConstructor()
        {
            XmlDocument doc = new XmlDocument();
            BuildItemGroup group = new BuildItemGroup(doc, true, new Project());
            Assertion.AssertEquals(String.Empty, group.Condition);
            Assert.AreNotEqual(null, group.ItemGroupElement);
            Assertion.AssertEquals(0, group.Count);
            Assertion.AssertEquals(true, group.IsImported);
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void XmlDocConstructor2()
        {
            BuildItemGroup group = new BuildItemGroup((XmlDocument)null, true, new Project());
        }

        [Test]
        public void XmlElementConstructor()
        {
            XmlElement ig = CreatePersistedItemGroupElement();

            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());
            Assertion.AssertEquals("c", group.Condition);
            Assertion.AssertEquals(ig, group.ItemGroupElement);
            Assertion.AssertEquals(ig.ParentNode, group.ParentElement);
            Assertion.AssertEquals(2, group.Count);
            Assertion.AssertEquals(false, group.IsImported);
            Assertion.AssertEquals("ci1", group[0].Condition);
            Assertion.AssertEquals("i1", group[0].Include);
            Assertion.AssertEquals("ci2", group[1].Condition);
            Assertion.AssertEquals("i2", group[1].Include);
        }

        private static XmlElement CreatePersistedItemGroupElement()
        {
            XmlDocument doc = new XmlDocument();
            XmlElement ig = doc.CreateElement("ItemGroup", XMakeAttributes.defaultXmlNamespace);
            XmlAttribute condition = doc.CreateAttribute("Condition");
            condition.Value = "c";
            ig.SetAttributeNode(condition);

            XmlElement item1 = doc.CreateElement("i", XMakeAttributes.defaultXmlNamespace);
            XmlAttribute condition1 = doc.CreateAttribute("Condition");
            condition1.Value = "ci1";
            item1.SetAttributeNode(condition1);
            XmlAttribute include1 = doc.CreateAttribute("Include");
            include1.Value = "i1";
            item1.SetAttributeNode(include1);
            ig.AppendChild(item1);

            XmlElement item2 = doc.CreateElement("i", XMakeAttributes.defaultXmlNamespace);
            XmlAttribute condition2 = doc.CreateAttribute("Condition");
            condition2.Value = "ci2";
            item2.SetAttributeNode(condition2);
            XmlAttribute include2 = doc.CreateAttribute("Include");
            include2.Value = "i2";
            item2.SetAttributeNode(include2);
            ig.AppendChild(item2);
            return ig;
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void XmlElementConstructor2()
        {
            BuildItemGroup group = new BuildItemGroup((XmlElement)null, true, new Project());
        }


        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void XmlElementConstructor3()
        {
            XmlDocument doc = new XmlDocument();
            XmlElement ig = doc.CreateElement("ItemGroup", XMakeAttributes.defaultXmlNamespace);
            XmlAttribute a = doc.CreateAttribute("x");
            ig.SetAttributeNode(a);

            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());
        }

        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void XmlElementConstructor4()
        {
            XmlDocument doc = new XmlDocument();
            XmlElement ig = doc.CreateElement("x", XMakeAttributes.defaultXmlNamespace);
            BuildItemGroup group = new BuildItemGroup(ig, true, new Project());
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetConditionOnVirtualGroup()
        {
            BuildItemGroup group = new BuildItemGroup();
            group.Condition = "x";
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetConditionOnImportedGroup()
        {
            XmlDocument doc = new XmlDocument();
            BuildItemGroup group = new BuildItemGroup(doc, true, new Project());
            group.Condition = "x";
        }

        [Test]
        public void SetNullCondition()
        {
            XmlDocument doc = new XmlDocument();
            BuildItemGroup group = new BuildItemGroup(doc, false, new Project());
            group.Condition = null;
            Assertion.AssertEquals(String.Empty, group.Condition);
        }

        [Test]
        public void SetEmptyCondition()
        {
            XmlDocument doc = new XmlDocument();
            BuildItemGroup group = new BuildItemGroup(doc, false, new Project());
            group.Condition = String.Empty;
            Assertion.AssertEquals(String.Empty, group.Condition);
        }

        [Test]
        public void SetCondition()
        {
            XmlDocument doc = new XmlDocument();
            BuildItemGroup group = new BuildItemGroup(doc, false, new Project());
            group.Condition = "x";
            Assertion.AssertEquals("x", group.Condition);
        }

        [Test]
        public void ToArray()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());
            BuildItem[] array = group.ToArray();

            Assertion.AssertEquals(2, array.Length);
            Assertion.AssertEquals("i1", array[0].Include);
            Assertion.AssertEquals("i2", array[1].Include);
        }

        [Test]
        public void Enumerator()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            List<BuildItem> items = new List<BuildItem>();
            foreach (BuildItem item in group)
            {
                items.Add(item);
            }

            Assertion.AssertEquals(2, items.Count);
            Assertion.AssertEquals("i1", items[0].Include);
            Assertion.AssertEquals("i2", items[1].Include);
        }

        [Test]
        public void AddNewItem1()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            group.AddNewItem("j", "j1");
            group.AddNewItem("j", "j2;", true /*literal*/);

            Assertion.AssertEquals(4, group.Count);
            Assertion.AssertEquals("j1", group[2].Include);
            Assertion.AssertEquals("j2%3b", group[3].Include);
        }

        /// <summary>
        /// Adding an existing item should cause it to pick up
        /// the project's item definitions
        /// </summary>
        [Test]
        public void AddExistingItemAt()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                </Project>
            ", logger);

            BuildItemGroup group = p.AddNewItemGroup();
            BuildItem item = new BuildItem("i", "i1");
            group.AddExistingItemAt(0, item);
            Expander expander = new Expander(new BuildPropertyGroup());
            item.EvaluateAllItemMetadata(expander, ParserOptions.AllowPropertiesAndItemLists, null, null);

            Assertion.AssertEquals("m1", item.GetMetadata("m"));
        }

        /// <summary>
        /// Metadata should be able to refer to metadata above
        /// </summary>
        [Test]
        public void MutualReferenceToMetadata()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`i1`>
                      <m>m1</m>
                      <m>%(m);m2</m>
                      <m Condition='false'>%(m);m3</m> 
                    </i>
                  </ItemGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[m1;m2]");
        }

        /// <summary>
        /// Metadata should be able to refer to metadata above
        /// </summary>
        [Test]
        public void MutualReferenceToMetadataQualified()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`i1`>
                      <m>m1</m>
                      <m>%(i.m);m2</m>
                      <m Condition='false'>%(m);m3</m> 
                    </i>
                  </ItemGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[m1;m2]");
        }

        /// <summary>
        /// Metadata should be able to refer to metadata above
        /// </summary>
        [Test]
        public void MutualReferenceToMetadataMixed()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <l>l1</l>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                      <i Include=`i1`>
                      <n>overridden</n>
                      <m>m1</m>
                      <n>%(l);%(i.l);n1;%(m);%(i.m);%(o);%(i.o);n2</n>
                    </i>
                  </ItemGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.n)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[l1;l1;n1;m1;m1;;;n2]");
        }

        /// <summary>
        /// Metadata should be able to refer to metadata definitions
        /// </summary>
        [Test]
        public void MetadataReferenceToMetadataDefinition()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project xmlns=`msbuildnamespace`>
                  <ItemDefinitionGroup>
                    <i>
                      <m>m1</m>
                    </i>
                  </ItemDefinitionGroup> 
                  <ItemGroup>
                    <i Include=`i1`>
                      <m>%(m);m2</m>
                    </i>
                  </ItemGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[m1;m2]");
        }

        /// <summary>
        /// Escaping metadata should prevent it being evaluated
        /// </summary>
        [Test]
        public void EscapedMetadataReference()
        {
            MockLogger logger = new MockLogger();
            Project p = ObjectModelHelpers.CreateInMemoryProject(@"
                <Project xmlns=`msbuildnamespace`>
                  <ItemGroup>
                    <i Include=`i1`>
                      <m>%25(m)</m>
                    </i>
                  </ItemGroup> 
                  <Target Name=`t`>
                    <Message Text=`[%(i.m)]`/>
                  </Target>
                </Project>
            ", logger);

            p.Build("t");

            logger.AssertLogContains("[%(m)]");
        }

        [Test]
        public void RemoveItem1()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            BuildItem i2 = group[1];
            group.RemoveItem(i2);

            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals(1, group.ItemGroupElement.ChildNodes.Count);
            Assertion.AssertEquals("i1", group[0].Include);
            Assertion.AssertEquals(null, i2.ParentPersistedItemGroup);
        }

        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveItemNotBelonging()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            BuildItem item = new BuildItem("x", "x1");
            group.RemoveItem(item);
        }

        [Test]
        public void RemoveItemAt1()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            BuildItem i2 = group[1];
            group.RemoveItemAt(1);

            Assertion.AssertEquals(1, group.Count);
            Assertion.AssertEquals(1, group.ItemGroupElement.ChildNodes.Count);
            Assertion.AssertEquals("i1", group[0].Include);
            Assertion.AssertEquals(null, i2.ParentPersistedItemGroup);
        }

        // Can't shallow clone a persisted item group
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ShallowCloneOfPersistedItemGroup()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            BuildItemGroup clone = group.Clone(false);
        }

        [Test]
        public void DeepCloneOfPersistedItemGroup()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            BuildItemGroup clone = group.Clone(true);

            Assertion.AssertEquals(2, clone.Count);
            Assert.AreNotEqual(group.ItemGroupElement, clone.ItemGroupElement);
            Assert.AreNotEqual(group.ParentProject, clone.ParentProject);
        }

        [Test]
        public void ShallowCloneOfVirtualItemGroup()
        {
            BuildItemGroup group = new BuildItemGroup();
            group.AddNewItem("i", "i1");
            BuildItem i2 = new BuildItem("i", "i2");
            group.AddItem(i2);

            BuildItemGroup group2 = group.Clone(false /*shallow*/);

            Assertion.AssertEquals(2, group2.Count);
            Assertion.AssertEquals("i1", group2[0].FinalItemSpec);
            Assertion.Assert(i2.Equals(group2[1]));
        }

        [Test]
        public void Clear()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            BuildItem i1 = group[0];
            group.Clear();

            Assertion.AssertEquals(0, group.Count);
            Assertion.AssertEquals(0, ig.ChildNodes.Count);
            Assertion.AssertEquals(null, i1.ParentPersistedItemGroup);
        }

        [Test]
        public void RemoveAllIntermediateItems1()
        {
            BuildItemGroup group = new BuildItemGroup(); // virtual group
            XmlElement element = CreatePersistedItemGroupElement();

            BuildItem item1 = CreatePersistedBuildItem(element, "i", "i1");
            BuildItem item2 = CreatePersistedBuildItem(element, "i", "i2");
            group.AddExistingItem(item1);
            group.AddExistingItem(item2);
            group.AddNewItem("j", "j1");
            Assertion.AssertEquals(3, group.Count);

            group.RemoveAllIntermediateItems();
            Assertion.AssertEquals(2, group.Count);
        }

        [Test]
        public void Backup1()
        {
            BuildItemGroup group = new BuildItemGroup(); // virtual group
            XmlElement element = CreatePersistedItemGroupElement();

            BuildItem item1 = CreatePersistedBuildItem(element, "i", "i1");
            BuildItem item2 = CreatePersistedBuildItem(element, "i", "i2");
            group.AddExistingItem(item1);
            group.AddExistingItem(item2);
            BuildItem item3 = group.AddNewItem("j", "j1"); // virtual
            Assertion.AssertEquals(3, group.Count);

            group.RemoveItemWithBackup(item3);
            group.RemoveItemWithBackup(item1);
            Assertion.AssertEquals(1, group.Count);

            group.RemoveAllIntermediateItems();
            Assertion.AssertEquals(2, group.Count);
        }

        [Test]
        public void AddItem1()
        {
            XmlElement ig = CreatePersistedItemGroupElement();
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            BuildItem item = CreatePersistedBuildItem(ig, "i", "i3");
            group.AddItem(item);
            VerifyPersistedItemPosition(group, item, 2); // should be last

            item = CreatePersistedBuildItem(ig, "h", "h1");
            group.AddItem(item);
            VerifyPersistedItemPosition(group, item, 3); // should be last, because there were no h's.

            item = CreatePersistedBuildItem(ig, "h", "h0");
            group.AddItem(item);
            VerifyPersistedItemPosition(group, item, 3);// should be 2nd last

            item = CreatePersistedBuildItem(ig, "i", "i2");
            group.AddItem(item);
            VerifyPersistedItemPosition(group, item, 1); // should be 2nd      

            item = CreatePersistedBuildItem(ig, "i", "i0");
            group.AddItem(item);
            VerifyPersistedItemPosition(group, item, 0); // should be first              
        }

        [Test]
        public void AddItemEmptyPersistedGroup()
        {
            XmlDocument doc = new XmlDocument();
            XmlElement ig = doc.CreateElement("ItemGroup", XMakeAttributes.defaultXmlNamespace);
            BuildItemGroup group = new BuildItemGroup(ig, false, new Project());

            BuildItem item = CreatePersistedBuildItem(ig, "i", "i3");
            group.AddItem(item);
            VerifyPersistedItemPosition(group, item, 0);
        }

        [Test]
        public void AddItemEmptyNonPersistedGroup()
        {
            BuildItemGroup group = new BuildItemGroup();
            BuildItem item = new BuildItem("i", "i1");
            group.AddItem(item);
            Assertion.AssertEquals(item, group[0]); // should be last
            item = new BuildItem("i", "i0");
            group.AddItem(item);
            Assertion.AssertEquals(item, group[1]); // should be last again
        }
        
        private static void VerifyPersistedItemPosition(BuildItemGroup group, BuildItem item, int position)
        {
            Assertion.AssertEquals(group[position].Include, group.ItemGroupElement.ChildNodes[position].Attributes["Include"].Value);
            Assertion.AssertEquals(item.Include, group[position].Include);
        }

        private static BuildItem CreatePersistedBuildItem(XmlElement groupElement, string name, string include)
        {
            XmlElement element = groupElement.OwnerDocument.CreateElement(name, XMakeAttributes.defaultXmlNamespace);
            element.SetAttribute("Include", include);
            BuildItem item = new BuildItem(element, false, new ItemDefinitionLibrary(new Project()));
            return item;
        }

    }
}
