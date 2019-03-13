using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.Build.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.OM.UnitTests.Construction
{
    public class ProjectFormatting_Tests : IDisposable
    {
        private readonly ITestOutputHelper _testOutput;

        public ProjectFormatting_Tests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            Setup();
        }

        /// <summary>
        /// Clear out the cache
        /// </summary>
        private void Setup()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        /// <summary>
        /// Clear out the cache
        /// </summary>
        public void Dispose()
        {
            Setup();
        }

        [Fact]
        public void ProjectCommentFormatting()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj`><!-- Comment -->
      <Project>{3699f81b-2d03-46c5-abd7-e88a4c946f28}</Project>
    </ProjectReference>
  </ItemGroup>
</Project>");

            string reformattedContent = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj`>
      <!-- Comment -->
      <Project>{3699f81b-2d03-46c5-abd7-e88a4c946f28}</Project>
    </ProjectReference>
  </ItemGroup>
</Project>");

            VerifyFormattingPreserved(content);
            VerifyProjectReformatting(content, reformattedContent);
        }

        [Fact]
        public void ProjectWhitespaceFormatting()
        {
            //  Note that there are two spaces after the <ItemGroup> tag on the second line
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>  
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj`>
<Project>{3699f81b-2d03-46c5-abd7-e88a4c946f28}</Project>
    </ProjectReference>
  </ItemGroup>
</Project>");

            string reformattedContent = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj`>
      <Project>{3699f81b-2d03-46c5-abd7-e88a4c946f28}</Project>
    </ProjectReference>
  </ItemGroup>
</Project>");

            VerifyFormattingPreserved(content);
            VerifyProjectReformatting(content, reformattedContent);
        }

        [Fact]
        public void ProjectAddItemFormatting_StartOfGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class2.cs"" />
    <Compile Include=""Program.cs""/>
  </ItemGroup>
</Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            Project project = new Project(xml);
            project.AddItem("Compile", "Class1.cs");
            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-16""?>
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
    <Compile Include=""Class2.cs"" />
    <Compile Include=""Program.cs"" />
  </ItemGroup>
</Project>");

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ProjectAddItemFormatting_MiddleOfGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
    <Compile Include=""Program.cs""/>
  </ItemGroup>
</Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            Project project = new Project(xml);
            project.AddItem("Compile", "Class2.cs");
            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-16""?>
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
    <Compile Include=""Class2.cs"" />
    <Compile Include=""Program.cs"" />
  </ItemGroup>
</Project>");

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ProjectAddItemFormatting_EndOfGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
    <Compile Include=""Class2.cs""/>
  </ItemGroup>
</Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            Project project = new Project(xml);
            project.AddItem("Compile", "Program.cs");
            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-16""?>
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
    <Compile Include=""Class2.cs"" />
    <Compile Include=""Program.cs"" />
  </ItemGroup>
</Project>");

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ProjectAddItemFormatting_EmptyGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
  </ItemGroup>
