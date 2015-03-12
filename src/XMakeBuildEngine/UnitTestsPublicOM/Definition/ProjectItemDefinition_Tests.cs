//-----------------------------------------------------------------------
// <copyright file="ProjectItemDefinition_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for ProjectItemDefinition</summary>
//-----------------------------------------------------------------------

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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectItemDefinition
    /// </summary>
    [TestClass]
    public class ProjectItemDefinition_Tests
    {
        /// <summary>
        /// Add metadata; should add to an existing item definition group that has item definitions of the same item type
        /// </summary>
        [TestMethod]
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
        [TestMethod]
        public void SetMetadata()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddProperty("p", "v");
            xml.AddItemDefinitionGroup().AddItemDefinition("i").AddMetadata("m", "m0");
            xml.AddItem("i", "i1");

            Project project = new Project(xml);

            ProjectMetadata metadatum = project.ItemDefinitions["i"].GetMetadata("m");

            metadatum.UnevaluatedValue = "$(p)";

            Assert.AreEqual("v", Helpers.GetFirst(project.GetItems("i")).GetMetadataValue("m"));
        }

        /// <summary>
        /// Access metadata when there isn't any
        /// </summary>
        [TestMethod]
        public void EmptyMetadataCollection()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinitionGroup().AddItemDefinition("i");
            Project project = new Project(xml);

            ProjectItemDefinition itemDefinition = project.ItemDefinitions["i"];
            IEnumerable<ProjectMetadata> metadataCollection = itemDefinition.Metadata;

            List<ProjectMetadata> metadataList = Helpers.MakeList(metadataCollection);

            Assert.AreEqual(0, metadataList.Count);

            Assert.AreEqual(null, itemDefinition.GetMetadata("m"));
        }

        /// <summary>
        /// Set metadata get collection
        /// </summary>
        [TestMethod]
        public void GetMetadataCollection()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.AddItemDefinitionGroup().AddItemDefinition("i").AddMetadata("m", "m0");

            Project project = new Project(xml);

            IEnumerable<ProjectMetadata> metadataCollection = project.ItemDefinitions["i"].Metadata;

            List<ProjectMetadata> metadataList = Helpers.MakeList(metadataCollection);

            Assert.AreEqual(1, metadataList.Count);
            Assert.AreEqual("m", metadataList[0].Name);
            Assert.AreEqual("m0", metadataList[0].EvaluatedValue);
        }

        /// <summary>
        /// Attempt to update metadata on imported item definition should fail
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void UpdateMetadataImported()
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

        /// <summary>
        /// Attempt to add new metadata on imported item definition should succeed,
        /// creating a new item definition in the main project
        /// </summary>
        [TestMethod]
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
        [TestMethod]
        [TestCategory("serialize")]
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
            Assert.AreEqual(true, project.Build(loggers));

            logger.AssertLogContains("a.foo;a.bar/m1");
            logger.AssertNoErrors();
            logger.AssertNoWarnings();
        }

        /// <summary>
        /// Expand built-in metadata "late"
        /// </summary>
        [TestMethod]
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
    <i Include='c:\a\b.ext'/>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.AreEqual("b", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Expand built-in metadata "late"
        /// </summary>
        [TestMethod]
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
    <i Include='c:\a\b.ext'/>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.AreEqual("b.ext", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Expand built-in metadata "late"
        /// </summary>
        [TestMethod]
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
    <i Include='c:\a\b.ext'/>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.AreEqual("b.l1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Custom metadata expressions on metadata on an ItemDefinitionGroup is still always
        /// expanded right there.
        /// </summary>
        [TestMethod]
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
    <i Include='c:\a\b.ext'>
      <n>n3</n>
    </i>
  </ItemGroup>
</Project>");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            ProjectItem item = project.GetItems("i").ElementAt(0);
            Assert.AreEqual("b.n1", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// A custom metadata that bizarrely expands to a built in metadata expression should
        /// not evaluate again.
        /// </summary>
        [TestMethod]
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

            Assert.AreEqual("%25(filename)", Project.GetMetadataValueEscaped(item, "m"));
            Assert.AreEqual("%(filename)", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Items created from other items should still have the built-in metadata expanded
        /// on them, not the original items.
        /// </summary>
        [TestMethod]
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
            Assert.AreEqual(".bar", item.GetMetadataValue("m"));
        }

        /// <summary>
        /// Items created from other items should still have the built-in metadata expanded
        /// on them, not the original items.
        /// </summary>
        [TestMethod]
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
            Assert.AreEqual(".foo", item.EvaluatedInclude);
        }

        /// <summary>
        /// Items created from other items should still have the built-in metadata expanded
        /// on them, not the original items.
        /// </summary>
        [TestMethod]
        [TestCategory("serialize")]
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
            Assert.AreEqual("n1", item1.GetMetadataValue("n"));

            ProjectItemInstance item2 = instance.GetItems("i").ElementAt(1);
            Assert.AreEqual("", item2.GetMetadataValue("n"));
        }

        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpandBuiltInMetadataAtPointOfUse_BuiltInProhibitedOnItemDefinitionMetadataCondition()
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

        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpandBuiltInMetadataAtPointOfUse_UnquotedBuiltInProhibitedOnItemDefinitionMetadataCondition()
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

        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpandBuiltInMetadataAtPointOfUse_BuiltInProhibitedOnItemDefinitionCondition()
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

        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpandBuiltInMetadataAtPointOfUse_BuiltInProhibitedOnItemDefinitionGroupCondition()
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

        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpandBuiltInMetadataAtPointOfUse_QualifiedBuiltInProhibitedOnItemDefinitionMetadataCondition()
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

        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpandBuiltInMetadataAtPointOfUse_QualifiedBuiltInProhibitedOnItemDefinitionCondition()
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

        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpandBuiltInMetadataAtPointOfUse_QualifiedBuiltInProhibitedOnItemDefinitionGroupCondition()
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

        /// <summary>
        /// Built-in metadata is prohibited in item definition conditions.
        /// Ideally it would also be late evaluated, but that's too difficult. 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ExpandBuiltInMetadataAtPointOfUse_UnquotedQualifiedBuiltInProhibitedOnItemDefinitionCondition()
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

        /// <summary>
        /// Custom metadata is allowed in item definition conditions.
        /// </summary>
        [TestMethod]
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
