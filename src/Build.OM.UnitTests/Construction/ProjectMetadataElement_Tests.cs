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
    /// Tests for the ProjectMetadataElement class
    /// </summary>
    [TestClass]
    public class ProjectMetadataElement_Tests
    {
        private readonly TestContext _testOutput;

        public ProjectMetadataElement_Tests(TestContext testOutput)
        {
            _testOutput = testOutput;
        }

        /// <summary>
        /// Read simple metadatum
        /// </summary>
        [MSBuildTestMethod]
        public void ReadMetadata()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            Assert.AreEqual("m", metadatum.Name);
            Assert.AreEqual("m1", metadatum.Value);
            Assert.AreEqual("c", metadatum.Condition);
        }

        /// <summary>
        /// Read metadatum with invalid attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidAttribute()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <m Condition='c' XX='YY'/>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read metadatum with invalid name characters (but legal xml)
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <" + "\u03A3" + @"/>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include='i1' " + "\u03A3" + @"='v1' />
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i " + "\u03A3" + @"='v1' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        public void ReadInvalidNameAsAttribute(string content)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        /// <summary>
        /// Read metadatum with invalid built-in metadata name
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidBuiltInName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <Filename/>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include='i1' Filename='v1'/>
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i Filename='v1'/>
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        public void ReadInvalidBuiltInNameAsAttribute(string content)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        /// <summary>
        /// Read metadatum with invalid built-in element name
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidBuiltInElementName()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <PropertyGroup/>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        /// <summary>
        /// Read metadatum with invalid built-in element name
        /// </summary>
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i Include='i1' PropertyGroup='v1' />
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i PropertyGroup='v1' />
                        </ItemDefinitionGroup>
                    </Project>
                ")]
        public void ReadInvalidBuiltInElementNameAsAttribute(string content)
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        /// <summary>
        /// Set metadatum value
        /// </summary>
        [MSBuildTestMethod]
        public void SetValue()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Value = "m1b";
            Assert.AreEqual("m1b", metadatum.Value);
        }

        /// <summary>
        /// Rename
        /// </summary>
        [MSBuildTestMethod]
        public void SetName()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Name = "m2";
            Assert.AreEqual("m2", metadatum.Name);
            Assert.IsTrue(metadatum.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to same value should not mark dirty
        /// </summary>
        [MSBuildTestMethod]
        public void SetNameSame()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();
            Helpers.ClearDirtyFlag(metadatum.ContainingProject);

            metadatum.Name = "m";
            Assert.AreEqual("m", metadatum.Name);
            Assert.IsFalse(metadatum.ContainingProject.HasUnsavedChanges);
        }

        /// <summary>
        /// Rename to illegal name
        /// </summary>
        [MSBuildTestMethod]
        public void SetNameIllegal()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                ProjectMetadataElement metadatum = GetMetadataXml();

                metadatum.Name = "ImportGroup";
            });
        }

        [MSBuildTestMethod]
        public void SetNameIllegalAsAttribute()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();
            metadatum.ExpressedAsAttribute = true;

            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                metadatum.Name = "Include";
            });
        }


        [MSBuildTestMethod]
        public void SetExpressedAsAttributeIllegalName()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();
            metadatum.Name = "Include";

            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                metadatum.ExpressedAsAttribute = true;
            });
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i' />
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' />
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void AddMetadataAsAttributeIllegalName(string project)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(project);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.ContainsSingle(items);

            var item = items.First();

            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                item.AddMetadata("Include", "v1", true);
            });
        }

        [MSBuildTestMethod]
        public void AddMetadataAsAttributeToItemDefinitionIllegalName()
        {
            string project = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1/>
                        </ItemDefinitionGroup>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(project);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemDefinitionGroupElement);

            var itemDefinitions = Helpers.MakeList(itemDefinitionGroup.ItemDefinitions);

            Assert.ContainsSingle(itemDefinitions);

            var itemDefinition = itemDefinitions.First();

            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                itemDefinition.AddMetadata("Include", "v1", true);
            });
        }

        /// <summary>
        /// Set metadatum value to empty
        /// </summary>
        [MSBuildTestMethod]
        public void SetEmptyValue()
        {
            ProjectMetadataElement metadatum = GetMetadataXml();

            metadatum.Value = String.Empty;
            Assert.AreEqual(String.Empty, metadatum.Value);
        }

        /// <summary>
        /// Set metadatum value to null
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullValue()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectMetadataElement metadatum = GetMetadataXml();

                metadatum.Value = null;
            });
        }
        /// <summary>
        /// Read a metadatum containing an expression like @(..) but whose parent is an ItemDefinitionGroup
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidItemExpressionInMetadata()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i>
                                <m1>@(x)</m1>
                            </i>
                        </ItemDefinitionGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read a metadatum containing an expression like @(..) but whose parent is NOT an ItemDefinitionGroup
        /// </summary>
        [MSBuildTestMethod]
        public void ReadValidItemExpressionInMetadata()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>@(x)</m1>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            // Should not throw
            using ProjectRootElementFromString projectRootElementFromString = new(content);
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i' m1='v1' />
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' m1='v1' />
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadMetadataAsAttribute(string project)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(project);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.ContainsSingle(items);
            Assert.ContainsSingle(items[0].Metadata);

            var metadata = items[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);
        }

        [MSBuildTestMethod]
        public void ReadMetadataAsAttributeOnItemDefinition()
        {
            string project = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1='v1' />
                        </ItemDefinitionGroup>
                    </Project>
                ";
            using ProjectRootElementFromString projectRootElementFromString = new(project);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemDefinitionGroupElement);

            var itemDefinitions = Helpers.MakeList(itemDefinitionGroup.ItemDefinitions);

            Assert.ContainsSingle(itemDefinitions);
            Assert.ContainsSingle(itemDefinitions[0].Metadata);

            var metadata = itemDefinitions[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i' m1='&lt;&amp;>""' />
                        </ItemGroup>
                    </Project>
                ")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' m1='&lt;&amp;>""' />
                            </ItemGroup>
                        </Target>
                    </Project>
                ")]
        public void ReadMetadataAsAttributeWithSpecialCharacters(string project)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(project);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.ContainsSingle(items);
            Assert.ContainsSingle(items[0].Metadata);

            var metadata = items[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual(@"<&>""", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);
        }

        [MSBuildTestMethod]
        public void ReadMetadataAsAttributeOnItemDefinitionWithSpecialCharacters()
        {
            var project = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1='&lt;&amp;>""' />
                        </ItemDefinitionGroup>
                    </Project>
                ";
            using ProjectRootElementFromString projectRootElementFromString = new(project);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemDefinitionGroupElement);

            var itemDefinitions = Helpers.MakeList(itemDefinitionGroup.ItemDefinitions);

            Assert.ContainsSingle(itemDefinitions);
            Assert.ContainsSingle(itemDefinitions[0].Metadata);

            var metadata = itemDefinitions[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual(@"<&>""", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>",
                @"
                    <Project>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v2` />
                        </ItemGroup>
                    </Project>")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1` />
                            </ItemGroup>
                        </Target>
                    </Project>",
                @"
                    <Project>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v2` />
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void UpdateMetadataValueAsAttribute(string projectContents, string updatedProject)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(
                ObjectModelHelpers.CleanupFileContents(projectContents),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;

            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.ContainsSingle(items);
            Assert.ContainsSingle(items[0].Metadata);

            var metadata = items[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            metadata.Value = "v2";

            Assert.IsTrue(project.IsDirty);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        public void UpdateMetadataValueAsAttributeOnItemDefinition()
        {
            var projectContents = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1=`v1` />
                        </ItemDefinitionGroup>
                    </Project>";
            using ProjectRootElementFromString projectRootElementFromString = new(
                ObjectModelHelpers.CleanupFileContents(projectContents),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemDefinitionGroupElement);

            var project = new Project(projectElement);

            var itemDefinitions = Helpers.MakeList(itemDefinitionGroup.ItemDefinitions);

            Assert.ContainsSingle(itemDefinitions);
            Assert.ContainsSingle(itemDefinitions[0].Metadata);

            var metadata = itemDefinitions[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            metadata.Value = "v2";

            Assert.IsTrue(project.IsDirty);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                              ObjectModelHelpers.CleanupFileContents(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1=`v2` />
                        </ItemDefinitionGroup>
                    </Project>");
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        // NOTE: When https://github.com/dotnet/msbuild/issues/362 is fixed, then the expected value in XML may be:
        //      &lt;&amp;>"
        //  instead of:
        //      &lt;&amp;&gt;&quot;
        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>",
                @"
                    <Project>
                        <ItemGroup>
                            <i1 Include=`i` m1=`&lt;&amp;&gt;&quot;` />
                        </ItemGroup>
                    </Project>")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1` />
                            </ItemGroup>
                        </Target>
                    </Project>",
                @"
                    <Project>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`&lt;&amp;&gt;&quot;` />
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void UpdateMetadataValueAsAttributeWithSpecialCharacters(string projectContents, string updatedProject)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(
               ObjectModelHelpers.CleanupFileContents(projectContents),
               ProjectCollection.GlobalProjectCollection,
               preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.ContainsSingle(items);
            Assert.ContainsSingle(items[0].Metadata);

            var metadata = items[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            metadata.Value = @"<&>""";

            Assert.IsTrue(project.IsDirty);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        public void UpdateMetadataValueAsAttributeOnItemDefinitionWithSpecialCharacters()
        {
            var projectContents = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1=`v1` />
                        </ItemDefinitionGroup>
                    </Project>";
            using ProjectRootElementFromString projectRootElementFromString = new(
               ObjectModelHelpers.CleanupFileContents(projectContents),
               ProjectCollection.GlobalProjectCollection,
               preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemDefinitionGroupElement);

            var project = new Project(projectElement);

            var itemDefinitions = Helpers.MakeList(itemDefinitionGroup.ItemDefinitions);

            Assert.ContainsSingle(itemDefinitions);
            Assert.ContainsSingle(itemDefinitions[0].Metadata);

            var metadata = itemDefinitions[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            metadata.Value = @"<&>""";

            Assert.IsTrue(project.IsDirty);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                              ObjectModelHelpers.CleanupFileContents(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1=`&lt;&amp;&gt;&quot;` />
                        </ItemDefinitionGroup>
                    </Project>");
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i'>
                              <m1>v1</m1>
                            </i1>
                        </ItemGroup>
                    </Project>",
                @"
                    <Project>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>")]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i'><m1>v1</m1></i1>
                        </ItemGroup>
                    </Project>",
                @"
                    <Project>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i'>
                                  <m1>v1</m1>
                                </i1>
                            </ItemGroup>
                        </Target>
                    </Project>",
                @"
                    <Project>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1` />
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void ChangeMetadataToAttribute(string projectContents, string updatedProject)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(
               projectContents,
               ProjectCollection.GlobalProjectCollection,
               preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.ContainsSingle(items);
            Assert.ContainsSingle(items[0].Metadata);

            var metadata = items[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsFalse(metadata.ExpressedAsAttribute);

            metadata.ExpressedAsAttribute = true;

            Assert.IsTrue(project.IsDirty);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1>
                              <m1>v1</m1>
                            </i1>
                        </ItemDefinitionGroup>
                    </Project>",
        @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1=`v1` />
                        </ItemDefinitionGroup>
                    </Project>")]
        [DataRow(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1><m1>v1</m1></i1>
                        </ItemDefinitionGroup>
                    </Project>",
        @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1=`v1` />
                        </ItemDefinitionGroup>
                    </Project>")]
        public void ChangeMetadataToAttributeOnItemDefinition(string projectContents, string updatedProject)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(
               projectContents,
               ProjectCollection.GlobalProjectCollection,
               preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemDefinitionGroupElement);

            var project = new Project(projectElement);

            var itemDefinitions = Helpers.MakeList(itemDefinitionGroup.ItemDefinitions);

            Assert.ContainsSingle(itemDefinitions);
            Assert.ContainsSingle(itemDefinitions[0].Metadata);

            var metadata = itemDefinitions[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsFalse(metadata.ExpressedAsAttribute);

            metadata.ExpressedAsAttribute = true;

            Assert.IsTrue(project.IsDirty);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i' m1='v1' />
                        </ItemGroup>
                    </Project>",
                    @"
                    <Project>
                        <ItemGroup>
                            <i1 Include=`i`>
                              <m1>v1</m1>
                            </i1>
                        </ItemGroup>
                    </Project>")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' m1='v1' />
                            </ItemGroup>
                        </Target>
                    </Project>",
                    @"
                    <Project>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i`>
                                  <m1>v1</m1>
                                </i1>
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void ChangeAttributeToMetadata(string projectContents, string updatedProject)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(
                 projectContents,
                 ProjectCollection.GlobalProjectCollection,
                 preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.ContainsSingle(items);
            Assert.ContainsSingle(items[0].Metadata);

            var metadata = items[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            metadata.ExpressedAsAttribute = false;

            Assert.IsFalse(metadata.ExpressedAsAttribute);
            Assert.IsTrue(project.IsDirty);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        public void ChangeAttributeToMetadataOnItemDefinition()
        {
            var projectContents = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1='v1'/>
                        </ItemDefinitionGroup>
                    </Project>";
            using ProjectRootElementFromString projectRootElementFromString = new(
                 projectContents,
                 ProjectCollection.GlobalProjectCollection,
                 preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemDefinitionGroupElement);

            var project = new Project(projectElement);

            var itemDefinitions = Helpers.MakeList(itemDefinitionGroup.ItemDefinitions);

            Assert.ContainsSingle(itemDefinitions);
            Assert.ContainsSingle(itemDefinitions[0].Metadata);

            var metadata = itemDefinitions[0].Metadata.First();
            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);

            metadata.ExpressedAsAttribute = false;

            Assert.IsFalse(metadata.ExpressedAsAttribute);
            Assert.IsTrue(project.IsDirty);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1>
                              <m1>v1</m1>
                            </i1>
                        </ItemDefinitionGroup>
                    </Project>");
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i' />
                        </ItemGroup>
                    </Project>",
        @"
                    <Project>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1` />
                        </ItemGroup>
                    </Project>")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' />
                            </ItemGroup>
                        </Target>
                    </Project>",
        @"
                    <Project>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1` />
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void AddMetadataAsAttribute(string projectContents, string updatedProject)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(
                 projectContents,
                 ProjectCollection.GlobalProjectCollection,
                 preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.ContainsSingle(items);
            Assert.IsEmpty(items[0].Metadata);

            var metadata = items[0].AddMetadata("m1", "v1", true);

            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);
            Assert.IsTrue(project.IsDirty);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        public void AddMetadataAsAttributeToItemDefinition()
        {
            var projectContents = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1/>
                        </ItemDefinitionGroup>
                    </Project>";
            using ProjectRootElementFromString projectRootElementFromString = new(
                 projectContents,
                 ProjectCollection.GlobalProjectCollection,
                 preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemDefinitionGroupElement);

            var project = new Project(projectElement);

            var itemDefinitions = Helpers.MakeList(itemDefinitionGroup.ItemDefinitions);

            Assert.ContainsSingle(itemDefinitions);
            Assert.IsEmpty(itemDefinitions[0].Metadata);

            var metadata = itemDefinitions[0].AddMetadata("m1", "v1", true);

            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);
            Assert.IsTrue(project.IsDirty);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                              ObjectModelHelpers.CleanupFileContents(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1=`v1` />
                        </ItemDefinitionGroup>
                    </Project>");
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        [DataRow(@"
                    <Project>
                        <ItemGroup>
                            <i1 Include='i' />
                        </ItemGroup>
                    </Project>",
        @"
                    <Project>
                        <ItemGroup>
                            <i1 Include=`i` m1=`v1`>
                              <m2>v2</m2>
                            </i1>
                        </ItemGroup>
                    </Project>")]
        [DataRow(@"
                    <Project>
                        <Target Name='t'>
                            <ItemGroup>
                                <i1 Include='i' />
                            </ItemGroup>
                        </Target>
                    </Project>",
        @"
                    <Project>
                        <Target Name=`t`>
                            <ItemGroup>
                                <i1 Include=`i` m1=`v1`>
                                  <m2>v2</m2>
                                </i1>
                            </ItemGroup>
                        </Target>
                    </Project>")]
        public void AddMetadataAsAttributeAndAsElement(string projectContents, string updatedProject)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(
                projectContents,
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemGroupElement);

            var project = new Project(projectElement);

            var items = Helpers.MakeList(itemGroup.Items);

            Assert.ContainsSingle(items);
            Assert.IsEmpty(items[0].Metadata);

            var metadata = items[0].AddMetadata("m1", "v1", true);

            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);
            Assert.IsTrue(project.IsDirty);

            metadata = items[0].AddMetadata("m2", "v2", false);

            Assert.AreEqual("m2", metadata.Name);
            Assert.AreEqual("v2", metadata.Value);
            Assert.IsFalse(metadata.ExpressedAsAttribute);
            Assert.IsTrue(project.IsDirty);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                ObjectModelHelpers.CleanupFileContents(updatedProject);
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [MSBuildTestMethod]
        public void AddMetadataToItemDefinitionAsAttributeAndAsElement()
        {
            var projectContents = @"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1/>
                        </ItemDefinitionGroup>
                    </Project>";
            using ProjectRootElementFromString projectRootElementFromString = new(
               projectContents,
               ProjectCollection.GlobalProjectCollection,
               preserveFormatting: true);
            ProjectRootElement projectElement = projectRootElementFromString.Project;
            ProjectItemDefinitionGroupElement itemDefinitionGroup = (ProjectItemDefinitionGroupElement)projectElement.AllChildren.FirstOrDefault(c => c is ProjectItemDefinitionGroupElement);

            var project = new Project(projectElement);

            var itemDefinitions = Helpers.MakeList(itemDefinitionGroup.ItemDefinitions);

            Assert.ContainsSingle(itemDefinitions);
            Assert.IsEmpty(itemDefinitions[0].Metadata);

            var metadata = itemDefinitions[0].AddMetadata("m1", "v1", true);

            Assert.AreEqual("m1", metadata.Name);
            Assert.AreEqual("v1", metadata.Value);
            Assert.IsTrue(metadata.ExpressedAsAttribute);
            Assert.IsTrue(project.IsDirty);

            metadata = itemDefinitions[0].AddMetadata("m2", "v2", false);

            Assert.AreEqual("m2", metadata.Name);
            Assert.AreEqual("v2", metadata.Value);
            Assert.IsFalse(metadata.ExpressedAsAttribute);
            Assert.IsTrue(project.IsDirty);

            using StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                              ObjectModelHelpers.CleanupFileContents(@"
                    <Project>
                        <ItemDefinitionGroup>
                            <i1 m1=`v1`>
                              <m2>v2</m2>
                            </i1>
                        </ItemDefinitionGroup>
                    </Project>");
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        /// <summary>
        /// Helper to get a ProjectMetadataElement for a simple metadatum
        /// </summary>
        private static ProjectMetadataElement GetMetadataXml()
        {
            string content = @"
                    <Project>
                        <ItemGroup>
                            <i Include='i1'>
                                <m Condition='c'>m1</m>
                            </i>
                        </ItemGroup>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;
            ProjectItemGroupElement itemGroup = (ProjectItemGroupElement)Helpers.GetFirst(project.Children);
            ProjectItemElement item = Helpers.GetFirst(itemGroup.Items);
            ProjectMetadataElement metadata = Helpers.GetFirst(item.Metadata);
            return metadata;
        }

        private void VerifyAssertLineByLine(string expected, string actual)
        {
            Helpers.VerifyAssertLineByLine(expected, actual, false, _testOutput);
        }
    }
}
