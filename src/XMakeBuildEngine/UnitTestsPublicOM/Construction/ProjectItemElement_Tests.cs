//-----------------------------------------------------------------------
// <copyright file="ProjectItemElement_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for the ProjectItemElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectItemElement class
    /// </summary>
    [TestClass]
    public class ProjectItemElement_Tests
    {
        /// <summary>
        /// Read item with no children
        /// </summary>
        [TestMethod]
        public void ReadNoChildren()
        {
            ProjectItemElement item = GetItemXml();

            Assert.AreEqual(0, Helpers.Count(item.Metadata));
        }

        /// <summary>
        /// Read item with no include
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidNoInclude()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i/>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read item which contains text
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidContainsText()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='a'>error text</i>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }
        
        /// <summary>
        /// Read item with empty include
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidEmptyInclude()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include=''>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read item with reserved element name
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidReservedElementName()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <PropertyGroup Include='i1'/>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read item with Exclude without Include
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidExcludeWithoutInclude()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Exclude='i1'/>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Basic reading of items
        /// </summary>
        [TestMethod]
        public void ReadBasic()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i1 Include='i'>
                                <m1>v1</m1>
                            </i1>
                            <i2 Include='i' Exclude='j'>
                                <m2>v2</m2>
                            </i2>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)Helpers.GetFirst(project.Children);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.AreEqual("i1", items[0].ItemType);
            Assert.AreEqual("i", items[0].Include);

            var metadata1 = Helpers.MakeList(items[0].Metadata);
            Assert.AreEqual(1, metadata1.Count);
            Assert.AreEqual("m1", metadata1[0].Name);
            Assert.AreEqual("v1", metadata1[0].Value);

            var metadata2 = Helpers.MakeList(items[1].Metadata);
            Assert.AreEqual("i2", items[1].ItemType);
            Assert.AreEqual("i", items[1].Include);
            Assert.AreEqual("j", items[1].Exclude);
            Assert.AreEqual(1, metadata2.Count);
            Assert.AreEqual("m2", metadata2[0].Name);
            Assert.AreEqual("v2", metadata2[0].Value);
        }

        /// <summary>
        /// Read metadata on item
        /// </summary>
        [TestMethod]
        public void ReadMetadata()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i1 Include='i'>
                                <m1>v1</m1>
                                <m2 Condition='c'>v2</m2>
                                <m1>v3</m1>
                            </i1>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)Helpers.GetFirst(project.Children);
            ProjectItemElement item = Helpers.GetFirst(itemGroup.Items);

            var metadata = Helpers.MakeList(item.Metadata);
            Assert.AreEqual(3, metadata.Count);
            Assert.AreEqual("m1", metadata[0].Name);
            Assert.AreEqual("v1", metadata[0].Value);
            Assert.AreEqual("m2", metadata[1].Name);
            Assert.AreEqual("v2", metadata[1].Value);
            Assert.AreEqual("c", metadata[1].Condition);
            Assert.AreEqual("m1", metadata[2].Name);
            Assert.AreEqual("v3", metadata[2].Value);
        }

        /// <summary>
        /// Read item with Remove outside of Target: not currently supported
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidRemoveOutsideTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Remove='i1'/>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read item with Remove inside of Target, but with metadata
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidRemoveWithMetadataInsideTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Remove='i1'>
                                    <m/>
                                </i>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read item with Remove inside of Target, but with Exclude: not currently supported
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidExcludeAndRemoveInsideTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Exclude='i1' Remove='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read item with Remove inside of Target, but with Include: not currently supported
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidIncludeAndRemoveInsideTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' Remove='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read item with Remove inside of Target
        /// </summary>
        [TestMethod]
        public void ReadValidRemoveInsideTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Remove='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)Helpers.GetFirst(target.Children);
            ProjectItemElement item = Helpers.GetFirst(itemGroup.Items);

            Assert.AreEqual("i1", item.Remove);
        }

        /// <summary>
        /// Read item with Exclude without Include, inside of Target
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ReadInvalidExcludeWithoutIncludeWithinTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Exclude='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
        }

        /// <summary>
        /// Read item with Exclude without Include, inside of Target
        /// </summary>
        [TestMethod]
        public void ReadValidIncludeExcludeWithinTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' Exclude='i2'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)Helpers.GetFirst(target.Children);
            ProjectItemElement item = Helpers.GetFirst(itemGroup.Items);

            Assert.AreEqual("i1", item.Include);
            Assert.AreEqual("i2", item.Exclude);
        }

        /// <summary>
        /// Set the include on an item
        /// </summary>
        [TestMethod]
        public void SetInclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Include = "ib";

            Assert.AreEqual("ib", item.Include);
        }

        /// <summary>
        /// Set empty include: this removes it
        /// </summary>
        [TestMethod]
        public void SetEmptyInclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Include = String.Empty;

            Assert.AreEqual(String.Empty, item.Include);
        }

        /// <summary>
        /// Set null empty : this removes it
        /// </summary>
        [TestMethod]
        public void SetNullInclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Include = null;

            Assert.AreEqual(String.Empty, item.Include);
        }

        /// <summary>
        /// Set the Exclude on an item
        /// </summary>
        [TestMethod]
        public void SetExclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Exclude = "ib";

            Assert.AreEqual("ib", item.Exclude);
        }

        /// <summary>
        /// Set empty Exclude: this removes it
        /// </summary>
        [TestMethod]
        public void SetEmptyExclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Exclude = String.Empty;

            Assert.AreEqual(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Set null Exclude: this removes it
        /// </summary>
        [TestMethod]
        public void SetNullExclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Exclude = null;

            Assert.AreEqual(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Set the Remove on an item
        /// </summary>
        [TestMethod]
        public void SetRemove()
        {
            ProjectItemElement item = GetItemXmlWithRemove();

            item.Remove = "ib";

            Assert.AreEqual("ib", item.Remove);
        }

        /// <summary>
        /// Set empty Remove: this removes it
        /// </summary>
        [TestMethod]
        public void SetEmptyRemove()
        {
            ProjectItemElement item = GetItemXmlWithRemove();

            item.Remove = String.Empty;

            Assert.AreEqual(String.Empty, item.Remove);
        }

        /// <summary>
        /// Set null Remove: this removes it
        /// </summary>
        [TestMethod]
        public void SetNullRemove()
        {
            ProjectItemElement item = GetItemXmlWithRemove();

            item.Remove = null;

            Assert.AreEqual(String.Empty, item.Remove);
        }

        /// <summary>
        /// Set Include when Remove is present
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetInvalidIncludeWithRemove()
        {
            ProjectItemElement item = GetItemXmlWithRemove();

            item.Include = "i1";
        }

        /// <summary>
        /// Set Exclude when Remove is present
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetInvalidExcludeWithRemove()
        {
            ProjectItemElement item = GetItemXmlWithRemove();

            item.Exclude = "i1";
        }

        /// <summary>
        /// Set Remove when Include is present, inside a target
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetInvalidRemoveWithInclude()
        {
            ProjectItemElement item = GetItemXmlInsideTarget();

            item.Remove = "i1";
        }

        /// <summary>
        /// Set Remove outside of a target: this is currently invalid
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetInvalidRemoveOutsideTarget()
        {
            ProjectItemElement item = GetItemXml();

            item.Remove = "i1";
        }

        /// <summary>
        /// Set the condition on an item
        /// </summary>
        [TestMethod]
        public void SetCondition()
        {
            ProjectItemElement item = GetItemXml();

            item.Condition = "c";

            Assert.AreEqual("c", item.Condition);
        }

        /// <summary>
        /// Setting condition should dirty the project
        /// </summary>
        [TestMethod]
        public void SettingItemConditionDirties()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.Xml.Condition = "false";
            project.ReevaluateIfNecessary();

            Assert.AreEqual(0, Helpers.MakeList(project.Items).Count);
        }

        /// <summary>
        /// Setting include should dirty the project
        /// </summary>
        [TestMethod]
        public void SettingItemIncludeDirties()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.Xml.Include = "i2";
            project.ReevaluateIfNecessary();

            Assert.AreEqual("i2", Helpers.GetFirst(project.Items).EvaluatedInclude);
        }

        /// <summary>
        /// Setting exclude should dirty the project
        /// </summary>
        [TestMethod]
        public void SettingItemExcludeDirties()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.Xml.Exclude = "i1";
            project.ReevaluateIfNecessary();

            Assert.AreEqual(0, Helpers.MakeList(project.Items).Count);
        }

        /// <summary>
        /// Setting exclude should dirty the project
        /// </summary>
        [TestMethod]
        public void SettingItemRemoveDirties()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectItemElement item = project.AddTarget("t").AddItemGroup().AddItem("i", "i1");
            item.Include = null;
            Helpers.ClearDirtyFlag(project);

            item.Remove = "i2";

            Assert.AreEqual("i2", item.Remove);
            Assert.AreEqual(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Get a valid ProjectItemElement with no metadata
        /// </summary>
        private static ProjectItemElement GetItemXml()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i'/>
                        </ItemGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)Helpers.GetFirst(project.Children);
            ProjectItemElement item = Helpers.GetFirst(itemGroup.Items);
            return item;
        }

        /// <summary>
        /// Get a valid ProjectItemElement with an Include on it (inside a Target)
        /// </summary>
        private static ProjectItemElement GetItemXmlInsideTarget()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)Helpers.GetFirst(target.Children);
            ProjectItemElement item = Helpers.GetFirst(itemGroup.Items);
            return item;
        }

        /// <summary>
        /// Get a valid ProjectItemElement with a Remove on it (in a target)
        /// </summary>
        private static ProjectItemElement GetItemXmlWithRemove()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Remove='i'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            ProjectTargetElement target = (ProjectTargetElement)Helpers.GetFirst(project.Children);
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)Helpers.GetFirst(target.Children);
            ProjectItemElement item = Helpers.GetFirst(itemGroup.Items);
            return item;
        }
    }
}