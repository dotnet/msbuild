// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectItemElement class
    /// </summary>
    [TestClass]
    public class ProjectItemElement_Tests
    {
        private const string RemoveInTarget = @"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Remove='i'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

        private const string RemoveOutsideTarget = @"
                    <Project>
                            <ItemGroup>
                                <i Remove='i'/>
                            </ItemGroup>
                    </Project>
                ";
        private const string IncludeOutsideTarget = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i'/>
                        </ItemGroup>
                    </Project>
                ";
        private const string IncludeInsideTarget = @"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";
        private const string UpdateOutsideTarget = @"
                    <Project>
                            <ItemGroup>
                                <i Update='i'/>
                            </ItemGroup>
                    </Project>
                ";
        private const string UpdateInTarget = @"
                    <Project>
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
        [MSBuildTestMethod]
        [DataRow(IncludeInsideTarget)]
        [DataRow(IncludeOutsideTarget)]
        public void ReadNoChildren(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            Assert.AreEqual(0, Helpers.Count(item.Metadata));
        }

        [MSBuildTestMethod]
        public void ReadMetadataLocationPreserved()
        {
            string project = """
                <Project>
                    <Target Name='t'>
                        <ItemGroup>
                            <i Include='i' MetadataA='123' MetadataB='xyz' />
                        </ItemGroup>
                    </Target>
                </Project>
                """;

            ProjectItemElement item = GetItemFromContent(project);
            Assert.AreEqual(2, item.Metadata.Count);
            ProjectMetadataElement metadatum1 = item.Metadata.First();
            ProjectMetadataElement metadatum2 = item.Metadata.Skip(1).First();

            Assert.AreEqual(4, metadatum1.Location.Line);
            Assert.AreEqual(4, metadatum2.Location.Line);
            Assert.AreEqual(27, metadatum1.Location.Column);
            Assert.AreEqual(43, metadatum2.Location.Column);
        }

        /// <summary>
        /// Read item with no include
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i/>
                        </ItemGroup>
                    </Project>
                ")]
        // https://github.com/dotnet/msbuild/issues/900
        // [DataRow(@"
        //            <Project>
        //                <Target Name='t'>
        //                    <ItemGroup>
        //                        <i/>
        //                    </ItemGroup>
        //                </Target>
        //            </Project>
        //        ")]
        public void ReadInvalidNoInclude(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        /// <summary>
        /// Read item which contains text
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include='a'>error text</i>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='a'>error text</i>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidContainsText(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        /// <summary>
        /// Read item with empty include
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include=''/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include=''/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidEmptyInclude(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        /// <summary>
        /// Read item with reserved element name
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <PropertyGroup Include='i1'/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <PropertyGroup Include='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidReservedElementName(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        /// <summary>
        /// Read item with Exclude without Include
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidExcludeWithoutInclude()
        {
            var project = @"
                    <Project>
                        <ItemGroup>
                            <i Exclude='i1'/>
                        </ItemGroup>
                    </Project>
                ";

            var exception =
                Assert.ThrowsExactly<InvalidProjectFileException>(
                    () => { ProjectRootElement.Create(XmlReader.Create(new StringReader(project))); });

            Assert.Contains("Items that are outside Target elements must have one of the following operations: Include, Update, or Remove.", exception.Message);
        }

        /// <summary>
        /// Read item with Exclude without Include under a target
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidExcludeWithoutIncludeUnderTarget()
        {
            var project = @"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Exclude='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ";

            var exception =
                Assert.ThrowsExactly<InvalidProjectFileException>(
                    () => { ProjectRootElement.Create(XmlReader.Create(new StringReader(project))); });

            Assert.Contains("The attribute \"Exclude\" in element <i> is unrecognized.", exception.Message);
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i include='i1'/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i include='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include='i1' exclude='i2' />
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' exclude='i2' />
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidItemAttributeCasing(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        /// <summary>
        /// Basic reading of items
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
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
        [DataRow(@"
                    <Project>
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
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i' m1='v1' />
                            <i2 Include='i' Exclude='j' m2='v2' />
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
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
            using ProjectRootElementFromString projectRootElementFromString = new(project);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.AreEqual("i1", items[0].ItemType);
            Assert.AreEqual("i", items[0].Include);

            var metadata1 = Helpers.MakeList(items[0].Metadata);
            Assert.ContainsSingle(metadata1);
            Assert.AreEqual("m1", metadata1[0].Name);
            Assert.AreEqual("v1", metadata1[0].Value);

            var metadata2 = Helpers.MakeList(items[1].Metadata);
            Assert.AreEqual("i2", items[1].ItemType);
            Assert.AreEqual("i", items[1].Include);
            Assert.AreEqual("j", items[1].Exclude);
            Assert.ContainsSingle(metadata2);
            Assert.AreEqual("m2", metadata2[0].Name);
            Assert.AreEqual("v2", metadata2[0].Value);
        }

        /// <summary>
        /// Read metadata on item
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i'>
                                <m1>v1</m1>
                                <m2 Condition='c'>v2</m2>
                                <m1>v3</m1>
                            </i1>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
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
            using ProjectRootElementFromString projectRootElementFromString = new(project);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);
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

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include='i1' Update='i2'/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' Update='i2'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidUpdateWithInclude(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include='i1' Exclude='i1' Update='i2'/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' Exclude='i1' Update='i2'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidUpdateWithIncludeAndExclude(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Exclude='i1' Update='i2'/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Exclude='i1' Update='i2'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidUpdateWithExclude(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        /// <summary>
        /// Read item with Remove inside of Target, but with metadata
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Remove='i1'>
                                    <m> </m>
                            </i>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
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
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        /// <summary>
        /// Read item with Remove inside of Target, but with Exclude: not currently supported
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Exclude='i1' Remove='i1'/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Exclude='i1' Remove='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidExcludeAndRemove(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        /// <summary>
        /// Read item with Remove inside of Target, but with Include: not currently supported
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include='i1' Remove='i1'/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i Include='i1' Remove='i1'/>
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadInvalidIncludeAndRemove(string project)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(project)));
            });
        }

        /// <summary>
        /// Read item with Remove inside of Target
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(RemoveInTarget)]
        [DataRow(RemoveOutsideTarget)]
        public void ReadValidRemove(string project)
        {
            var item = GetItemFromContent(project);

            Assert.AreEqual("i", item.Remove);
        }

        /// <summary>
        /// Read item with Remove inside of Target
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(UpdateInTarget)]
        [DataRow(UpdateOutsideTarget)]
        public void ReadValidUpdate(string project)
        {
            var item = GetItemFromContent(project);

            Assert.AreEqual("i", item.Update);
        }

        /// <summary>
        /// Read item with Exclude without Include, inside of Target
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include='i1' Exclude='i2'/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
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

            Assert.AreEqual("i1", item.Include);
            Assert.AreEqual("i2", item.Exclude);
        }

        /// <summary>
        /// Set the include on an item
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(IncludeInsideTarget)]
        [DataRow(IncludeOutsideTarget)]
        public void SetInclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Include = "ib";

            Assert.AreEqual("ib", item.Include);
        }

        /// <summary>
        /// Set empty include: this removes it
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(IncludeInsideTarget)]
        [DataRow(IncludeOutsideTarget)]
        public void SetEmptyInclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Include = String.Empty;

            Assert.AreEqual(String.Empty, item.Include);
        }

        /// <summary>
        /// Set null empty : this removes it
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(IncludeInsideTarget)]
        [DataRow(IncludeOutsideTarget)]
        public void SetNullInclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Include = null;

            Assert.AreEqual(String.Empty, item.Include);
        }

        /// <summary>
        /// Set the Exclude on an item
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(IncludeInsideTarget)]
        [DataRow(IncludeOutsideTarget)]
        public void SetExclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Exclude = "ib";

            Assert.AreEqual("ib", item.Exclude);
        }

        /// <summary>
        /// Set empty Exclude: this removes it
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(IncludeInsideTarget)]
        [DataRow(IncludeOutsideTarget)]
        public void SetEmptyExclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Exclude = String.Empty;

            Assert.AreEqual(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Set null Exclude: this removes it
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(IncludeInsideTarget)]
        [DataRow(IncludeOutsideTarget)]
        public void SetNullExclude(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Exclude = null;

            Assert.AreEqual(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Set Remove when Include is present, inside a target
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(IncludeInsideTarget)]
        [DataRow(IncludeOutsideTarget)]
        public void SetInvalidRemoveWithInclude(string project)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Remove = "i1";
            });
        }

        /// <summary>
        /// Set Update when Include is present
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(IncludeInsideTarget)]
        [DataRow(IncludeOutsideTarget)]
        public void SetInvalidUpdateWithInclude(string project)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Update = "i1";
            });
        }

        /// <summary>
        /// Set the Remove on an item
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(RemoveInTarget)]
        [DataRow(RemoveOutsideTarget)]
        public void SetRemove(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Remove = "ib";

            Assert.AreEqual("ib", item.Remove);
        }

        /// <summary>
        /// Set empty Remove: this removes it
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(RemoveInTarget)]
        [DataRow(RemoveOutsideTarget)]
        public void SetEmptyRemove(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Remove = String.Empty;

            Assert.AreEqual(String.Empty, item.Remove);
        }

        /// <summary>
        /// Set null Remove: this removes it
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(RemoveInTarget)]
        [DataRow(RemoveOutsideTarget)]
        public void SetNullRemove(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Remove = null;

            Assert.AreEqual(String.Empty, item.Remove);
        }

        /// <summary>
        /// Set Include when Remove is present
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(RemoveInTarget)]
        [DataRow(RemoveOutsideTarget)]
        public void SetInvalidIncludeWithRemove(string project)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Include = "i1";
            });
        }

        /// <summary>
        /// Set Exclude when Remove is present
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(RemoveInTarget)]
        [DataRow(RemoveOutsideTarget)]
        public void SetInvalidExcludeWithRemove(string project)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Exclude = "i1";
            });
        }

        /// <summary>
        /// Set Update when Remove is present
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(RemoveInTarget)]
        [DataRow(RemoveOutsideTarget)]
        public void SetInvalidUpdateWithRemove(string project)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Update = "i1";
            });
        }

        ///
        /// <summary>
        /// Set the Update on an item
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(UpdateInTarget)]
        [DataRow(UpdateOutsideTarget)]
        public void SetUpdate(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Update = "ib";

            Assert.AreEqual("ib", item.Update);
        }

        /// <summary>
        /// Set empty Update: this removes it
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(UpdateInTarget)]
        [DataRow(UpdateOutsideTarget)]
        public void SetEmptyUpdate(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Update = String.Empty;

            Assert.AreEqual(String.Empty, item.Update);
        }

        /// <summary>
        /// Set null Update: this removes it
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(UpdateInTarget)]
        [DataRow(UpdateOutsideTarget)]
        public void SetNullUpdate(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Update = null;

            Assert.AreEqual(String.Empty, item.Update);
        }

        /// <summary>
        /// Set Include when Update is present
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(UpdateInTarget)]
        [DataRow(UpdateOutsideTarget)]
        public void SetInvalidIncludeWithUpdate(string project)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Include = "i1";
            });
        }

        /// <summary>
        /// Set Exclude when Update is present
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(UpdateInTarget)]
        [DataRow(UpdateOutsideTarget)]
        public void SetInvalidExcludeWithUpdate(string project)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectItemElement item = GetItemFromContent(project);

                item.Exclude = "i1";
            });
        }

        /// <summary>
        /// Set the condition on an item
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(UpdateInTarget)]
        [DataRow(UpdateOutsideTarget)]
        public void SetCondition(string project)
        {
            ProjectItemElement item = GetItemFromContent(project);

            item.Condition = "c";

            Assert.AreEqual("c", item.Condition);
        }

        /// <summary>
        /// Setting condition should dirty the project
        /// </summary>
        [MSBuildTestMethod]
        public void SettingItemConditionDirties()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.Xml.Condition = "false";
            project.ReevaluateIfNecessary();

            Assert.IsEmpty(Helpers.MakeList(project.Items));
        }

        /// <summary>
        /// Setting include should dirty the project
        /// </summary>
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void SettingItemExcludeDirties()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            project.ReevaluateIfNecessary();

            item.Xml.Exclude = "i1";
            project.ReevaluateIfNecessary();

            Assert.IsEmpty(Helpers.MakeList(project.Items));
        }

        /// <summary>
        /// Setting exclude should dirty the project
        /// </summary>
        [MSBuildTestMethod]
        public void SettingItemRemoveDirties()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectItemElement item = project.AddTarget("t").AddItemGroup().AddItem("i", "i1");
            item.Include = null;
            Helpers.ClearDirtyFlag(project);

            Assert.IsFalse(project.HasUnsavedChanges);

            item.Remove = "i2";

            Assert.AreEqual("i2", item.Remove);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Setting update should dirty the project
        /// </summary>
        [MSBuildTestMethod]
        public void SettingItemUpdateDirties()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectItemElement item = project.AddItemGroup().AddItem("i", "i1");
            item.Include = null;
            Helpers.ClearDirtyFlag(project);

            Assert.IsFalse(project.HasUnsavedChanges);

            item.Update = "i2";

            Assert.AreEqual("i2", item.Update);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        private static ProjectItemElement GetItemFromContent(string content)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;

            return Helpers.GetFirst(project.Items);
        }
    }
}
