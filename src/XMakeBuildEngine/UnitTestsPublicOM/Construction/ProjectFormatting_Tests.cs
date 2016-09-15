using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
        public void Setup()
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

        //  TODO: Test preprocessor formatting (project.SaveLogicalProject): https://github.com/Microsoft/msbuild/issues/362
        //  TODO: If preserveFormatting is true, don't change single quotes to double quotes
        //  TODO: Add tests for preserving formatting when mutating project (ie adding an item, modifying a property)

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
        public void ProjectQuoteFormatting()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets='Build' ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
  <ItemGroup>
    <ProjectReference Include='..\CLREXE\CLREXE.vcxproj'>
      <Project>{3699f81b-2d03-46c5-abd7-e88a4c946f28}</Project>
    </ProjectReference>
  </ItemGroup>
</Project>");

            string reformattedContent = ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <ProjectReference Include=""..\CLREXE\CLREXE.vcxproj"">
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
            ProjectItemElement item = xml.AddItem("Compile", "Class1.cs");
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
            ProjectItemElement item = xml.AddItem("Compile", "Class2.cs");
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
            ProjectItemElement item = xml.AddItem("Compile", "Program.cs");
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
    }
}
