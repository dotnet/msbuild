// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectItemElement class
    /// </summary>
    public class ProjectItemElement_Tests
    {
        private const string RemoveInTarget = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Remove='i'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

        private const string RemoveOutsideTarget = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                            <ItemGroup>
                                <i Remove='i'/>
                            </ItemGroup>
                    </Project>
                ";
        private const string IncludeOutsideTarget = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i'/>
                        </ItemGroup>
                    </Project>
                ";
        private const string IncludeInsideTarget = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";
        private const string UpdateOutsideTarget = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                            <ItemGroup>
                                <i Update='i'/>
                            </ItemGroup>
                    </Project>
                ";
        private const string UpdateInTarget = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Update='i'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

        /// <summary>
        /// Read item with no children
        /// </summary>
        [Theory]
        [InlineData(IncludeInsideTarget)]
        [InlineData(IncludeOutsideTarget)]
        public void ReadNoChildren(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            Assert.Equal(0, Helpers.Count(item.Metadata));
        }

        /// <summary>
        /// Read item with no include
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i/>
                        </ItemGroup>
                    </Project>
                ")]
        // https://github.com/Microsoft/msbuild/issues/900
        //[InlineData(@"
        //            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
        //                <Target Name='t'>
        //                    <ItemGroup>
        //                        <i/>
        //                    </ItemGroup>
        //                </Target>
        //            </Project>
        //        ")]
        public void ReadInvalidNoInclude(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        /// <summary>
        /// Read item which contains text
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='a'>error text</i>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='a'>error text</i>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidContainsText(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        /// <summary>
        /// Read item with empty include
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include=''/>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include=''/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidEmptyInclude(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        /// <summary>
        /// Read item with reserved element name
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <PropertyGroup Include='i1'/>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <PropertyGroup Include='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidReservedElementName(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        /// <summary>
        /// Read item with Exclude without Include
        /// </summary>
        [Fact]
        public void ReadInvalidExcludeWithoutInclude()
        {
            var project = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Exclude='i1'/>
                        </ItemGroup>
                    </Project>
                ";

            var exception =
                Assert.Throws<InvalidProjectFileException>(
                    () => { ProjectRootElement.Create(XmlReader.Create(new StringReader(project))); }
                    );
            
            Assert.Contains("Items that are outside Target elements must have one of the following operations: Include, Update, or Remove.", exception.Message);
        }
        
        /// <summary>
        /// Read item with Exclude without Include under a target
        /// </summary>
        [Fact]
        public void ReadInvalidExcludeWithoutIncludeUnderTarget()
        {
            var project = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Exclude='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            var exception =
                Assert.Throws<InvalidProjectFileException>(
                    () => { ProjectRootElement.Create(XmlReader.Create(new StringReader(project))); }
                    );
            
            Assert.Contains("The attribute \"Exclude\" in element <i> is unrecognized.", exception.Message);
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i include='i1'/>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i include='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1' exclude='i2' />
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' exclude='i2' />
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidItemAttributeCasing(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        /// <summary>
        /// Basic reading of items
        /// </summary>
        [Theory]
        [InlineData(@"
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
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i'>
                                    <m1>v1</m1>
                                </i1>
                                <i2 Include='i' Exclude='j'>
                                    <m2>v2</m2>
                                </i2>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i1 Include='i' m1='v1' />
                            <i2 Include='i' Exclude='j' m2='v2' />
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' m1='v1' />
                                <i2 Include='i' Exclude='j' m2='v2' />
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadBasic(string project)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement) projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.Equal("i1", items[0].ItemType);
            Assert.Equal("i", items[0].Include);

            var metadata1 = Helpers.MakeList(items[0].Metadata);
            Assert.Single(metadata1);
            Assert.Equal("m1", metadata1[0].Name);
            Assert.Equal("v1", metadata1[0].Value);

            var metadata2 = Helpers.MakeList(items[1].Metadata);
            Assert.Equal("i2", items[1].ItemType);
            Assert.Equal("i", items[1].Include);
            Assert.Equal("j", items[1].Exclude);
            Assert.Single(metadata2);
            Assert.Equal("m2", metadata2[0].Name);
            Assert.Equal("v2", metadata2[0].Value);
        }

        /// <summary>
        /// Read metadata on item
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i1 Include='i'>
                                <m1>v1</m1>
                                <m2 Condition='c'>v2</m2>
                                <m1>v3</m1>
                            </i1>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i'>
                                    <m1>v1</m1>
                                    <m2 Condition='c'>v2</m2>
                                    <m1>v3</m1>
                                </i1>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadMetadata(string project)
        {
            ProjectRootElement projectElement = ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);
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

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1' Update='i2'/>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' Update='i2'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidUpdateWithInclude(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1' Exclude='i1' Update='i2'/>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' Exclude='i1' Update='i2'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidUpdateWithIncludeAndExclude(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Exclude='i1' Update='i2'/>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Exclude='i1' Update='i2'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidUpdateWithExclude(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        /// <summary>
        /// Read item with Remove inside of Target, but with metadata
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Remove='i1'>
                                    <m> </m>
                            </i>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Remove='i1'>
                                    <m> </m>
                                </i>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidRemoveWithMetadata(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        /// <summary>
        /// Read item with Remove inside of Target, but with Exclude: not currently supported
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Exclude='i1' Remove='i1'/>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Exclude='i1' Remove='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidExcludeAndRemove(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        /// <summary>
        /// Read item with Remove inside of Target, but with Include: not currently supported
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1' Remove='i1'/>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' Remove='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidIncludeAndRemove(string project)
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            }
           );
        }

        /// <summary>
        /// Read item with Remove inside of Target
        /// </summary>
        [Theory]
        [InlineData(RemoveInTarget)]
        [InlineData(RemoveOutsideTarget)]
        public void ReadValidRemove(string project)
        {
            var item = GetItemFromContent(project);

            Assert.Equal("i", item.Remove);
        }

        /// <summary>
        /// Read item with Remove inside of Target
        /// </summary>
        [Theory]
        [InlineData(UpdateInTarget)]
        [InlineData(UpdateOutsideTarget)]
        public void ReadValidUpdate(string project)
        {
            var item = GetItemFromContent(project);

            Assert.Equal("i", item.Update);
        }

        /// <summary>
        /// Read item with Exclude without Include, inside of Target
        /// </summary>
        [Theory]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <ItemGroup>
                            <i Include='i1' Exclude='i2'/>
                        </ItemGroup>
                    </Project>
                ")]
        [InlineData(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' Exclude='i2'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadValidIncludeExclude(string project)
        {
            var item = GetItemFromContent(project);

            Assert.Equal("i1", item.Include);
            Assert.Equal("i2", item.Exclude);
        }

        /// <summary>
        /// Set the include on an item
        /// </summary>
        [Theory]
        [InlineData(IncludeInsideTarget)]
        [InlineData(IncludeOutsideTarget)]
        public void SetInclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Include = "ib";

            Assert.Equal("ib", item.Include);
        }

        /// <summary>
        /// Set empty include: this removes it
        /// </summary>
        [Theory]
        [InlineData(IncludeInsideTarget)]
        [InlineData(IncludeOutsideTarget)]
        public void SetEmptyInclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Include = String.Empty;

            Assert.Equal(String.Empty, item.Include);
        }

        /// <summary>
        /// Set null empty : this removes it
        /// </summary>
        [Theory]
        [InlineData(IncludeInsideTarget)]
        [InlineData(IncludeOutsideTarget)]
        public void SetNullInclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Include = null;

            Assert.Equal(String.Empty, item.Include);
        }

        /// <summary>
        /// Set the Exclude on an item
        /// </summary>
        [Theory]
        [InlineData(IncludeInsideTarget)]
        [InlineData(IncludeOutsideTarget)]
        public void SetExclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Exclude = "ib";

            Assert.Equal("ib", item.Exclude);
        }

        /// <summary>
        /// Set empty Exclude: this removes it
        /// </summary>
        [Theory]
        [InlineData(IncludeInsideTarget)]
        [InlineData(IncludeOutsideTarget)]
        public void SetEmptyExclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Exclude = String.Empty;

            Assert.Equal(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Set null Exclude: this removes it
        /// </summary>
        [Theory]
        [InlineData(IncludeInsideTarget)]
        [InlineData(IncludeOutsideTarget)]
        public void SetNullExclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Exclude = null;

            Assert.Equal(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Set Remove when Include is present, inside a target
        /// </summary>
        [Theory]
        [InlineData(IncludeInsideTarget)]
        [InlineData(IncludeOutsideTarget)]
        public void SetInvalidRemoveWithInclude(string project)
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Remove = "i1";
            }
           );
        }

        /// <summary>
        /// Set Update when Include is present
        /// </summary>
        [Theory]
        [InlineData(IncludeInsideTarget)]
        [InlineData(IncludeOutsideTarget)]
        public void SetInvalidUpdateWithInclude(string project)
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Update = "i1";
            }
           );
        }

        /// <summary>
        /// Set the Remove on an item
        /// </summary>
        [Theory]
        [InlineData(RemoveInTarget)]
        [InlineData(RemoveOutsideTarget)]
        public void SetRemove(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Remove = "ib";

            Assert.Equal("ib", item.Remove);
        }

        /// <summary>
        /// Set empty Remove: this removes it
        /// </summary>
        [Theory]
        [InlineData(RemoveInTarget)]
        [InlineData(RemoveOutsideTarget)]
        public void SetEmptyRemove(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Remove = String.Empty;

            Assert.Equal(String.Empty, item.Remove);
        }

        /// <summary>
        /// Set null Remove: this removes it
        /// </summary>
        [Theory]
        [InlineData(RemoveInTarget)]
        [InlineData(RemoveOutsideTarget)]
        public void SetNullRemove(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Remove = null;

            Assert.Equal(String.Empty, item.Remove);
        }

        /// <summary>
        /// Set Include when Remove is present
        /// </summary>
        [Theory]
        [InlineData(RemoveInTarget)]
        [InlineData(RemoveOutsideTarget)]
        public void SetInvalidIncludeWithRemove(string project)
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Include = "i1";
            }
           );
        }

        /// <summary>
        /// Set Exclude when Remove is present
        /// </summary>
        [Theory]
        [InlineData(RemoveInTarget)]
        [InlineData(RemoveOutsideTarget)]
        public void SetInvalidExcludeWithRemove(string project)
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Exclude = "i1";
            }
           );
        }
        
        /// <summary>
        /// Set Update when Remove is present
        /// </summary>
        [Theory]
        [InlineData(RemoveInTarget)]
        [InlineData(RemoveOutsideTarget)]
        public void SetInvalidUpdateWithRemove(string project)
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Update = "i1";
            }
           );
        }

        /// 
        /// <summary>
        /// Set the Update on an item
        /// </summary>
        [Theory]
        [InlineData(UpdateInTarget)]
        [InlineData(UpdateOutsideTarget)]
        public void SetUpdate(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Update = "ib";

            Assert.Equal("ib", item.Update);
        }

        /// <summary>
        /// Set empty Update: this removes it
        /// </summary>
        [Theory]
        [InlineData(UpdateInTarget)]
        [InlineData(UpdateOutsideTarget)]
        public void SetEmptyUpdate(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Update = String.Empty;

            Assert.Equal(String.Empty, item.Update);
        }

        /// <summary>
        /// Set null Update: this removes it
        /// </summary>
        [Theory]
        [InlineData(UpdateInTarget)]
        [InlineData(UpdateOutsideTarget)]
        public void SetNullUpdate(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Update = null;

            Assert.Equal(String.Empty, item.Update);
        }

        /// <summary>
        /// Set Include when Update is present
        /// </summary>
        [Theory]
        [InlineData(UpdateInTarget)]
        [InlineData(UpdateOutsideTarget)]
        public void SetInvalidIncludeWithUpdate(string project)
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Include = "i1";
            }
           );
        }

        /// <summary>
        /// Set Exclude when Update is present
        /// </summary>
        [Theory]
        [InlineData(UpdateInTarget)]
        [InlineData(UpdateOutsideTarget)]
        public void SetInvalidExcludeWithUpdate(string project)
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Exclude = "i1";
            }
           );
        }

        /// <summary>
        /// Set the condition on an item
        /// </summary>
        [Theory]
        [InlineData(UpdateInTarget)]
        [InlineData(UpdateOutsideTarget)]
        public void SetCondition(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

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

            Assert.Empty(Helpers.MakeList(project.Items));
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

            Assert.Empty(Helpers.MakeList(project.Items));
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

            Assert.False(project.HasUnsavedChanges);

            item.Remove = "i2";

            Assert.Equal("i2", item.Remove);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Setting update should dirty the project
        /// </summary>
        [Fact]
        public void SettingItemUpdateDirties()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectItemElement item = project.AddItemGroup().AddItem("i", "i1");
            item.Include = null;
            Helpers.ClearDirtyFlag(project);

            Assert.False(project.HasUnsavedChanges);

            item.Update = "i2";

            Assert.Equal("i2", item.Update);
            Assert.True(project.HasUnsavedChanges);
        }

        private static ProjectItemElement GetItemFromContent(string content)
        {
            var project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            return Helpers.GetFirst(project.Items);
        }
    }
}