</Project>");
            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            Project project = new Project(xml);
            project.AddItem("Compile", "Program.cs");
            StringWriter writer = new EncodingStringWriter();
            project.Save(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Program.cs"" />
  </ItemGroup>
</Project>");

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ProjectAddItemFormatting_NoItemGroup()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
</Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            Project project = new Project(xml);
            project.AddItem("Compile", "Program.cs");
            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-16""?>
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Program.cs"" />
  </ItemGroup>
</Project>");

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ProjectRemoveItemFormatting()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
    <Compile Include=""Class2.cs""/>
    <Compile Include=""Program.cs""/>
  </ItemGroup>
</Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            Project project = new Project(xml);

            var itemToRemove = project.GetItems("Compile").Single(item => item.EvaluatedInclude == "Class2.cs");
            project.RemoveItem(itemToRemove);
            
            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-16""?>
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
    <Compile Include=""Program.cs"" />
  </ItemGroup>
</Project>");

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ProjectAddItemMetadataFormatting()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
    <Compile Include=""Class2.cs""/>
    <Compile Include=""Program.cs""/>
  </ItemGroup>
</Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            Project project = new Project(xml);

            var itemToEdit = project.GetItems("Compile").Single(item => item.EvaluatedInclude == "Class2.cs");
            itemToEdit.SetMetadataValue("ExcludeFromStyleCop", "true");
            
            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-16""?>
<Project DefaultTargets=`Build` ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
    <Compile Include=""Class1.cs"" />
    <Compile Include=""Class2.cs"">
      <ExcludeFromStyleCop>true</ExcludeFromStyleCop>
    </Compile>
    <Compile Include=""Program.cs"" />
  </ItemGroup>
</Project>");

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/362")]
        public void PreprocessorFormatting()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets='Build' ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
  <Target
    Name=""XamlPreCompile""
    Inputs=""$(MSBuildAllProjects);
           @(Compile);
           @(_CoreCompileResourceInputs);""
  />
</Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)), ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            Project project = new Project(xml);

            StringWriter writer = new StringWriter();

            project.SaveLogicalProject(writer);

            string actual = writer.ToString();
            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                content;

            VerifyAssertLineByLine(expected, actual);
        }

        void VerifyFormattingPreserved(string projectContents)
        {
            VerifyFormattingPreservedFromString(projectContents);
            VerifyFormattingPreservedFromFile(projectContents);
        }

        void VerifyFormattingPreservedFromString(string projectContents)
        {
            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectContents)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: true);
            Project project = new Project(xml);
            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                projectContents;
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        void VerifyFormattingPreservedFromFile(string projectContents)
        {
            string directory = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(directory);

                string file = Path.Combine(directory, "test.proj");
                File.WriteAllText(file, projectContents);

                ProjectRootElement xml = ProjectRootElement.Open(file, ProjectCollection.GlobalProjectCollection,
                    preserveFormatting: true);
                Project project = new Project(xml);
                StringWriter writer = new StringWriter();
                project.Save(writer);

                string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                    projectContents;
                string actual = writer.ToString();

                VerifyAssertLineByLine(expected, actual);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        void VerifyProjectReformatting(string originalContents, string expectedContents)
        {
            VerifyProjectReformattingFromString(originalContents, expectedContents);
            VerifyProjectReformattingFromFile(originalContents, expectedContents);
        }

        void VerifyProjectReformattingFromString(string originalContents, string expectedContents)
        {
            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(originalContents)),
                ProjectCollection.GlobalProjectCollection,
                preserveFormatting: false);
            Project project = new Project(xml);
            StringWriter writer = new StringWriter();
            project.Save(writer);

            string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                expectedContents;
            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        void VerifyProjectReformattingFromFile(string originalContents, string expectedContents)
        {
            string directory = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(directory);

                string file = Path.Combine(directory, "test.proj");
                File.WriteAllText(file, originalContents);

                ProjectRootElement xml = ProjectRootElement.Open(file, ProjectCollection.GlobalProjectCollection,
                    preserveFormatting: false);
                Project project = new Project(xml);
                StringWriter writer = new StringWriter();
                project.Save(writer);

                string expected = @"<?xml version=""1.0"" encoding=""utf-16""?>" +
                    expectedContents;
                string actual = writer.ToString();

                VerifyAssertLineByLine(expected, actual);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        void VerifyAssertLineByLine(string expected, string actual)
        {
            Helpers.VerifyAssertLineByLine(expected, actual, false, _testOutput);
        }

        [Fact]
        public void VerifyNamespaceRemainsWhenPresent()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project xmlns=`msbuildnamespace`>
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj`>
      <Project>{3699f81b-2d03-46c5-abd7-e88a4c946f28}</Project>
    </ProjectReference>
  </ItemGroup>
</Project>");

            VerifyFormattingPreserved(content);
        }

        [Fact]
        public void VerifyNoNamespaceRemains()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project>
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj` />
  </ItemGroup>
</Project>");

            VerifyFormattingPreserved(content);
        }

        [Fact]
        public void DefaultProjectSaveContainsAllNewFileOptions()
        {
            // XML declaration tag, namespace, and tools version must be present by default.
            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj`>
      <metadata>value</metadata>
    </ProjectReference>
  </ItemGroup>
</Project>");

            Project project = new Project();
            project.AddItem("ProjectReference", @"..\CLREXE\CLREXE.vcxproj",
                new[] {new KeyValuePair<string, string>("metadata", "value")});
            
            StringWriter writer = new EncodingStringWriter();
            project.Save(writer);

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void NewProjectSaveWithOptionsNone()
        {
            // When NewProjectFileOptions.None is specified, we should not have an XML declaration,
            // tools version, or namespace in the project file.
            string expected = ObjectModelHelpers.CleanupFileContents(@"<Project>
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj`>
      <metadata>value</metadata>
    </ProjectReference>
  </ItemGroup>
</Project>");

            Project project = new Project(NewProjectFileOptions.None);
            var item = project.AddItem("ProjectReference", @"..\CLREXE\CLREXE.vcxproj");
            item[0].SetMetadataValue("metadata", "value");

            StringWriter writer = new EncodingStringWriter();
            project.Save(writer);

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ChangeItemTypeNoNamespace()
        {
            string expected = ObjectModelHelpers.CleanupFileContents(@"<Project>
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj` />
  </ItemGroup>
</Project>");

            Project project = new Project(NewProjectFileOptions.None);
            var item = project.AddItem("NotProjectReference", @"..\CLREXE\CLREXE.vcxproj");
            item[0].ItemType = "ProjectReference";

            StringWriter writer = new EncodingStringWriter();
            project.Save(writer);

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ChangeItemTypeWithNamespace()
        {
            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=`1.0` encoding=`utf-16`?>
<Project xmlns=`msbuildnamespace`>
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj` />
  </ItemGroup>
</Project>");

            Project project = new Project(NewProjectFileOptions.IncludeXmlNamespace);
            var item = project.AddItem("NotProjectReference", @"..\CLREXE\CLREXE.vcxproj");
            item[0].ItemType = "ProjectReference";

            // StringWriter is UTF16 (will output xml declaration)
            StringWriter writer = new StringWriter();
            project.Save(writer);

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ChangeItemTypeWithXmlHeader()
        {
            string expected = ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
  <ItemGroup>
    <ProjectReference Include=`..\CLREXE\CLREXE.vcxproj` />
  </ItemGroup>
</Project>");

            Project project = new Project(NewProjectFileOptions.IncludeXmlDeclaration);
            var item = project.AddItem("NotProjectReference", @"..\CLREXE\CLREXE.vcxproj");
            item[0].ItemType = "ProjectReference";

            // Should still output XML declaration even when using UTF8 (NewProjectFileOptions.IncludeXmlDeclaration
            // was specified)
            StringWriter writer = new EncodingStringWriter();
            project.Save(writer);

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void VerifyUtf8WithoutBomTreatedAsUtf8()
        {
            string expected = ObjectModelHelpers.CleanupFileContents(@"<Project>
</Project>");

            Project project = new Project(NewProjectFileOptions.None);
            StringWriter writer = new EncodingStringWriter(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            project.Save(writer);

            string actual = writer.ToString();

            VerifyAssertLineByLine(expected, actual);
        }

        [Fact]
        public void ProjectFileWithBomContainsBomAfterSave()
        {
            CreateProjectAndAssertEncoding(xmlDeclaration: false, byteOrderMark: true);
        }

        [Fact]
        public void ProjectFileWithoutBomDoesNotContainsBomAfterSave()
        {
            CreateProjectAndAssertEncoding(xmlDeclaration: false, byteOrderMark: false);
        }

        [Fact]
        public void ProjectFileWithoutBomWithXmlDeclarationDoesNotContainsBomAfterSave()
        {
            CreateProjectAndAssertEncoding(xmlDeclaration: true, byteOrderMark: false);
        }

        [Fact]
        public void ProjectFileWithBomWithXmlDeclarationContainsBomAfterSave()
        {
            CreateProjectAndAssertEncoding(xmlDeclaration: true, byteOrderMark: true);
        }

        private static void CreateProjectAndAssertEncoding(bool xmlDeclaration, bool byteOrderMark)
        {
            string declaration = @"<?xml version=""1.0"" encoding=""utf-8""?>";

            string content = xmlDeclaration ? declaration : string.Empty;
            content += @"<Project><Target Name=""Build""/></Project>";
            content = ObjectModelHelpers.CleanupFileContents(content);

            var file = FileUtilities.GetTemporaryFile(".proj");
            try
            {
                File.WriteAllText(file, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: byteOrderMark));
                Assert.Equal(byteOrderMark, EncodingUtilities.FileStartsWithPreamble(file));

                // Load and manipulate/save the project
                var project = new Project(ProjectRootElement.Open(file, ProjectCollection.GlobalProjectCollection));
                project.AddItem("Compile", "Program.cs");
                project.Save();

                // Ensure the file was really saved and that the presence of a BOM has not changed
                string actualContents = File.ReadAllText(file);
                Assert.Contains("<Compile Include=\"Program.cs\" />", actualContents);
                if (xmlDeclaration)
                {
                    Assert.Contains(declaration, actualContents);
                }
                else
                {
                    Assert.DoesNotContain(declaration, actualContents);
                }
                Assert.Equal(byteOrderMark, EncodingUtilities.FileStartsWithPreamble(file));
            }
            finally
            {
                FileUtilities.DeleteDirectoryNoThrow(Path.GetDirectoryName(file), false);
            }
        }
    }
}
