// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
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

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectItemElement class
    /// </summary>
    public class ProjectItemElement_Tests
    {
        /// <summary>
        /// Read item with no children
        /// </summary>
        [Fact]
        public void ReadNoChildren()
        {
            ProjectItemElement item = GetItemXml();

            Assert.Equal(0, Helpers.Count(item.Metadata));
        }

        /// <summary>
        /// Read item with no include
        /// </summary>
        [Fact]
        public void ReadInvalidNoInclude()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read item which contains text
        /// </summary>
        [Fact]
        public void ReadInvalidContainsText()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read item with empty include
        /// </summary>
        [Fact]
        public void ReadInvalidEmptyInclude()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read item with reserved element name
        /// </summary>
        [Fact]
        public void ReadInvalidReservedElementName()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read item with Exclude without Include
        /// </summary>
        [Fact]
        public void ReadInvalidExcludeWithoutInclude()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Basic reading of items
        /// </summary>
        [Fact]
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

            Assert.Equal("i1", items[0].ItemType);
            Assert.Equal("i", items[0].Include);

            var metadata1 = Helpers.MakeList(items[0].Metadata);
            Assert.Equal(1, metadata1.Count);
            Assert.Equal("m1", metadata1[0].Name);
            Assert.Equal("v1", metadata1[0].Value);

            var metadata2 = Helpers.MakeList(items[1].Metadata);
            Assert.Equal("i2", items[1].ItemType);
            Assert.Equal("i", items[1].Include);
            Assert.Equal("j", items[1].Exclude);
            Assert.Equal(1, metadata2.Count);
            Assert.Equal("m2", metadata2[0].Name);
            Assert.Equal("v2", metadata2[0].Value);
        }

        /// <summary>
        /// Read metadata on item
        /// </summary>
        [Fact]
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
            Assert.Equal(3, metadata.Count);
            Assert.Equal("m1", metadata[0].Name);
            Assert.Equal("v1", metadata[0].Value);
            Assert.Equal("m2", metadata[1].Name);
            Assert.Equal("v2", metadata[1].Value);
            Assert.Equal("c", metadata[1].Condition);
            Assert.Equal("m1", metadata[2].Name);
            Assert.Equal("v3", metadata[2].Value);
        }

        /// <summary>
        /// Read item with Remove outside of Target: not currently supported
        /// </summary>
        [Fact]
        public void ReadInvalidRemoveOutsideTarget()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read item with Remove inside of Target, but with metadata
        /// </summary>
        [Fact]
        public void ReadInvalidRemoveWithMetadataInsideTarget()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read item with Remove inside of Target, but with Exclude: not currently supported
        /// </summary>
        [Fact]
        public void ReadInvalidExcludeAndRemoveInsideTarget()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read item with Remove inside of Target, but with Include: not currently supported
        /// </summary>
        [Fact]
        public void ReadInvalidIncludeAndRemoveInsideTarget()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read item with Remove inside of Target
        /// </summary>
        [Fact]
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

            Assert.Equal("i1", item.Remove);
        }

        /// <summary>
        /// Read item with Exclude without Include, inside of Target
        /// </summary>
        [Fact]
        public void ReadInvalidExcludeWithoutIncludeWithinTarget()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
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
           );
        }
        /// <summary>
        /// Read item with Exclude without Include, inside of Target
        /// </summary>
        [Fact]
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

            Assert.Equal("i1", item.Include);
            Assert.Equal("i2", item.Exclude);
        }

        /// <summary>
        /// Set the include on an item
        /// </summary>
        [Fact]
        public void SetInclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Include = "ib";

            Assert.Equal("ib", item.Include);
        }

        /// <summary>
        /// Set empty include: this removes it
        /// </summary>
        [Fact]
        public void SetEmptyInclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Include = String.Empty;

            Assert.Equal(String.Empty, item.Include);
        }

        /// <summary>
        /// Set null empty : this removes it
        /// </summary>
        [Fact]
        public void SetNullInclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Include = null;

            Assert.Equal(String.Empty, item.Include);
        }

        /// <summary>
        /// Set the Exclude on an item
        /// </summary>
        [Fact]
        public void SetExclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Exclude = "ib";

            Assert.Equal("ib", item.Exclude);
        }

        /// <summary>
        /// Set empty Exclude: this removes it
        /// </summary>
        [Fact]
        public void SetEmptyExclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Exclude = String.Empty;

            Assert.Equal(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Set null Exclude: this removes it
        /// </summary>
        [Fact]
        public void SetNullExclude()
        {
            ProjectItemElement item = GetItemXml();

            item.Exclude = null;

            Assert.Equal(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Set the Remove on an item
        /// </summary>
        [Fact]
        public void SetRemove()
        {
            ProjectItemElement item = GetItemXmlWithRemove();

            item.Remove = "ib";

            Assert.Equal("ib", item.Remove);
        }

        /// <summary>
        /// Set empty Remove: this removes it
        /// </summary>
        [Fact]
        public void SetEmptyRemove()
        {
            ProjectItemElement item = GetItemXmlWithRemove();

            item.Remove = String.Empty;

            Assert.Equal(String.Empty, item.Remove);
        }

        /// <summary>
        /// Set null Remove: this removes it
        /// </summary>
        [Fact]
        public void SetNullRemove()
        {
            ProjectItemElement item = GetItemXmlWithRemove();

            item.Remove = null;

            Assert.Equal(String.Empty, item.Remove);
        }

        /// <summary>
        /// Set Include when Remove is present
        /// </summary>
        [Fact]
        public void SetInvalidIncludeWithRemove()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemXmlWithRemove();

                item.Include = "i1";
            }
           );
        }
        /// <summary>
        /// Set Exclude when Remove is present
        /// </summary>
        [Fact]
        public void SetInvalidExcludeWithRemove()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemXmlWithRemove();

                item.Exclude = "i1";
            }
           );
        }
        /// <summary>
        /// Set Remove when Include is present, inside a target
        /// </summary>
        [Fact]
        public void SetInvalidRemoveWithInclude()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemXmlInsideTarget();

                item.Remove = "i1";
            }
           );
        }
        /// <summary>
        /// Set Remove outside of a target: this is currently invalid
        /// </summary>
        [Fact]
        public void SetInvalidRemoveOutsideTarget()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemXml();

                item.Remove = "i1";
            }
           );
        }
        /// <summary>
        /// Set the condition on an item
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectItemElement item = GetItemXml();

            item.Condition = "c";

            Assert.Equal("c", item.Condition);
        }

        /// <summary>
        /// Setting condition should dirty the project
        /// </summary>
        [Fact]
        public void SettingItemConditionDirties()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.Xml.Condition = "false";
            project.ReevaluateIfNecessary();

            Assert.Equal(0, Helpers.MakeList(project.Items).Count);
        }

        /// <summary>
        /// Setting include should dirty the project
        /// </summary>
        [Fact]
        public void SettingItemIncludeDirties()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.Xml.Include = "i2";
            project.ReevaluateIfNecessary();

            Assert.Equal("i2", Helpers.GetFirst(project.Items).EvaluatedInclude);
        }

        /// <summary>
        /// Setting exclude should dirty the project
        /// </summary>
        [Fact]
        public void SettingItemExcludeDirties()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.Xml.Exclude = "i1";
            project.ReevaluateIfNecessary();

            Assert.Equal(0, Helpers.MakeList(project.Items).Count);
        }

        /// <summary>
        /// Setting exclude should dirty the project
        /// </summary>
        [Fact]
        public void SettingItemRemoveDirties()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectItemElement item = project.AddTarget("t").AddItemGroup().AddItem("i", "i1");
            item.Include = null;
            Helpers.ClearDirtyFlag(project);

            item.Remove = "i2";

            Assert.Equal("i2", item.Remove);
            Assert.Equal(true, project.HasUnsavedChanges);
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