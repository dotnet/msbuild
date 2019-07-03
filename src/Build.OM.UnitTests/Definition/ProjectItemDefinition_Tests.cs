// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectItemDefinition
    /// </summary>
    public class ProjectItemDefinition_Tests
    {
        /// <summary>
        /// Add metadata; should add to an existing item definition group that has item definitions of the same item type
        /// </summary>
        [Fact]
        public void AddMetadataExistingItemDefinitionGroup()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinitionGroup().AddItemDefinition("i").AddMetadata("m", "m0");

            Project project = new Project(xml);
            project.ItemDefinitions["i"].SetMetadataValue("n", "n0");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <m>m0</m>
    </i>
    <i>
      <n>n0</n>
    </i>
  </ItemDefinitionGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Set metadata with property expression; should be expanded
        /// </summary>
        [Fact]
        public void SetMetadata()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddProperty("p", "v");
            xml.AddItemDefinitionGroup().AddItemDefinition("i").AddMetadata("m", "m0");
            xml.AddItem("i", "i1");

            Project project = new Project(xml);

            ProjectMetadata metadatum = project.ItemDefinitions["i"].GetMetadata("m");

            metadatum.UnevaluatedValue = "$(p)";

            Assert.Equal("v", Helpers.GetFirst(project.GetItems("i")).GetMetadataValue("m"));
        }

        /// <summary>
        /// Access metadata when there isn't any
        /// </summary>
        [Fact]
        public void EmptyMetadataCollection()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinitionGroup().AddItemDefinition("i");
            Project project = new Project(xml);

            ProjectItemDefinition itemDefinition = project.ItemDefinitions["i"];
            IEnumerable<ProjectMetadata> metadataCollection = itemDefinition.Metadata;

            List<ProjectMetadata> metadataList = Helpers.MakeList(metadataCollection);

            Assert.Empty(metadataList);

            Assert.Null(itemDefinition.GetMetadata("m"));
        }

        /// <summary>
        /// Set metadata get collection
        /// </summary>
        [Fact]
        public void GetMetadataCollection()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinitionGroup().AddItemDefinition("i").AddMetadata("m", "m0");

            Project project = new Project(xml);

            IEnumerable<ProjectMetadata> metadataCollection = project.ItemDefinitions["i"].Metadata;

            List<ProjectMetadata> metadataList = Helpers.MakeList(metadataCollection);

            Assert.Single(metadataList);
            Assert.Equal("m", metadataList[0].Name);
            Assert.Equal("m0", metadataList[0].EvaluatedValue);
        }

        /// <summary>
        /// Attempt to update metadata on imported item definition should fail
        /// </summary>
        [Fact]
        public void UpdateMetadataImported()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string file = null;

                try
                {
                    file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                    ProjectRootElement import = ProjectRootElement.Create(file);
                    import.AddItemDefinitionGroup().AddItemDefinition("i").AddMetadata("m", "m0");
                    import.Save();

                    ProjectRootElement main = ProjectRootElement.Create();
                    Project project = new Project(main);
                    main.AddImport(file);
                    project.ReevaluateIfNecessary();

                    ProjectItemDefinition definition = project.ItemDefinitions["i"];
                    definition.SetMetadataValue("m", "m1");
                }
                finally
                {
                    File.Delete(file);
                }
            }
           );
        }
        /// <summary>
        /// Attempt to add new metadata on imported item definition should succeed,
        /// creating a new item definition in the main project
        /// </summary>
        [Fact]
        public void SetMetadataImported()
        {
            string file = null;

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                ProjectRootElement import = ProjectRootElement.Create(file);
                import.AddItemDefinitionGroup().AddItemDefinition("i").AddMetadata("m", "m0");
                import.Save();

                ProjectRootElement main = ProjectRootElement.Create();
                Project project = new Project(main);
                main.AddImport(file);
                project.ReevaluateIfNecessary();

                ProjectItemDefinition definition = project.ItemDefinitions["i"];
                definition.SetMetadataValue("n", "n0");

                string expected = String.Format
                    (
    ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <n>n0</n>
    </i>
  </ItemDefinitionGroup>
  <Import Project=""{0}"" />
</Project>"),
                   file
                   );

                Helpers.VerifyAssertProjectContent(expected, project.Xml);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Item definition metadata should be sufficient to avoid errors like
        /// "error MSB4096: The item "a.foo" in item list "h" does not define a value for metadata "m".  In
        /// order to use this metadata, either qualify it by specifying %(h.m), or ensure that all items in this list define a value
        /// for this metadata."
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void BatchingConsidersItemDefinitionMetadata()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <m>m1</m>
    </i>
  </ItemDefinitionGroup>
  <ItemGroup>
    <i Include='a.foo;a.bar'/>
  </ItemGroup>
  <Target Name='t'>
    <Message Text='@(i)/%(m)'/>
  </Target>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            MockLogger logger = new MockLogger();
            List<ILogger> loggers = new List<ILogger>() { logger };
            Assert.True(project.Build(loggers));

            logger.AssertLogContains("a.foo;a.bar/m1");
            logger.AssertNoErrors();
            logger.AssertNoWarnings();
        }

        /// <summary>
        /// Expand built-in metadata "late"
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <m>%(filename)</m>
    </i>
  </ItemDefinitionGroup>
  <ItemGroup>
    <i Include='" + (NativeMethodsShared.IsWindows ? @"c:\a\b.ext" : "/a/b.ext") + @"'/>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.Equal("b", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Expand built-in metadata "late"
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_ReferToMetadataAbove()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <m>%(filename)</m>
      <m>%(m)%(extension)</m>
    </i>
  </ItemDefinitionGroup>
  <ItemGroup>
    <i Include='" + (NativeMethodsShared.IsWindows ? @"c:\a\b.ext" : "/a/b.ext") + @"'/>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.Equal("b.ext", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Expand built-in metadata "late"
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_MixtureOfCustomAndBuiltIn()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <l>l1</l>
      <m>%(filename).%(l)</m>
    </i>
  </ItemDefinitionGroup>
  <ItemGroup>
    <i Include='" + (NativeMethodsShared.IsWindows ? @"c:\a\b.ext" : "/a/b.ext") + @"'/>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.Equal("b.l1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Custom metadata expressions on metadata on an ItemDefinitionGroup is still always
        /// expanded right there.
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_CustomEvaluationNeverDelayed()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <n>n1</n>
      <m>%(filename).%(n)</m>
      <n>n2</n>
    </i>
  </ItemDefinitionGroup>
  <ItemGroup>
    <i Include='" + (NativeMethodsShared.IsWindows ? @"c:\a\b.ext" : "/a/b.ext") + @"'>
      <n>n3</n>
    </i>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.Equal("b.n1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// A custom metadata that bizarrely expands to a built in metadata expression should
        /// not evaluate again.
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_DoNotDoubleEvaluate()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <n>%25(filename)</n> <!-- escaped % sign -->
      <m>%(n)</m>
    </i>
  </ItemDefinitionGroup>
  <ItemGroup>
    <i Include='c:\a\b.ext'/>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);

            Assert.Equal("%25(filename)", Project.GetMetadataValueEscaped(item, "m"));
            Assert.Equal("%(filename)", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Items created from other items should still have the built-in metadata expanded
        /// on them, not the original items.
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_CopyItems()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <m>%(extension)</m>
    </i>
  </ItemDefinitionGroup>
  <ItemGroup>
    <h Include='a.foo'/>
    <i Include=""@(h->'%(identity).bar')""/>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.Equal(".bar", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Items created from other items should still have the built-in metadata expanded
        /// on them, not the original items.
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_UseInTransform()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <h>
      <m>%(extension)</m>
    </h>
  </ItemDefinitionGroup>
  <ItemGroup>
    <h Include='a.foo'/>
    <i Include=""@(h->'%(m)')""/>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.Equal(".foo", item.EvaluatedInclude);
        }

        /// <summary>
        /// Items created from other items should still have the built-in metadata expanded
        /// on them, not the original items.
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void ExpandBuiltInMetadataAtPointOfUse_UseInBatching()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <h>
      <m>%(extension)</m>
    </h>
  </ItemDefinitionGroup>
  <ItemGroup>
    <h Include='a.foo;a.bar'/>
  </ItemGroup>
  <Target Name='t'>
    <ItemGroup>
      <i Include=""@(h)"">
         <n Condition=""'%(m)'=='.foo'"">n1</n>
      </i>
    </ItemGroup>
  </Target>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectInstance instance = project.CreateProjectInstance();
            MockLogger l = new MockLogger();
            List<ILogger> loggers = new List<ILogger>() { l };
            instance.Build(loggers);

            ProjectItemInstance item1 = instance.GetItems("i").ElementAt(0);
            Assert.Equal("n1", item1.GetMetadataValue("n"));

            ProjectItemInstance item2 = instance.GetItems("i").ElementAt(1);
            Assert.Equal("", item2.GetMetadataValue("n"));
        }

        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_BuiltInProhibitedOnItemDefinitionMetadataCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content =
    ObjectModelHelpers.CleanupFileContents(
    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <m Condition=""'%(filename)'!=''"">m1</m>
    </i>
  </ItemDefinitionGroup>
</Project>");

                Project project = new Project(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_UnquotedBuiltInProhibitedOnItemDefinitionMetadataCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content =
    ObjectModelHelpers.CleanupFileContents(
    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <m Condition=""%(filename)!=''"">m1</m>
    </i>
  </ItemDefinitionGroup>
</Project>");

                Project project = new Project(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_BuiltInProhibitedOnItemDefinitionCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content =
    ObjectModelHelpers.CleanupFileContents(
    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i Condition=""'%(filename)'!=''"">
      <m>m1</m>
    </i>
  </ItemDefinitionGroup>
</Project>");

                Project project = new Project(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_BuiltInProhibitedOnItemDefinitionGroupCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content =
    ObjectModelHelpers.CleanupFileContents(
    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup Condition=""'%(filename)'!=''"">
    <i>
      <m>m1</m>
    </i>
  </ItemDefinitionGroup>
</Project>");

                Project project = new Project(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_QualifiedBuiltInProhibitedOnItemDefinitionMetadataCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content =
    ObjectModelHelpers.CleanupFileContents(
    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i>
      <m Condition=""'%(i.filename)'!=''"">m1</m>
    </i>
  </ItemDefinitionGroup>
</Project>");

                Project project = new Project(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_QualifiedBuiltInProhibitedOnItemDefinitionCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content =
    ObjectModelHelpers.CleanupFileContents(
    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i Condition=""'%(i.filename)'!=''"">
      <m>m1</m>
    </i>
  </ItemDefinitionGroup>
</Project>");

                Project project = new Project(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_QualifiedBuiltInProhibitedOnItemDefinitionGroupCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content =
    ObjectModelHelpers.CleanupFileContents(
    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup Condition=""'%(i.filename)'!=''"">
    <i>
      <m>m1</m>
    </i>
  </ItemDefinitionGroup>
</Project>");

                Project project = new Project(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_UnquotedQualifiedBuiltInProhibitedOnItemDefinitionCondition()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content =
    ObjectModelHelpers.CleanupFileContents(
    @"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i Condition=""%(i.filename)!=''"">
      <m>m1</m>
    </i>
  </ItemDefinitionGroup>
</Project>");

                Project project = new Project(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Custom metadata is allowed in item definition conditions.
        /// </summary>
        [Fact]
        public void ExpandBuiltInMetadataAtPointOfUse_UnquotedQualifiedCustomAllowedOnItemDefinitionCondition()
        {
            string content =
ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i Condition=""%(i.custom)!=''"">
      <m Condition=""%(i.custom)!=''"">m1</m>
    </i>
  </ItemDefinitionGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));  // No exception
        }
    }
}
