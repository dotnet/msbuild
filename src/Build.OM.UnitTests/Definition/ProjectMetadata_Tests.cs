// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectMetadata
    /// </summary>
    public class ProjectMetadata_Tests
    {
        /// <summary>
        /// Project getter
        /// </summary>
        [Fact]
        public void ProjectGetter()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            ProjectMetadata metadatum = item.SetMetadataValue("m", "m1");

            Assert.Equal(true, Object.ReferenceEquals(project, metadatum.Project));
        }

        /// <summary>
        /// Set a new metadata value via the evaluated ProjectMetadata object
        /// </summary>
        [Fact]
        public void SetUnevaluatedValue()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>v1</m1>
                                <m2>v%253</m2>
                            </i>
                        </ItemGroup>
                    </Project>
                ");

            ProjectRootElement projectXml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(projectXml);

            Assert.Equal(false, project.IsDirty);

            Helpers.GetFirst(project.GetItems("i")).SetMetadataValue("m1", "v2");
            Helpers.GetFirst(project.GetItems("i")).SetMetadataValue("m2", "v%214");

            Assert.Equal(true, project.IsDirty);

            StringWriter writer = new EncodingStringWriter();
            projectXml.Save(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>v2</m1>
                                <m2>v%214</m2>
                            </i>
                        </ItemGroup>
                    </Project>
                ");

            Helpers.CompareProjectXml(expected, writer.ToString());
            Assert.Equal("v!4", Helpers.GetFirst(project.GetItems("i")).GetMetadataValue("m2"));
        }

        /// <summary>
        /// If the value doesn't change then the project shouldn't dirty
        /// </summary>
        [Fact]
        public void SetUnchangedValue()
        {
            Project project = new Project();
            ProjectItem item = project.AddItem("i", "i1")[0];
            item.SetMetadataValue("m", "m1");
            project.ReevaluateIfNecessary();

            item.SetMetadataValue("m", "m1");

            Assert.Equal(false, project.IsDirty);

            item.GetMetadata("m").UnevaluatedValue = "m1";

            Assert.Equal(false, project.IsDirty);
        }

        /// <summary>
        /// Properties should be expanded
        /// </summary>
        [Fact]
        public void SetValueWithPropertyExpression()
        {
            Project project = new Project();
            project.SetProperty("p", "p0");
            ProjectItem item = project.AddItem("i", "i1")[0];
            ProjectMetadata metadatum = item.SetMetadataValue("m", "m1");
            project.ReevaluateIfNecessary();

            metadatum.UnevaluatedValue = "$(p)";

            Assert.Equal("$(p)", metadatum.UnevaluatedValue);
            Assert.Equal("p0", metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Items should be expanded
        /// </summary>
        [Fact]
        public void SetValueWithItemExpression()
        {
            Project project = new Project();
            project.AddItem("i", "i1");
            ProjectItem item = project.AddItem("j", "j1")[0];
            ProjectMetadata metadatum = item.SetMetadataValue("m", "@(i)");
            project.ReevaluateIfNecessary();

            metadatum.UnevaluatedValue = "@(i)";

            Assert.Equal("@(i)", metadatum.UnevaluatedValue);
            Assert.Equal("i1", metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value with a qualified metadata expression.
        /// Per 3.5, this expands to nothing.
        /// </summary>
        [Fact]
        public void SetValueWithQualifiedMetadataExpressionOtherItemType()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>v1</m1>
                            </i>
                            <j Include='j1'>
                                <m1>v2</m1>
                            </j>
                        </ItemGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectMetadata metadatum = Helpers.GetFirst(project.GetItems("j")).GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(i.m1)";

            Assert.Equal("%(i.m1)", metadatum.UnevaluatedValue);
            Assert.Equal(String.Empty, metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value with a qualified metadata expression of the same item type
        /// </summary>
        [Fact]
        public void SetValueWithQualifiedMetadataExpressionSameItemType()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m0>v0</m0>
                                <m1>v1</m1>
                            </i>
                        </ItemGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectMetadata metadatum = Helpers.GetFirst(project.GetItems("i")).GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(i.m0)";

            Assert.Equal("%(i.m0)", metadatum.UnevaluatedValue);
            Assert.Equal("v0", metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value with a qualified metadata expression of the same item type
        /// </summary>
        [Fact]
        public void SetValueWithQualifiedMetadataExpressionSameMetadata()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>v1</m1>
                            </i>
                        </ItemGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectMetadata metadatum = Helpers.GetFirst(project.GetItems("i")).GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(i.m1)";

            Assert.Equal("%(i.m1)", metadatum.UnevaluatedValue);
            Assert.Equal(String.Empty, metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value with an unqualified metadata expression
        /// </summary>
        [Fact]
        public void SetValueWithUnqualifiedMetadataExpression()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemGroup>
                            <i Include='i1'>
                                <m0>v0</m0>
                                <m1>v1</m1>
                            </i>
                        </ItemGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectMetadata metadatum = Helpers.GetFirst(project.GetItems("i")).GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(m0)";

            Assert.Equal("%(m0)", metadatum.UnevaluatedValue);
            Assert.Equal("v0", metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value with an unqualified metadata expression
        /// Value from an item definition
        /// </summary>
        [Fact]
        public void SetValueWithUnqualifiedMetadataExpressionFromItemDefinition()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemDefinitionGroup>
                            <i>
                               <m0>v0</m0>
                            </i>
                        </ItemDefinitionGroup>
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>v1</m1>
                            </i>
                        </ItemGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectMetadata metadatum = Helpers.GetFirst(project.GetItems("i")).GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(m0)";

            Assert.Equal("%(m0)", metadatum.UnevaluatedValue);
            Assert.Equal("v0", metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value with a qualified metadata expression
        /// Value from an item definition
        /// </summary>
        [Fact]
        public void SetValueWithQualifiedMetadataExpressionFromItemDefinition()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemDefinitionGroup>
                            <i>
                               <m0>v0</m0>
                            </i>
                        </ItemDefinitionGroup>
                        <ItemGroup>
                            <i Include='i1'>
                                <m1>v1</m1>
                            </i>
                        </ItemGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectMetadata metadatum = Helpers.GetFirst(project.GetItems("i")).GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(i.m0)";

            Assert.Equal("%(i.m0)", metadatum.UnevaluatedValue);
            Assert.Equal("v0", metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value with an qualified metadata expression
        /// of the wrong item type.
        /// Per 3.5, this evaluates to nothing.
        /// </summary>
        [Fact]
        public void SetValueWithQualifiedMetadataExpressionWrongItemType()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemGroup>
                            <j Include='j1'>
                                <m0>v0</m0>
                            </j>
                            <i Include='i1'>
                                <m0>v0</m0>
                                <m1>v1</m1>
                            </i>
                        </ItemGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectMetadata metadatum = Helpers.GetFirst(project.GetItems("i")).GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(j.m0)";

            Assert.Equal("%(j.m0)", metadatum.UnevaluatedValue);
            Assert.Equal(String.Empty, metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value on an item definition with an unqualified metadata expression
        /// </summary>
        [Fact]
        public void SetValueOnItemDefinitionWithUnqualifiedMetadataExpression()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemDefinitionGroup>
                            <i>
                                <m0>v0</m0>
                            </i>
                        </ItemDefinitionGroup>
                        <ItemDefinitionGroup>
                            <i>
                                <m1>v1</m1>
                            </i>
                        </ItemDefinitionGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItemDefinition itemDefinition;
            project.ItemDefinitions.TryGetValue("i", out itemDefinition);
            ProjectMetadata metadatum = itemDefinition.GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(m0)";

            Assert.Equal("%(m0)", metadatum.UnevaluatedValue);
            Assert.Equal("v0", metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value on an item definition with an qualified metadata expression
        /// </summary>
        [Fact]
        public void SetValueOnItemDefinitionWithQualifiedMetadataExpression()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemDefinitionGroup>
                            <i>
                                <m0>v0</m0>
                                <m1>v1</m1>
                            </i>
                        </ItemDefinitionGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItemDefinition itemDefinition;
            project.ItemDefinitions.TryGetValue("i", out itemDefinition);
            ProjectMetadata metadatum = itemDefinition.GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(i.m0)";

            Assert.Equal("%(i.m0)", metadatum.UnevaluatedValue);
            Assert.Equal("v0", metadatum.EvaluatedValue);
        }

        /// <summary>
        /// Set a new metadata value on an item definition with an qualified metadata expression
        /// of the wrong item type.
        /// Per 3.5, this evaluates to empty string.
        /// </summary>
        [Fact]
        public void SetValueOnItemDefinitionWithQualifiedMetadataExpressionWrongItemType()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <ItemDefinitionGroup>
                            <j>
                                <m0>v0</m0>
                            </j>
                            <i>
                                <m0>v0</m0>
                                <m1>v1</m1>
                            </i>
                        </ItemDefinitionGroup>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItemDefinition itemDefinition;
            project.ItemDefinitions.TryGetValue("i", out itemDefinition);
            ProjectMetadata metadatum = itemDefinition.GetMetadata("m1");
            metadatum.UnevaluatedValue = "%(j.m0)";

            Assert.Equal("%(j.m0)", metadatum.UnevaluatedValue);
            Assert.Equal(String.Empty, metadatum.EvaluatedValue);
        }

        /// <summary>
        /// IsImported = false
        /// </summary>
        [Fact]
        public void IsImportedFalse()
        {
            Project project = new Project();
            ProjectMetadata metadata = project.AddItem("i", "i1")[0].SetMetadataValue("m", "m1");

            Assert.Equal(false, metadata.IsImported);
        }

        /// <summary>
        /// Attempt to set metadata on imported item should fail
        /// </summary>
        [Fact]
        public void SetMetadataImported()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement import = ProjectRootElement.Create("import");
                ProjectItemElement itemXml = import.AddItem("i", "i1");
                itemXml.AddMetadata("m", "m0");

                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddImport("import");
                Project project = new Project(xml);

                ProjectItem item = Helpers.GetFirst(project.GetItems("i"));

                ProjectMetadata metadata = item.GetMetadata("m");

                Assert.Equal(true, metadata.IsImported);

                metadata.UnevaluatedValue = "m1";
            }
           );
        }
        /// <summary>
        /// Escaping in metadata values
        /// </summary>
        [Fact]
        public void SpecialCharactersInMetadataValueConstruction()
        {
            string projectString = ObjectModelHelpers.CleanupFileContents(@"<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
    <ItemGroup>
        <None Include='MetadataTests'>
            <EscapedSemicolon>%3B</EscapedSemicolon>
            <EscapedDollarSign>%24</EscapedDollarSign>
        </None>
    </ItemGroup>
</Project>");
            System.Xml.XmlReader reader = XmlReader.Create(new StringReader(projectString));
            Microsoft.Build.Evaluation.Project project = new Microsoft.Build.Evaluation.Project(reader);
            Microsoft.Build.Evaluation.ProjectItem item = project.GetItems("None").Single();

            SpecialCharactersInMetadataValueTests(item);
        }

        /// <summary>
        /// Escaping in metadata values
        /// </summary>
        [Fact]
        public void SpecialCharactersInMetadataValueEvaluation()
        {
            Microsoft.Build.Evaluation.Project project = new Microsoft.Build.Evaluation.Project();
            var metadata = new Dictionary<string, string>
            {
                { "EscapedSemicolon", "%3B" }, // Microsoft.Build.Internal.Utilities.Escape(";")
                { "EscapedDollarSign", "%24" }, // Microsoft.Build.Internal.Utilities.Escape("$")
            };
            Microsoft.Build.Evaluation.ProjectItem item = project.AddItem(
                "None",
                "MetadataTests",
                metadata).Single();

            SpecialCharactersInMetadataValueTests(item);
            project.ReevaluateIfNecessary();
            SpecialCharactersInMetadataValueTests(item);
        }

        /// <summary>
        /// Helper for metadata escaping tests
        /// </summary>
        private void SpecialCharactersInMetadataValueTests(Microsoft.Build.Evaluation.ProjectItem item)
        {
            Assert.Equal("%3B", item.GetMetadata("EscapedSemicolon").UnevaluatedValue);
            Assert.Equal(";", item.GetMetadata("EscapedSemicolon").EvaluatedValue);
            Assert.Equal(";", item.GetMetadataValue("EscapedSemicolon"));

            Assert.Equal("%24", item.GetMetadata("EscapedDollarSign").UnevaluatedValue);
            Assert.Equal("$", item.GetMetadata("EscapedDollarSign").EvaluatedValue);
            Assert.Equal("$", item.GetMetadataValue("EscapedDollarSign"));
        }
    }
}
