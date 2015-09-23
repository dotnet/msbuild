// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for Project</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ToolLocationHelper = Microsoft.Build.Utilities.ToolLocationHelper;
using TargetDotNetFrameworkVersion = Microsoft.Build.Utilities.TargetDotNetFrameworkVersion;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for Project public members
    /// </summary>
    public class Project_Tests : IDisposable
    {
        /// <summary>
        /// Clear out the global project collection
        /// </summary>
        public Project_Tests()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Clear out the global project collection
        /// </summary>
        public void Dispose()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            Assert.Equal(0, ProjectCollection.GlobalProjectCollection.Count);

            IDictionary<string, string> globalProperties = ProjectCollection.GlobalProjectCollection.GlobalProperties;
            foreach (string propertyName in globalProperties.Keys)
            {
                ProjectCollection.GlobalProjectCollection.RemoveGlobalProperty(propertyName);
            }

            Assert.Equal(0, ProjectCollection.GlobalProjectCollection.GlobalProperties.Count);
        }

        /// <summary>
        /// Since when the project file is saved it may be intented we want to make sure the indent charachters do not affect the evaluation against empty. 
        /// We test here newline, tab, and carriage return.
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void VerifyNewLinesAndTabsEvaluateToEmpty()
        {
            MockLogger mockLogger = new MockLogger();

            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                       <PropertyGroup><NewLine>" + Environment.NewLine + Environment.NewLine + "</NewLine></PropertyGroup>" +
                       "<PropertyGroup><Tab>\t\t\t\t</Tab></PropertyGroup>" +
                       "<PropertyGroup><CarriageReturn>\r\r\r\r</CarriageReturn></PropertyGroup>" +
                        @"<PropertyGroup><Message1 Condition =""'$(NewLine)' == ''"">NewLineEvalAsEmpty</Message1></PropertyGroup>
                        <PropertyGroup><Message2 Condition =""'$(Tab)' == ''"">TabEvalAsEmpty</Message2></PropertyGroup>
                        <PropertyGroup><Message3 Condition =""'$(CarriageReturn)' == ''"">CarriageReturnEvalAsEmpty</Message3></PropertyGroup>

                        <Target Name=""BUild"">
                           <Message Text=""$(Message1)"" Importance=""High""/>
                          <Message Text=""$(Message2)"" Importance=""High""/>
                          <Message Text=""$(Message3)"" Importance=""High""/>
                       </Target>                    
                    </Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));
            Project project = new Project(xml);
            bool result = project.Build(new ILogger[] { mockLogger });
            Assert.Equal(true, result);
            mockLogger.AssertLogContains("NewLineEvalAsEmpty");
            mockLogger.AssertLogContains("TabEvalAsEmpty");
            mockLogger.AssertLogContains("CarriageReturnEvalAsEmpty");
        }

        /// <summary>
        /// Make sure if we build a project and specify no loggers that the loggers registered on the project collection is the one used.
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void LogWithLoggersOnProjectCollection()
        {
            MockLogger mockLogger = new MockLogger();

            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                      <Target Name=""BUild"">
                           <Message Text=""IHaveBeenLogged"" Importance=""High""/>
                       </Target>                    
                    </Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));
            ProjectCollection collection = new ProjectCollection();
            collection.RegisterLogger(mockLogger);
            Project project = new Project(xml, null, null, collection);

            bool result = project.Build();
            Assert.Equal(true, result);
            mockLogger.AssertLogContains("IHaveBeenLogged");
        }

        /// <summary>
        /// Make sure if we build a project and specify we specify a custom logger that the custom logger is used instead of the one registered on the project collection.
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void LogWithLoggersOnProjectCollectionCustomOneUsed()
        {
            MockLogger mockLogger = new MockLogger();
            MockLogger mockLogger2 = new MockLogger();

            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                      <Target Name=""BUild"">
                           <Message Text=""IHaveBeenLogged"" Importance=""High""/>
                       </Target>                    
                    </Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));
            ProjectCollection collection = new ProjectCollection();
            collection.RegisterLogger(mockLogger2);
            Project project = new Project(xml, null, null, collection);

            bool result = project.Build(mockLogger);
            Assert.Equal(true, result);
            mockLogger.AssertLogContains("IHaveBeenLogged");
            mockLogger2.AssertLogDoesntContain("IHaveBeenLogged");
        }

        /// <summary>
        /// Load a project from a file path
        /// </summary>
        [Fact]
        public void BasicFromFile()
        {
            string file = null;

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();

                string content = GetSampleProjectContent();
                File.WriteAllText(file, content);

                Project project = new Project(file);

                VerifyContentOfSampleProject(project);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Load a project from a file path that has valid XML that does not 
        /// evaluate successfully; then trying again after fixing the file should succeed.
        /// </summary>
        [Fact]
        public void FailedEvaluationClearsXmlCache()
        {
            string file = Path.GetTempPath() + "\\" + Guid.NewGuid().ToString("N");

            try
            {
                var xml = ProjectRootElement.Create(file);
                xml.AddItem("i", "i1").Condition = "typo in ''condition''";
                xml.Save();

                Project project = null;
                try
                {
                    project = new Project(file);
                }
                catch (InvalidProjectFileException ex)
                {
                    Console.WriteLine(ex.Message);
                }

                // Verify that we don't now have invalid project XML left in the cache
                // by writing out valid project XML and trying again;
                // Don't save through the OM or the cache would get updated; do it directly
                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents(@"<Project xmlns='msbuildnamespace'/>"));

                project = new Project(file); // should not throw
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Reading from an XMLReader that has no content should throw the correct
        /// exception
        /// </summary>
        [Fact]
        public void ReadFromEmptyReader1()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                XmlReader reader = XmlReader.Create(new StringReader(String.Empty));
                ProjectRootElement xml = ProjectRootElement.Create(reader);
            }
           );
        }
        /// <summary>
        /// Reading from an XMLReader that has no content should throw the correct
        /// exception
        /// </summary>
        [Fact]
        public void ReadFromEmptyReader2()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                XmlReader reader = XmlReader.Create(new StringReader(String.Empty));
                Project project = new Project(reader);
            }
           );
        }
        /// <summary>
        /// Reading from an XMLReader that has no content should throw the correct
        /// exception
        /// </summary>
        [Fact]
        public void ReadFromEmptyReader3()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                // Variation, we have a reader but it's already read
                XmlReader reader = XmlReader.Create(new StringReader(ProjectRootElement.Create().RawXml));

                while (reader.Read())
                {
                }

                Project project = (new ProjectCollection()).LoadProject(reader);
            }
           );
        }
        /// <summary>
        /// Reading from an XMLReader that was closed should throw the correct
        /// exception
        /// </summary>
        [Fact]
        public void ReadFromClosedReader()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                XmlReader reader = XmlReader.Create(new StringReader(String.Empty));
                reader.Close();
                Project project = new Project(reader);
            }
           );
        }
        /// <summary>
        /// Reading from an XMLReader that has TWO valid root elements should work
        /// if it's already read past the first one.
        /// </summary>
        [Fact]
        public void ReadFromReaderTwoDocs()
        {
            string emptyProject = ObjectModelHelpers.CleanupFileContents(@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace""/>");
            XmlReader reader = XmlReader.Create(new StringReader(emptyProject + emptyProject), new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment });

            reader.ReadToFollowing("Project");
            reader.Read();

            Project project2 = new Project(reader);

            Assert.Equal(false, reader.Read());
        }

        /// <summary>
        /// Import does not exist. Default case is an exception.
        /// </summary>
        [Fact]
        public void ImportDoesNotExistDefaultSettings()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddImport("__nonexistent__");

                Project project = new Project(xml);
            }
           );
        }
        /// <summary>
        /// Import gives invalid uri exception
        /// </summary>
        [Fact]
        public void ImportInvalidUriFormat()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddImport(@"//MSBuildExtensionsPath32)\4.0\Microsoft.VisualStudioVersion.v11.Common.props");

                Project project = new Project(xml);
            }
           );
        }
        /// <summary>
        /// Necessary but not sufficient for MSBuild evaluation to be thread safe.
        /// </summary>
        [Fact]
        public void ConcurrentLoadDoesNotCrash()
        {
            var tasks = new Task[500];

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Factory.StartNew(delegate () { new Project(); }); // Should not throw
            }

            Task.WaitAll(tasks);
        }

        /// <summary>
        /// Import does not exist but ProjectLoadSettings.IgnoreMissingImports was set
        /// </summary>
        [Fact]
        public void ImportDoesNotExistIgnoreMissingImports()
        {
            ProjectRootElement xml = ProjectRootElement.Create();

            xml.AddProperty("p", "1");
            xml.AddImport("__nonexistent__");
            xml.AddProperty("q", "$(p)");

            Project project = new Project(xml, null, null, new ProjectCollection(), ProjectLoadSettings.IgnoreMissingImports);

            // Make sure some evaluation did occur
            Assert.Equal("1", project.GetPropertyValue("q"));
        }

        /// <summary>
        /// When we try and access the ImportsIncludingDuplicates property on the project without setting 
        /// the correct projectloadsetting flag, we expect an invalidoperationexception.
        /// </summary>
        [Fact]
        public void TryImportsIncludingDuplicatesExpectException()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                Project project = new Project(xml, null, null, new ProjectCollection(), ProjectLoadSettings.IgnoreMissingImports);
                IList<ResolvedImport> imports = project.ImportsIncludingDuplicates;
                Assert.Equal(0, imports.Count);
            }
           );
        }
        /// <summary>
        /// Import self ignored
        /// </summary>
        [Fact]
        public void ImportSelfIgnored()
        {
            string file = null;

            try
            {
                ProjectCollection collection = new ProjectCollection();
                MockLogger logger = new MockLogger();
                collection.RegisterLogger(logger);

                Project project = new Project(collection);
                project.Xml.AddImport("$(MSBuildProjectFullPath)");

                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                project.Save(file);
                project.ReevaluateIfNecessary();

                logger.AssertLogContains("MSB4210"); // selfimport
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Import self indirectly ignored
        /// </summary>
        [Fact]
        public void ImportSelfIndirectIgnored()
        {
            string file = null;
            string file2 = null;

            try
            {
                ProjectCollection collection = new ProjectCollection();
                MockLogger logger = new MockLogger();
                collection.RegisterLogger(logger);

                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                file2 = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                Project project = new Project(collection);
                project.Xml.AddImport(file2);
                project.Save(file);

                Project project2 = new Project(collection);
                project2.Xml.AddImport(file);
                project2.Save(file2);

                project.ReevaluateIfNecessary();

                logger.AssertLogContains("MSB4210"); // selfimport
            }
            finally
            {
                File.Delete(file);
                File.Delete(file2);
            }
        }

        /// <summary>
        /// Double import ignored
        /// </summary>
        [Fact]
        public void DoubleImportIgnored()
        {
            string file = null;
            string file2 = null;

            try
            {
                ProjectCollection collection = new ProjectCollection();
                MockLogger logger = new MockLogger();
                collection.RegisterLogger(logger);

                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                file2 = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                Project project = new Project(collection);
                project.Xml.AddImport(file2);
                project.Xml.AddImport(file2);
                project.Save(file);

                Project project2 = new Project(collection);
                project2.Save(file2);

                project.ReevaluateIfNecessary();

                logger.AssertLogContains("MSB4011"); // duplicate import
            }
            finally
            {
                File.Delete(file);
                File.Delete(file2);
            }
        }

        /// <summary>
        /// Double import ignored
        /// </summary>
        [Fact]
        public void DoubleImportIndirectIgnored()
        {
            string file = null;
            string file2 = null;
            string file3 = null;

            try
            {
                ProjectCollection collection = new ProjectCollection();
                MockLogger logger = new MockLogger();
                collection.RegisterLogger(logger);

                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                file2 = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                file3 = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();

                Project project = new Project(collection);
                project.Xml.AddImport(file2);
                project.Xml.AddImport(file3);
                project.Save(file);

                Project project2 = new Project(collection);
                project.Xml.AddImport(file3);
                project2.Save(file2);

                Project project3 = new Project(collection);
                project3.Save(file3);

                project.ReevaluateIfNecessary();

                logger.AssertLogContains("MSB4011"); // duplicate import
            }
            finally
            {
                File.Delete(file);
                File.Delete(file2);
                File.Delete(file3);
            }
        }

        /// <summary>
        /// Basic created from backing XML
        /// </summary>
        [Fact]
        public void BasicFromXml()
        {
            ProjectRootElement xml = GetSampleProjectRootElement();
            Project project = new Project(xml);

            VerifyContentOfSampleProject(project);
        }

        /// <summary>
        /// Test Project from an XML with an import.
        /// Also verify the Imports collection on the evaluated Project.
        /// </summary>
        [Fact]
        public void BasicFromXmlFollowImport()
        {
            string importContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <PropertyGroup>
                            <p2>v3</p2>
                        </PropertyGroup>
                        <ItemGroup>
                            <i Include='i4'/>
                        </ItemGroup>
                        <Target Name='t2'>
                            <task/>
                        </Target>
                    </Project>");

            string importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.targets", importContent);

            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <PropertyGroup Condition=""'$(Configuration)'=='Foo'"">
                            <p>v1</p>
                        </PropertyGroup>
                        <PropertyGroup Condition=""'$(Configuration)'!='Foo'"">
                            <p>v2</p>
                        </PropertyGroup>
                        <PropertyGroup>
                            <p2>X$(p)</p2>
                        </PropertyGroup>
                        <ItemGroup>
                            <i Condition=""'$(Configuration)'=='Foo'"" Include='i0'/>
                            <i Include='i1'/>
                            <i Include='$(p)X;i3'/>
                        </ItemGroup>
                        <Target Name='t'>
                            <task/>
                        </Target>
                        <Import Project='{0}'/>
                    </Project>");

            projectFileContent = String.Format(projectFileContent, importPath);

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));
            Project project = new Project(xml);

            Assert.Equal("v3", project.GetPropertyValue("p2"));

            List<ProjectItem> items = Helpers.MakeList(project.GetItems("i"));
            Assert.Equal(4, items.Count);
            Assert.Equal("i1", items[0].EvaluatedInclude);
            Assert.Equal("v2X", items[1].EvaluatedInclude);
            Assert.Equal("i3", items[2].EvaluatedInclude);
            Assert.Equal("i4", items[3].EvaluatedInclude);

            IList<ResolvedImport> imports = project.Imports;
            Assert.Equal(1, imports.Count);
            Assert.Equal(true, Object.ReferenceEquals(imports.First().ImportingElement, xml.Imports.ElementAt(0)));

            // We can take advantage of the fact that we will get the same ProjectRootElement from the cache if we try to
            // open it with a path; get that and then compare it to what project.Imports gave us.
            Assert.Equal(true, Object.ReferenceEquals(imports.First().ImportedProject, ProjectRootElement.Open(importPath)));

            // Test the logical project iterator
            List<ProjectElement> logicalElements = new List<ProjectElement>(project.GetLogicalProject());

            Assert.Equal(18, logicalElements.Count);

            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Get items, transforms use correct directory base, ie., the project folder
        /// </summary>
        [Fact]
        public void TransformsUseCorrectDirectory_Basic()
        {
            string file = null;

            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <ItemGroup>
                            <IntermediateAssembly Include='obj\i386\foo.dll'/>
                            <BuiltProjectOutputGroupKeyOutput Include=""@(IntermediateAssembly->'%(FullPath)')""/>
                        </ItemGroup>
                    </Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));

            Project project = new Project(xml);

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                project.Save(file);
                project.ReevaluateIfNecessary();

                Assert.Equal(Path.Combine(Path.GetTempPath(), @"obj\i386\foo.dll"), project.GetItems("BuiltProjectOutputGroupKeyOutput").First().EvaluatedInclude);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Get items, transforms use correct directory base, ie., the current
        /// directory at the time of load for a project that was not yet saved
        /// </summary>
        [Fact]
        public void TransformsUseCorrectDirectory_Basic_NotSaved()
        {
            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <ItemGroup>
                            <IntermediateAssembly Include='obj\i386\foo.dll'/>
                            <BuiltProjectOutputGroupKeyOutput Include=""@(IntermediateAssembly->'%(FullPath)')""/>
                        </ItemGroup>
                    </Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));

            Project project = new Project(xml);
            ProjectInstance projectInstance = new ProjectInstance(xml);

            Assert.Equal(Path.Combine(Environment.CurrentDirectory, @"obj\i386\foo.dll"), project.GetItems("BuiltProjectOutputGroupKeyOutput").First().EvaluatedInclude);
            Assert.Equal(Path.Combine(Environment.CurrentDirectory, @"obj\i386\foo.dll"), projectInstance.GetItems("BuiltProjectOutputGroupKeyOutput").First().EvaluatedInclude);
        }

        /// <summary>
        /// Directory transform uses project directory
        /// </summary>
        [Fact]
        public void TransformsUseCorrectDirectory_DirectoryTransform()
        {
            string file = null;

            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"<Project xmlns='msbuildnamespace'>
                        <ItemGroup>
                            <IntermediateAssembly Include='obj\i386\foo.dll'/>
                            <BuiltProjectOutputGroupKeyOutput Include=""@(IntermediateAssembly->'%(Directory)')""/>
                        </ItemGroup>
                    </Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                xml.FullPath = file;

                Project project = new Project(xml);
                ProjectInstance projectInstance = new ProjectInstance(xml);

                Assert.Equal(Path.Combine(Path.GetTempPath().Substring(3) /* remove c:\ */, @"obj\i386\"), project.GetItems("BuiltProjectOutputGroupKeyOutput").First().EvaluatedInclude);
                Assert.Equal(Path.Combine(Path.GetTempPath().Substring(3) /* remove c:\ */, @"obj\i386\"), projectInstance.GetItems("BuiltProjectOutputGroupKeyOutput").First().EvaluatedInclude);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Directory item function uses project directory
        /// </summary>
        [Fact]
        public void TransformsUseCorrectDirectory_DirectoryItemFunction()
        {
            string file = null;

            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <ItemGroup>
                            <IntermediateAssembly Include='obj\i386\foo.dll'/>
                            <BuiltProjectOutputGroupKeyOutput Include=""@(IntermediateAssembly->Directory())""/>
                        </ItemGroup>
                    </Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                xml.FullPath = file;

                Project project = new Project(xml);
                ProjectInstance projectInstance = new ProjectInstance(xml);

                Assert.Equal(Path.Combine(Path.GetTempPath().Substring(3) /* remove c:\ */, @"obj\i386\"), project.GetItems("BuiltProjectOutputGroupKeyOutput").First().EvaluatedInclude);
                Assert.Equal(Path.Combine(Path.GetTempPath().Substring(3) /* remove c:\ */, @"obj\i386\"), projectInstance.GetItems("BuiltProjectOutputGroupKeyOutput").First().EvaluatedInclude);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Directory item function uses project directory
        /// </summary>
        [Fact]
        public void TransformsUseCorrectDirectory_DirectoryNameItemFunction()
        {
            string file = null;

            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace'>
                        <ItemGroup>
                            <IntermediateAssembly Include='obj\i386\foo.dll'/>
                            <BuiltProjectOutputGroupKeyOutput Include=""@(IntermediateAssembly->DirectoryName())""/>
                        </ItemGroup>
                    </Project>");

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                xml.FullPath = file;

                Project project = new Project(xml);
                ProjectInstance projectInstance = new ProjectInstance(xml);

                // Should be the full path to the directory
                Assert.Equal(Path.Combine(Path.GetTempPath() /* remove c:\ */, @"obj\i386"), project.GetItems("BuiltProjectOutputGroupKeyOutput").First().EvaluatedInclude);
                Assert.Equal(Path.Combine(Path.GetTempPath() /* remove c:\ */, @"obj\i386"), projectInstance.GetItems("BuiltProjectOutputGroupKeyOutput").First().EvaluatedInclude);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Global properties accessor
        /// </summary>
        [Fact]
        public void GetGlobalProperties()
        {
            ProjectRootElement xml = GetSampleProjectRootElement();
            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties.Add("g1", "v1");
            globalProperties.Add("g2", "v2");
            Project project = new Project(xml, globalProperties, null);

            Assert.Equal("v1", project.GlobalProperties["g1"]);
            Assert.Equal("v2", project.GlobalProperties["g2"]);
        }

        /// <summary>
        /// Global properties are cloned when passed in:
        /// subsequent changes have no effect
        /// </summary>
        [Fact]
        public void GlobalPropertiesCloned()
        {
            ProjectRootElement xml = GetSampleProjectRootElement();
            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties.Add("g1", "v1");
            Project project = new Project(xml, globalProperties, null);

            globalProperties.Add("g2", "v2");

            Assert.Equal("v1", project.GlobalProperties["g1"]);
            Assert.Equal(false, project.GlobalProperties.ContainsKey("g2"));
        }

        /// <summary>
        /// Global properties accessor when no global properties
        /// </summary>
        [Fact]
        public void GetGlobalPropertiesNone()
        {
            ProjectRootElement xml = GetSampleProjectRootElement();
            Project project = new Project(xml);

            Assert.Equal(0, project.GlobalProperties.Count);
        }

        /// <summary>
        /// Changing global properties should make the project a candidate
        /// for reevaluation.
        /// </summary>
        [Fact]
        public void ChangeGlobalProperties()
        {
            Project project = new Project();
            ProjectPropertyElement propertyElement = project.Xml.AddProperty("p", "v0");
            propertyElement.Condition = "'$(g)'=='v1'";
            project.ReevaluateIfNecessary();
            Assert.Equal(String.Empty, project.GetPropertyValue("p"));

            Assert.Equal(true, project.SetGlobalProperty("g", "v1"));
            Assert.Equal(true, project.IsDirty);
            project.ReevaluateIfNecessary();
            Assert.Equal("v0", project.GetPropertyValue("p"));
            Assert.Equal("v1", project.GlobalProperties["g"]);
        }

        /// <summary>
        /// Changing global property after reevaluation should not crash
        /// </summary>
        [Fact]
        public void ChangeGlobalPropertyAfterReevaluation()
        {
            Project project = new Project();
            project.SetGlobalProperty("p", "v1");
            project.ReevaluateIfNecessary();
            project.SetGlobalProperty("p", "v2");

            Assert.Equal("v2", project.GetPropertyValue("p"));
            Assert.Equal(true, project.GetProperty("p").IsGlobalProperty);
        }

        /// <summary>
        /// Test the SkipEvaluation functionality of ReevaluateIfNecessary
        /// </summary>
        [Fact]
        public void SkipEvaluation()
        {
            Project project = new Project();
            project.SetGlobalProperty("p", "v1");
            project.ReevaluateIfNecessary();
            Assert.Equal("v1", project.GetPropertyValue("p"));

            project.SkipEvaluation = true;
            ProjectPropertyElement propertyElement = project.Xml.AddProperty("p1", "v0");
            propertyElement.Condition = "'$(g)'=='v1'";
            project.SetGlobalProperty("g", "v1");
            project.ReevaluateIfNecessary();
            Assert.Equal(String.Empty, project.GetPropertyValue("p1"));

            project.SkipEvaluation = false;
            project.SetGlobalProperty("g", "v1");
            project.ReevaluateIfNecessary();
            Assert.Equal("v0", project.GetPropertyValue("p1"));
        }

        /// <summary>
        /// Setting property with same name as global property but after reevaluation should error
        /// because the property is global, not fail with null reference exception
        /// </summary>
        [Fact]
        public void ChangeGlobalPropertyAfterReevaluation2()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                project.SetGlobalProperty("p", "v1");
                project.ReevaluateIfNecessary();
                project.SetProperty("p", "v2");
            }
           );
        }
        /// <summary>
        /// Setting environment property should create a real property
        /// </summary>
        [Fact]
        public void ChangeEnvironmentProperty()
        {
            Project project = new Project();
            project.SetProperty("computername", "v1");

            Assert.Equal("v1", project.GetPropertyValue("computername"));
            Assert.Equal(true, project.IsDirty);

            project.ReevaluateIfNecessary();

            Assert.Equal("v1", project.GetPropertyValue("computername"));
        }

        /// <summary>
        /// Setting a reserved property through the project should error nicely
        /// </summary>
        [Fact]
        public void SetReservedPropertyThroughProject()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = new Project();
                project.SetProperty("msbuildprojectdirectory", "v1");
            }
           );
        }
        /// <summary>
        /// Changing global properties with some preexisting.
        /// </summary>
        [Fact]
        public void ChangeGlobalPropertiesPreexisting()
        {
            Dictionary<string, string> initial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            initial.Add("p0", "v0");
            initial.Add("p1", "v1");
            Project project = new Project(ProjectRootElement.Create(), initial, null);
            ProjectPropertyElement propertyElement = project.Xml.AddProperty("pp", "vv");
            propertyElement.Condition = "'$(p0)'=='v0' and '$(p1)'=='v1b'";
            project.ReevaluateIfNecessary();
            Assert.Equal(String.Empty, project.GetPropertyValue("pp"));

            project.SetGlobalProperty("p1", "v1b");
            Assert.Equal(true, project.IsDirty);
            project.ReevaluateIfNecessary();
            Assert.Equal("vv", project.GetPropertyValue("pp"));
            Assert.Equal("v0", project.GlobalProperties["p0"]);
            Assert.Equal("v1b", project.GlobalProperties["p1"]);
        }

        /// <summary>
        /// Changing global properties with some preexisting from the project collection.
        /// Should not modify those on the project collection.
        /// </summary>
        [Fact]
        public void ChangeGlobalPropertiesInitiallyFromProjectCollection()
        {
            Dictionary<string, string> initial = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            initial.Add("p0", "v0");
            initial.Add("p1", "v1");
            ProjectCollection collection = new ProjectCollection(initial, null, ToolsetDefinitionLocations.ConfigurationFile);
            Project project = new Project(collection);
            ProjectPropertyElement propertyElement = project.Xml.AddProperty("pp", "vv");
            propertyElement.Condition = "'$(p0)'=='v0' and '$(p1)'=='v1b'";
            project.ReevaluateIfNecessary();
            Assert.Equal(String.Empty, project.GetPropertyValue("pp"));

            project.SetGlobalProperty("p1", "v1b");
            Assert.Equal(true, project.IsDirty);
            project.ReevaluateIfNecessary();
            Assert.Equal("vv", project.GetPropertyValue("pp"));
            Assert.Equal("v0", collection.GlobalProperties["p0"]);
            Assert.Equal("v1", collection.GlobalProperties["p1"]);
        }

        /// <summary>
        /// Changing global property to the same value should not dirty the project.
        /// </summary>
        [Fact]
        public void ChangeGlobalPropertiesSameValue()
        {
            Project project = new Project();
            project.SetGlobalProperty("g", "v1");
            Assert.Equal(true, project.IsDirty);
            project.ReevaluateIfNecessary();

            Assert.Equal(false, project.SetGlobalProperty("g", "v1"));
            Assert.Equal(false, project.IsDirty);
        }

        /// <summary>
        /// Removing global properties should make the project a candidate
        /// for reevaluation.
        /// </summary>
        [Fact]
        public void RemoveGlobalProperties()
        {
            Project project = new Project();
            ProjectPropertyElement propertyElement = project.Xml.AddProperty("p", "v0");
            propertyElement.Condition = "'$(g)'==''";
            project.SetGlobalProperty("g", "v1");
            project.ReevaluateIfNecessary();
            Assert.Equal(String.Empty, project.GetPropertyValue("p"));

            bool existed = project.RemoveGlobalProperty("g");
            Assert.Equal(true, existed);
            Assert.Equal(true, project.IsDirty);
            project.ReevaluateIfNecessary();
            Assert.Equal("v0", project.GetPropertyValue("p"));
            Assert.Equal(false, project.GlobalProperties.ContainsKey("g"));
        }

        /// <summary>
        /// Remove nonexistent global property should return false and not dirty the project.
        /// </summary>
        [Fact]
        public void RemoveNonExistentGlobalProperties()
        {
            Project project = new Project();
            bool existed = project.RemoveGlobalProperty("x");

            Assert.Equal(false, existed);
            Assert.Equal(false, project.IsDirty);
        }

        /// <summary>
        /// ToolsVersion accessor for explicitly specified
        /// </summary>
        [Fact]
        public void GetToolsVersionExplicitlySpecified()
        {
            if (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35) == null)
            {
                // "Requires 3.5 to be installed"
                return;
            }

            ProjectRootElement xml = GetSampleProjectRootElement();
            Project project = new Project(xml, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "4.0");

            Assert.Equal("4.0", project.ToolsVersion);
        }

        /// <summary>
        /// ToolsVersion accessor when none was specified.
        /// Should not return the value on the project element.
        /// </summary>
        [Fact]
        public void GetToolsVersionNoneExplicitlySpecified()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            xml.ToolsVersion = String.Empty;
            xml.AddTarget("t");

            Project project = new Project(xml);

            Assert.Equal(String.Empty, project.Xml.ToolsVersion);

            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// ToolsVersion defaults to 4.0
        /// </summary>
        [Fact]
        public void GetToolsVersionFromProject()
        {
            Project project = new Project();

            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.ToolsVersion);
        }

        /// <summary>
        /// Project.ToolsVersion should be set to ToolsVersion evaluated with,
        /// even if it is subsequently changed on the XML (without reevaluation)
        /// </summary>
        [Fact]
        public void ProjectToolsVersion20Present()
        {
            if (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20) == null)
            {
                // "Requires 2.0 to be installed"
                return;
            }

            Project project = new Project();
            project.Xml.ToolsVersion = "2.0";
            project.ReevaluateIfNecessary();

            // ... and after all that, we end up defaulting to the current ToolsVersion instead.  There's a way 
            // to turn this behavior (new in Dev12) off, but it requires setting an environment variable and 
            // clearing some internal state to make sure that the update environment variable is picked up, so 
            // there's not a good way of doing it from these deliberately public OM only tests. 
            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.ToolsVersion);

            project.Xml.ToolsVersion = "4.0";

            // Still defaulting to the current ToolsVersion
            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.ToolsVersion);
        }

        /// <summary>
        /// Project.ToolsVersion should be set to ToolsVersion evaluated with,
        /// even if it is subsequently changed on the XML (without reevaluation)
        /// </summary>
        [Fact]
        public void ProjectToolsVersion20NotPresent()
        {
            if (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20) != null)
            {
                // "Requires 2.0 to NOT be installed"
                return;
            }

            Project project = new Project();
            project.Xml.ToolsVersion = "2.0";
            project.ReevaluateIfNecessary();

            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.ToolsVersion);

            project.Xml.ToolsVersion = ObjectModelHelpers.MSBuildDefaultToolsVersion;

            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.ToolsVersion);
        }

        /// <summary>
        /// $(MSBuildToolsVersion) should be set to ToolsVersion evaluated with,
        /// even if it is subsequently changed on the XML (without reevaluation)
        /// </summary>
        [Fact]
        public void MSBuildToolsVersionProperty()
        {
            if (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20) == null)
            {
                // "Requires 2.0 to be installed"
                return;
            }

            Project project = new Project();
            project.Xml.ToolsVersion = "2.0";
            project.ReevaluateIfNecessary();

            // ... and after all that, we end up defaulting to the current ToolsVersion instead.  There's a way 
            // to turn this behavior (new in Dev12) off, but it requires setting an environment variable and 
            // clearing some internal state to make sure that the update environment variable is picked up, so 
            // there's not a good way of doing it from these deliberately public OM only tests. 
            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.GetPropertyValue("msbuildtoolsversion"));

            project.Xml.ToolsVersion = "4.0";

            // Still current
            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.GetPropertyValue("msbuildtoolsversion"));

            project.ReevaluateIfNecessary();

            // Still current
            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.GetPropertyValue("msbuildtoolsversion"));
        }

        /// <summary>
        /// $(MSBuildToolsVersion) should be set to ToolsVersion evaluated with,
        /// even if it is subsequently changed on the XML (without reevaluation)
        /// </summary>
        [Fact]
        public void MSBuildToolsVersionProperty40()
        {
            Project project = new Project();

            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.GetPropertyValue("msbuildtoolsversion"));
        }

        /// <summary>
        /// It's okay to change ToolsVersion to some apparently bogus value -- the project can be persisted
        /// that way, and maybe later it'll correspond to some known toolset. If the effective ToolsVersion was being
        /// gotten from the attribute, that'll be affected too; and thus might be bogus.
        /// </summary>
        [Fact]
        public void ChangingToolsVersionAttributeToUnrecognizedValue()
        {
            Project project = new Project();

            project.Xml.ToolsVersion = "bogus";

            Assert.Equal("bogus", project.Xml.ToolsVersion);
        }

        /// <summary>
        /// Test Project's surfacing of the sub-toolset version
        /// </summary>
        [Fact(Skip = "Fails due to a null-to-empty-string comparison")]
        public void GetSubToolsetVersion()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                Project p = new Project(GetSampleProjectRootElement(), null, "4.0", new ProjectCollection());

                Assert.Equal("4.0", p.ToolsVersion);

                Toolset t = p.ProjectCollection.GetToolset("4.0");

                Assert.Equal(t.DefaultSubToolsetVersion, p.SubToolsetVersion);
                Assert.Equal(t.DefaultSubToolsetVersion, p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Test Project's surfacing of the sub-toolset version when it is overridden by a value in the 
        /// environment 
        /// </summary>
        [Fact]
        public void GetSubToolsetVersion_FromEnvironment()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", "ABCD");

                Project p = new Project(GetSampleProjectRootElement(), null, "4.0", new ProjectCollection());

                Assert.Equal("4.0", p.ToolsVersion);
                Assert.Equal("ABCD", p.SubToolsetVersion);
                Assert.Equal("ABCD", p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Test ProjectInstance's surfacing of the sub-toolset version when it is overridden by a global property
        /// </summary>
        [Fact]
        public void GetSubToolsetVersion_FromProjectGlobalProperties()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("VisualStudioVersion", "ABCDE");

                Project p = new Project(GetSampleProjectRootElement(), globalProperties, "4.0", new ProjectCollection());

                Assert.Equal("4.0", p.ToolsVersion);
                Assert.Equal("ABCDE", p.SubToolsetVersion);
                Assert.Equal("ABCDE", p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Verify that if a sub-toolset version is passed to the constructor, it all other heuristic methods for 
        /// getting the sub-toolset version. 
        /// </summary>
        [Fact]
        public void GetSubToolsetVersion_FromConstructor()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", "ABC");

                IDictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("VisualStudioVersion", "ABCD");

                IDictionary<string, string> projectCollectionGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                projectCollectionGlobalProperties.Add("VisualStudioVersion", "ABCDE");

                Project p = new Project(GetSampleProjectRootElement(), globalProperties, "4.0", "ABCDEF", new ProjectCollection(projectCollectionGlobalProperties), ProjectLoadSettings.Default);

                Assert.Equal("4.0", p.ToolsVersion);
                Assert.Equal("ABCDEF", p.SubToolsetVersion);
                Assert.Equal("ABCDEF", p.GetPropertyValue("VisualStudioVersion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        /// <summary>
        /// Reevaluation should update the evaluation counter.
        /// </summary>
        [Fact]
        public void ReevaluationCounter()
        {
            Project project = new Project();
            int last = project.EvaluationCounter;

            project.ReevaluateIfNecessary();
            Assert.Equal(project.EvaluationCounter, last);
            last = project.EvaluationCounter;

            project.SetProperty("p", "v");
            project.ReevaluateIfNecessary();
            Assert.NotEqual(project.EvaluationCounter, last);
        }

        /// <summary>
        /// Unload should not reset the evaluation counter.
        /// </summary>
        [Fact]
        public void ReevaluationCounterUnload()
        {
            string path = null;

            try
            {
                path = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                ProjectRootElement.Create().Save(path);

                Project project = new Project(path);
                int last = project.EvaluationCounter;

                project.ProjectCollection.UnloadAllProjects();

                project = new Project(path);
                Assert.NotEqual(project.EvaluationCounter, last);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Modifying the XML of an imported file should cause the project
        /// to be dirtied.
        /// </summary>
        [Fact]
        public void ImportedXmlModified()
        {
            string path = null;

            try
            {
                path = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                ProjectRootElement import = ProjectRootElement.Create(path);
                import.Save();

                Project project = new Project();
                int last = project.EvaluationCounter;

                project.Xml.AddImport(path);
                project.ReevaluateIfNecessary();
                Assert.NotEqual(project.EvaluationCounter, last);
                last = project.EvaluationCounter;

                project.ReevaluateIfNecessary();
                Assert.Equal(project.EvaluationCounter, last);

                import.AddProperty("p", "v");
                Assert.Equal(true, project.IsDirty);
                project.ReevaluateIfNecessary();
                Assert.NotEqual(project.EvaluationCounter, last);
                last = project.EvaluationCounter;
                Assert.Equal("v", project.GetPropertyValue("p"));

                project.ReevaluateIfNecessary();
                Assert.Equal(project.EvaluationCounter, last);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// To support certain corner cases, it is possible to explicitly mark a Project
        /// as dirty, so that reevaluate is productive.
        /// </summary>
        [Fact]
        public void ExternallyMarkDirty()
        {
            Project project = new Project();
            project.SetProperty("p", "v");
            project.ReevaluateIfNecessary();

            Assert.Equal(false, project.IsDirty);

            ProjectProperty property1 = project.GetProperty("p");

            project.MarkDirty();

            Assert.Equal(true, project.IsDirty);

            project.ReevaluateIfNecessary();

            Assert.Equal(false, project.IsDirty);

            ProjectProperty property2 = project.GetProperty("p");

            Assert.Equal(false, Object.ReferenceEquals(property1, property2)); // different object indicates reevaluation occurred
        }

        /// <summary>
        /// Basic test of getting items by their include
        /// </summary>
        [Fact]
        public void ItemsByEvaluatedInclude()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "i1");
            project.Xml.AddItem("i", "i1");
            project.Xml.AddItem("j", "j1");
            project.Xml.AddItem("j", "i1");

            project.ReevaluateIfNecessary();

            List<ProjectItem> items = Helpers.MakeList(project.GetItemsByEvaluatedInclude("i1"));

            Assert.Equal(3, items.Count);
            foreach (ProjectItem item in items)
            {
                Assert.Equal("i1", item.EvaluatedInclude);
            }
        }

        /// <summary>
        /// Basic test of getting items by their include
        /// </summary>
        [Fact]
        public void ItemsByEvaluatedInclude_EvaluatedIncludeNeedsEscaping()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "i%261");
            project.Xml.AddItem("j", "i%25261");
            project.Xml.AddItem("k", "j1");
            project.Xml.AddItem("l", "i&1");

            project.ReevaluateIfNecessary();

            List<ProjectItem> items = Helpers.MakeList(project.GetItemsByEvaluatedInclude("i&1"));

            Assert.Equal(2, items.Count);
            foreach (ProjectItem item in items)
            {
                Assert.Equal("i&1", item.EvaluatedInclude);
                Assert.True(
                    String.Equals(item.ItemType, "i", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(item.ItemType, "l", StringComparison.OrdinalIgnoreCase)
                    );
            }
        }

        /// <summary>
        /// Verify none returned when none match
        /// </summary>
        [Fact]
        public void ItemsByEvaluatedIncludeNone()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "i1");

            project.ReevaluateIfNecessary();

            List<ProjectItem> items = Helpers.MakeList(project.GetItemsByEvaluatedInclude("i2"));

            Assert.Equal(0, items.Count);
        }

        /// <summary>
        /// Tests the tracking of virtual items from the construction to instance model, with the removal of a virtual item. 
        /// </summary>
        [Fact]
        public void ItemsByEvaluatedIncludeAndExpansion()
        {
            List<string> filePaths = new List<string>();
            string testFileRoot = null;
            try
            {
                int count = 0;
                testFileRoot = Path.Combine(Path.GetTempPath(), "foodir");
                Directory.CreateDirectory(testFileRoot);
                int maxFiles = 2;
                for (int i = 0; i < maxFiles; i++)
                {
                    string fileName = String.Format("foo{0}.foo", i);
                    string filePath = Path.Combine(testFileRoot, fileName);
                    File.WriteAllText(filePath, String.Empty);
                    filePaths.Add(filePath);
                }

                ProjectRootElement projectConstruction = ProjectRootElement.Create();
                projectConstruction.AddItem("foo", Path.Combine(testFileRoot, "*.foo"));

                count = Helpers.Count(projectConstruction.Items);
                Assert.Equal(1, count); // "Construction Model"

                Project project = new Project(projectConstruction);

                count = Helpers.Count(project.GetItems("foo"));
                Assert.Equal(2, count); // "Evaluation Model, Before Removal"

                ProjectItem itemToRemove = null;

                // Get the first item from IEnumerable Collection.
                foreach (ProjectItem item in project.Items)
                {
                    itemToRemove = item;
                    break;
                }

                project.RemoveItem(itemToRemove);
                count = Helpers.Count(project.GetItems("foo"));
                Assert.Equal(1, count); // "Evaluation Model, After Removal"

                ProjectInstance projectInstance = project.CreateProjectInstance();
                count = Helpers.Count(projectInstance.Items);
                Assert.Equal(1, count); // "Instance Model"

                // Ensure XML has been updated accordingly on the Evaluation model (projectInstance doesn't back onto XML)
                Assert.False(project.Xml.RawXml.Contains(itemToRemove.Xml.Include));
                Assert.False(project.Xml.RawXml.Contains("*.foo"));
            }
            finally
            {
                foreach (string filePathToRemove in filePaths)
                {
                    File.Delete(filePathToRemove);
                }

                Directory.Delete(testFileRoot);
            }
        }

        /// <summary>
        /// Reevaluation should update items-by-evaluated-include
        /// </summary>
        [Fact]
        public void ItemsByEvaluatedIncludeReevaluation()
        {
            Project project = new Project();
            project.Xml.AddItem("i", "i1");
            project.ReevaluateIfNecessary();

            List<ProjectItem> items = Helpers.MakeList(project.GetItemsByEvaluatedInclude("i1"));
            Assert.Equal(1, items.Count);

            project.Xml.AddItem("j", "i1");
            project.ReevaluateIfNecessary();

            items = Helpers.MakeList(project.GetItemsByEvaluatedInclude("i1"));
            Assert.Equal(2, items.Count);
        }

        /// <summary>
        /// Direct adds to the project (ie, not added by evaluation) should update
        /// items-by-evaluated-include
        /// </summary>
        [Fact]
        public void ItemsByEvaluatedIncludeDirectAdd()
        {
            Project project = new Project();
            project.AddItem("i", "i1");

            List<ProjectItem> items = Helpers.MakeList(project.GetItemsByEvaluatedInclude("i1"));
            Assert.Equal(1, items.Count);
        }

        /// <summary>
        /// Direct removes from the project (ie, not removed by evaluation) should update
        /// items-by-evaluated-include
        /// </summary>
        [Fact]
        public void ItemsByEvaluatedIncludeDirectRemove()
        {
            Project project = new Project();
            ProjectItem item1 = project.AddItem("i", "i1;j1")[0];
            project.RemoveItem(item1);

            List<ProjectItem> items = Helpers.MakeList(project.GetItemsByEvaluatedInclude("i1"));
            Assert.Equal(0, items.Count);
        }

        /// <summary>
        /// Choose, When has true condition
        /// </summary>
        [Fact]
        public void ChooseWhenTrue()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <Choose>
                            <When Condition='true'>
                              <PropertyGroup>
                                <p>v1</p>
                              </PropertyGroup> 
                              <ItemGroup>
                                <i Include='i1' />
                              </ItemGroup>
                            </When>      
                        </Choose>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            Assert.Equal("v1", project.GetPropertyValue("p"));
            Assert.Equal("i1", Helpers.MakeList(project.GetItems("i"))[0].EvaluatedInclude);
        }

        /// <summary>
        /// Choose, second When has true condition
        /// </summary>
        [Fact]
        public void ChooseSecondWhenTrue()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <Choose>
                            <When Condition='false'>
                              <PropertyGroup>
                                <p>v1</p>
                              </PropertyGroup> 
                              <ItemGroup>
                                <i Include='i1' />
                              </ItemGroup>
                            </When>   
                            <When Condition='true'>
                              <PropertyGroup>
                                <p>v2</p>
                              </PropertyGroup> 
                              <ItemGroup>
                                <i Include='i2' />
                              </ItemGroup>
                            </When>    
                        </Choose>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            Assert.Equal("v2", project.GetPropertyValue("p"));
            Assert.Equal("i2", Helpers.MakeList(project.GetItems("i"))[0].EvaluatedInclude);
        }

        /// <summary>
        /// Choose, when has false condition
        /// </summary>
        [Fact]
        public void ChooseOtherwise()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <Choose>
                            <When Condition='false'>
                              <PropertyGroup>
                                <p>v1</p>
                              </PropertyGroup> 
                              <ItemGroup>
                                <i Include='i1' />
                              </ItemGroup>
                            </When>   
                            <Otherwise>
                              <PropertyGroup>
                                <p>v2</p>
                              </PropertyGroup> 
                              <ItemGroup>
                                <i Include='i2' />
                              </ItemGroup>
                            </Otherwise>    
                        </Choose>
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            Assert.Equal("v2", project.GetPropertyValue("p"));
            Assert.Equal("i2", Helpers.MakeList(project.GetItems("i"))[0].EvaluatedInclude);
        }

        /// <summary>
        /// Choose should be entered twice, once for properties and again for items.
        /// That means items should see properties defined below.
        /// </summary>
        [Fact]
        public void ChooseTwoPasses()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <Choose>
                            <When Condition='true'>
                              <ItemGroup>
                                <i Include='$(p)_$(p2)' />
                              </ItemGroup>
                              <PropertyGroup>
                                <p>@(i);v1</p>
                              </PropertyGroup> 
                            </When>      
                        </Choose>

                      <PropertyGroup>
                        <p2>v2</p2>
                      </PropertyGroup> 

                        <Choose>
                            <When Condition='false'/>
                            <Otherwise>
                              <ItemGroup>
                                <j Include='$(q)_$(q2)' />
                              </ItemGroup>
                              <PropertyGroup>
                                <q>@(j);v1</q>
                              </PropertyGroup> 
                            </Otherwise>
                        </Choose>

                      <PropertyGroup>
                        <q2>v2</q2>
                      </PropertyGroup> 
                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            Assert.Equal("@(i);v1", project.GetPropertyValue("p"));
            Assert.Equal("@(j);v1", project.GetPropertyValue("q"));
            Assert.Equal("v1_v2", project.GetItems("i").ElementAt(0).EvaluatedInclude);
            Assert.Equal(1, project.GetItems("i").Count());
            Assert.Equal("v1_v2", project.GetItems("j").ElementAt(0).EvaluatedInclude);
            Assert.Equal(1, project.GetItems("j").Count());
        }

        /// <summary>
        /// Choose conditions are only evaluated once, on the property pass
        /// </summary>
        [Fact]
        public void ChooseEvaluateConditionOnlyOnce()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <Choose>
                            <When Condition=""'$(p)' != ''"">
                              <ItemGroup>
                                <i Include='i1' />
                              </ItemGroup>
                            </When>      
                        </Choose>

                      <PropertyGroup>
                        <p>v</p>
                      </PropertyGroup> 

                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            Assert.Equal(0, project.GetItems("i").Count());
        }

        /// <summary>
        /// Choose items can see item definitions below
        /// </summary>
        [Fact]
        public void ChooseSeesItemDefinitions()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <Choose>
                            <When Condition='true'>
                              <ItemGroup>
                                <i Include='i1'>
                                  <m>%(m);m1</m>
                                </i>
                              </ItemGroup>
                            </When>      
                        </Choose>

                      <ItemDefinitionGroup>
                        <i>
                          <m>m0</m>
                        </i>
                      </ItemDefinitionGroup> 

                    </Project>
                ");

            Project project = new Project(XmlReader.Create(new StringReader(content)));

            Assert.Equal("m0;m1", project.GetItems("i").ElementAt(0).GetMetadataValue("m"));
        }

        /// <summary>
        /// When build is disabled on the project, it shouldn't run, and should give MSB4112.
        /// </summary>
        [Fact]
        public void BuildDisabled()
        {
            Project project = new Project();
            project.Xml.AddTarget("t");
            project.IsBuildEnabled = false;
            MockLogger mockLogger = new MockLogger();
            ProjectCollection.GlobalProjectCollection.RegisterLogger(mockLogger);

            bool result = project.Build();

            Assert.Equal(false, result);

            Assert.Equal(mockLogger.Errors[0].Code, "MSB4112"); //                 "Security message about disabled targets need to have code MSB4112, because code in the VS Core project system depends on this.  See DesignTimeBuildFeedback.cpp."
        }

        /// <summary>
        /// Building a nonexistent target should log an error and return false (not throw)
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void BuildNonExistentTarget()
        {
            Project project = new Project();
            MockLogger logger = new MockLogger();
            bool result = project.Build(new string[] { "nonexistent" }, new List<ILogger>() { logger });
            Assert.Equal(false, result);
            Assert.Equal(1, logger.ErrorCount);
        }

        /// <summary>
        /// When Project.Build is invoked with custom loggers, those loggers should contain the result of any evaluation warnings and errors.
        /// </summary>
        [Fact]
        [Trait("Category", "serialize")]
        public void BuildEvaluationUsesCustomLoggers()
        {
            string importProjectContent =
                ObjectModelHelpers.CleanupFileContents(@"<Project xmlns='msbuildnamespace'>
                </Project>");

            string importFileName = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile() + ".proj";
            File.WriteAllText(importFileName, importProjectContent);

            string projectContent =
                ObjectModelHelpers.CleanupFileContents(@"<Project xmlns='msbuildnamespace'>
                    <Import Project=""" + importFileName + @"""/>
                    <Import Project=""" + importFileName + @"""/>
                    <ItemGroup>
                        <Compile Include='a.cs' />
                    </ItemGroup>
                    <Target Name=""Build"" />
                </Project>");

            Project project = new Project(XmlReader.Create(new StringReader(projectContent)));
            project.MarkDirty();

            MockLogger collectionLogger = new MockLogger();
            project.ProjectCollection.RegisterLogger(collectionLogger);

            MockLogger mockLogger = new MockLogger();

            bool result;

            try
            {
                result = project.Build(new ILogger[] { mockLogger });
            }
            catch
            {
                throw;
            }
            finally
            {
                project.ProjectCollection.UnregisterAllLoggers();
            }

            Assert.Equal(true, result);

            Assert.Equal(0, mockLogger.WarningCount); //                 "Log should not contain MSB4011 because the build logger will not receive evaluation messages."

            Assert.Equal(collectionLogger.Warnings[0].Code, "MSB4011"); //                 "Log should contain MSB4011 because the project collection logger should have been used for evaluation."
        }

        /// <summary>
        /// UsingTask expansion should throw InvalidProjectFileException
        /// if it expands to nothing.
        /// </summary>
        [Fact]
        public void UsingTaskExpansion1()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddUsingTask("x", "@(x->'%(x)')", null);
                Project project = new Project(xml);
            }
           );
        }
        /// <summary>
        /// UsingTask expansion should throw InvalidProjectFileException
        /// if it expands to nothing.
        /// </summary>
        [Fact]
        public void UsingTaskExpansion2()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddUsingTask("@(x->'%(x)')", "y", null);
                Project project = new Project(xml);
            }
           );
        }
        /// <summary>
        /// UsingTask expansion should throw InvalidProjectFileException
        /// if it expands to nothing.
        /// </summary>
        [Fact]
        public void UsingTaskExpansion3()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.AddUsingTask("x", null, "@(x->'%(x)')");
                Project project = new Project(xml);
            }
           );
        }
        /// <summary>
        /// Saving project should make it "clean" for saving
        /// but "dirty" for reevaluation if it was to a new location
        /// </summary>
        [Fact]
        public void SavingProjectClearsDirtyBit()
        {
            string contents = ObjectModelHelpers.CleanupFileContents(@"<Project xmlns='msbuildnamespace'/>");
            Project project = new Project(XmlReader.Create(new StringReader(contents)));

            Assert.True(project.Xml.HasUnsavedChanges); // Not dirty for saving
            Assert.False(project.IsDirty); // "1" // was evaluated on load

            string file = null;
            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                project.Save(file);
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }
            }

            Assert.False(project.Xml.HasUnsavedChanges); // Not dirty for saving
            Assert.True(project.IsDirty); // "2" // Dirty for reevaluation, because the project now has gotten a new file name
        }

        /// <summary>
        /// Remove an already removed item
        /// </summary>
        [Fact]
        public void RemoveItemTwiceEvaluationProject()
        {
            string projectOriginalContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <Compile Include='a.cs' />
                    </ItemGroup>
                </Project>
                ");
            Project project = new Project(XmlReader.Create(new StringReader(projectOriginalContents)));
            ProjectItem itemToRemove = Helpers.GetFirst(project.GetItems("Compile"));
            project.RemoveItem(itemToRemove);
            project.RemoveItem(itemToRemove); // should not throw

            Assert.Equal(0, Helpers.MakeList(project.Items).Count);
        }

        /// <summary>
        /// Remove an updated item
        /// </summary>
        [Fact]
        public void RemoveItemOutdatedByUpdate()
        {
            string projectOriginalContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <Compile Include='a.cs' />
                    </ItemGroup>
                </Project>
                ");
            Project project = new Project(XmlReader.Create(new StringReader(projectOriginalContents)));
            ProjectItem itemToRemove = Helpers.GetFirst(project.GetItems("Compile"));
            itemToRemove.UnevaluatedInclude = "b.cs";
            project.RemoveItem(itemToRemove); // should not throw

            Assert.Equal(0, Helpers.MakeList(project.Items).Count);
        }

        /// <summary>
        /// Remove several items
        /// </summary>
        [Fact]
        public void RemoveSeveralItems()
        {
            string projectOriginalContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <i Include='i1' />
                        <i Include='i2' />
                    </ItemGroup>
                </Project>
                ");
            Project project = new Project(XmlReader.Create(new StringReader(projectOriginalContents)));

            project.RemoveItems(project.GetItems("i"));

            Assert.Equal(0, project.Items.Count());
        }

        /// <summary>
        /// Remove several items
        /// </summary>
        [Fact]
        public void RemoveSeveralItemsOfVariousTypes()
        {
            string projectOriginalContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <i Include='i1' />
                        <j Include='j1' />
                        <j Include='j2' />
                        <k Include='k1' />
                    </ItemGroup>
                </Project>
                ");
            Project project = new Project(XmlReader.Create(new StringReader(projectOriginalContents)));

            List<ProjectItem> list = new List<ProjectItem>() { project.GetItems("i").FirstOrDefault(), project.GetItems("j").FirstOrDefault() };

            project.RemoveItems(list);

            Assert.Equal(2, project.Items.Count());
        }

        /// <summary>
        /// Remove items expanding itemlist expression
        /// </summary>
        [Fact]
        public void RemoveSeveralItemsExpandExpression()
        {
            string projectOriginalContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <i Include='i1;i2' />
                        <j Include='@(i);j2' />
                    </ItemGroup>
                </Project>
                ");
            Project project = new Project(XmlReader.Create(new StringReader(projectOriginalContents)));

            project.RemoveItems(project.GetItems("j").Take(2));
            Assert.Equal(3, project.Items.Count());

            StringWriter writer = new StringWriter();
            project.Save(writer);

            string projectExpectedContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <i Include='i1;i2' />
                        <j Include='j2' />
                    </ItemGroup>
                </Project>
                ");

            Helpers.CompareProjectXml(projectExpectedContents, writer.ToString());
        }

        /// <summary>
        /// Remove several items where removing the first one
        /// causes the second one to be detached
        /// </summary>
        [Fact]
        public void RemoveSeveralItemsFirstZombiesSecond()
        {
            string projectOriginalContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <i Include='i1;i2' />
                    </ItemGroup>
                </Project>
                ");
            Project project = new Project(XmlReader.Create(new StringReader(projectOriginalContents)));

            project.RemoveItems(project.GetItems("i"));

            Assert.Equal(0, project.Items.Count());
        }

        /// <summary>
        /// Should not get null reference
        /// </summary>
        [Fact]
        public void RemoveItemsOneNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Project project = new Project();
                project.RemoveItems(new List<ProjectItem>() { null });
            }
           );
        }
        /// <summary>
        /// Remove several items where removing the first one
        /// causes the second one to be detached
        /// </summary>
        [Fact]
        public void RemoveItemWrongProject()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectRootElement root1 = ProjectRootElement.Create();
                root1.AddItem("i", "i1");
                ProjectRootElement root2 = ProjectRootElement.Create();
                root2.AddItem("i", "i1");
                Project project1 = new Project(root1);
                Project project2 = new Project(root2);

                project1.RemoveItems(project2.Items);
            }
           );
        }
        /// <summary>
        /// Remove an item that is no longer attached. For convenience,
        /// we just skip it.
        /// </summary>
        [Fact]
        public void RemoveZombiedItem()
        {
            string projectOriginalContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                    <ItemGroup>
                        <i Include='i1' />
                    </ItemGroup>
                </Project>
                ");
            Project project = new Project(XmlReader.Create(new StringReader(projectOriginalContents)));
            ProjectItem item = project.GetItems("i").FirstOrDefault();

            project.RemoveItems(new List<ProjectItem>() { item });
            project.RemoveItems(new List<ProjectItem>() { item });

            Assert.Equal(0, project.Items.Count());
        }

        /// <summary> 
        /// Reserved property in project constructor should just throw
        /// </summary>
        [Fact]
        public void ReservedPropertyProjectConstructor()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("msbuildprojectdirectory", "x");

                Project project = new Project(globalProperties, null, new ProjectCollection());
            }
           );
        }
        /// <summary> 
        /// Reserved property in project collection global properties should log an error then rethrow
        /// </summary>
        [Fact]
        public void ReservedPropertyProjectCollectionConstructor()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("msbuildprojectdirectory", "x");
                MockLogger logger = new MockLogger();
                List<ILogger> loggers = new List<ILogger>();
                loggers.Add(logger);

                try
                {
                    ProjectCollection collection = new ProjectCollection(globalProperties, loggers, ToolsetDefinitionLocations.None);
                }
                finally
                {
                    logger.AssertLogContains("MSB4177");
                }
            }
           );
        }
        /// <summary> 
        /// Invalid property (reserved name) in project collection global properties should log an error then rethrow
        /// </summary>
        [Fact]
        public void ReservedPropertyProjectCollectionConstructor2()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("Target", "x");
                MockLogger logger = new MockLogger();
                List<ILogger> loggers = new List<ILogger>();
                loggers.Add(logger);

                try
                {
                    ProjectCollection collection = new ProjectCollection(globalProperties, loggers, ToolsetDefinitionLocations.None);
                }
                finally
                {
                    logger.AssertLogContains("MSB4177");
                }
            }
           );
        }
        /// <summary>
        /// Create tree like this
        /// 
        /// \b.targets
        /// \sub\a.proj
        /// 
        /// An item specified with "..\*" in b.targets should find b.targets
        /// as it was evaluated relative to the project file itself.
        /// </summary>
        [Fact]
        public void RelativePathsInItemsInTargetsFilesAreRelativeToProjectFile()
        {
            string directory = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                string subdirectory = Path.Combine(directory, "sub");
                Directory.CreateDirectory(subdirectory);

                string projectPath = Path.Combine(subdirectory, "a.proj");
                string targetsPath = Path.Combine(directory, "b.targets");

                ProjectRootElement targetsXml = ProjectRootElement.Create(targetsPath);
                targetsXml.AddItem("i", @"..\*");
                targetsXml.Save();

                ProjectRootElement projectXml = ProjectRootElement.Create(projectPath);
                projectXml.AddImport(@"..\b.targets");
                projectXml.Save();

                Project project = new Project(projectPath);

                IEnumerable<ProjectItem> items = project.GetItems("i");
                Assert.Equal(@"..\*", Helpers.GetFirst(items).UnevaluatedInclude);
                Assert.Equal(@"..\b.targets", Helpers.GetFirst(items).EvaluatedInclude);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary> 
        /// Invalid property (space) in project collection global properties should log an error then rethrow
        /// </summary>
        [Fact]
        public void ReservedPropertyProjectCollectionConstructor3()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties.Add("Target", "x");
                MockLogger logger = new MockLogger();
                List<ILogger> loggers = new List<ILogger>();
                loggers.Add(logger);

                try
                {
                    ProjectCollection collection = new ProjectCollection(globalProperties, loggers, ToolsetDefinitionLocations.None);
                }
                finally
                {
                    logger.AssertLogContains("MSB4177");
                }
            }
           );
        }
        /// <summary>
        /// Create a structure of various imports and verify that project.GetLogicalProject()
        /// walks through them correctly.
        /// </summary>
        [Fact]
        public void VariousImports()
        {
            ProjectRootElement one = ProjectRootElement.Create("c:\\1.targets");
            one.AddProperty("p", "1");
            ProjectRootElement two = ProjectRootElement.Create("c:\\2.targets");
            two.AddProperty("p", "2");

            ProjectRootElement zero = ProjectRootElement.Create("c:\\foo\\0.targets");
            zero.AddProperty("p", "0");
            zero.AddImport(one.FullPath);
            zero.AddImport(two.FullPath);
            zero.AddImport(two.FullPath); // Duplicated import: only the first one should be entered
            zero.AddImport(zero.FullPath); // Ignored self import

            ProjectRootElement three = ProjectRootElement.Create("c:\\3.targets");
            three.AddProperty("p", "3");
            one.AddImport(three.FullPath);

            ProjectRootElement four = ProjectRootElement.Create("c:\\4.targets");
            four.AddProperty("p", "4");
            one.AddImport(four.FullPath).Condition = "false"; // False condition; should not be entered

            Project project = new Project(zero);

            List<ProjectElement> logicalProject = new List<ProjectElement>(project.GetLogicalProject());

            Assert.Equal(8, logicalProject.Count); // 4 properties + 4 property groups
            Assert.Equal(true, Object.ReferenceEquals(zero, logicalProject[0].ContainingProject));
            Assert.Equal(true, Object.ReferenceEquals(zero, logicalProject[1].ContainingProject));
            Assert.Equal(true, Object.ReferenceEquals(one, logicalProject[2].ContainingProject));
            Assert.Equal(true, Object.ReferenceEquals(one, logicalProject[3].ContainingProject));
            Assert.Equal(true, Object.ReferenceEquals(three, logicalProject[4].ContainingProject));
            Assert.Equal(true, Object.ReferenceEquals(three, logicalProject[5].ContainingProject));
            Assert.Equal(true, Object.ReferenceEquals(two, logicalProject[6].ContainingProject));
            Assert.Equal(true, Object.ReferenceEquals(two, logicalProject[7].ContainingProject));

            // Clear the cache
            project.ProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Create a structure containing a import statement such that the import statement results in more than one
        /// file being imported. Then, verify that project.GetLogicalProject() walks through them correctly.
        /// </summary>
        [Fact]
        public void LogicalProjectWithWildcardImport()
        {
            string myTempDir = Path.Combine(Path.GetTempPath() + "MyTempDir");

            try
            {
                // Create a new directory in the system temp folder.
                Directory.CreateDirectory(myTempDir);

                ProjectRootElement one = ProjectRootElement.Create(Path.Combine(myTempDir, "1.targets"));
                one.Save();
                one.AddProperty("p", "1");

                ProjectRootElement two = ProjectRootElement.Create(Path.Combine(myTempDir, "2.targets"));
                two.Save();
                two.AddProperty("p", "2");

                ProjectRootElement zero = ProjectRootElement.Create(Path.Combine(myTempDir, "0.targets"));
                zero.AddProperty("p", "0");

                // Add a single import statement that would import both one and two.
                zero.AddImport(Path.Combine(myTempDir, "*.targets"));

                Project project = new Project(zero);

                List<ProjectElement> logicalProject = new List<ProjectElement>(project.GetLogicalProject());

                Assert.Equal(6, logicalProject.Count); // 3 properties + 3 property groups
                Assert.Equal(true, Object.ReferenceEquals(zero, logicalProject[0].ContainingProject)); // PropertyGroup
                Assert.Equal(true, Object.ReferenceEquals(zero, logicalProject[1].ContainingProject)); // p = 0
                Assert.Equal(true, Object.ReferenceEquals(one, logicalProject[2].ContainingProject));  // PropertyGroup
                Assert.Equal(true, Object.ReferenceEquals(one, logicalProject[3].ContainingProject));  // p = 1
                Assert.Equal(true, Object.ReferenceEquals(two, logicalProject[4].ContainingProject));  // PropertyGroup
                Assert.Equal(true, Object.ReferenceEquals(two, logicalProject[5].ContainingProject));  // p = 2

                // Clear the cache
                project.ProjectCollection.UnloadAllProjects();
            }
            finally
            {
                // Delete the temp directory that was created above.
                if (Directory.Exists(myTempDir))
                {
                    Directory.Delete(myTempDir, true);
                }
            }
        }

        /// <summary>
        /// Import of string that evaluates to empty should give InvalidProjectFileException
        /// </summary>
        [Fact]
        public void ImportPropertyEvaluatingToEmpty()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string projectOriginalContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                  <Import Project='$(not_defined)'/>
                </Project>
                ");
                Project project = new Project(XmlReader.Create(new StringReader(projectOriginalContents)));
            }
           );
        }
        /// <summary>
        /// Import of string that evaluates to invalid path should cause InvalidProjectFileException
        /// </summary>
        [Fact]
        public void ImportPropertyEvaluatingToInvalidPath()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string projectOriginalContents = ObjectModelHelpers.CleanupFileContents(@"
                <Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                  <PropertyGroup>
                    <p>|</p>
                  </PropertyGroup>
                  <Import Project='$(p)'/>
                </Project>
                ");
                Project project = new Project(XmlReader.Create(new StringReader(projectOriginalContents)));
            }
           );
        }
        /// <summary>
        /// Creates a simple ProjectRootElement object.
        /// (When ProjectRootElement supports editing, we need not load from a string here.)
        /// </summary>
        private ProjectRootElement GetSampleProjectRootElement()
        {
            string projectFileContent = GetSampleProjectContent();

            ProjectRootElement xml = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectFileContent)));

            return xml;
        }

        /// <summary>
        /// Creates a simple project content.
        /// </summary>
        private string GetSampleProjectContent()
        {
            string projectFileContent = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='2.0' InitialTargets='it' DefaultTargets='dt'>
                        <PropertyGroup Condition=""'$(Configuration)'=='Foo'"">
                            <p>v1</p>
                        </PropertyGroup>
                        <PropertyGroup Condition=""'$(Configuration)'!='Foo'"">
                            <p>v2</p>
                        </PropertyGroup>
                        <PropertyGroup>
                            <p2>X$(p)</p2>
                        </PropertyGroup>
                        <ItemGroup>
                            <i Condition=""'$(Configuration)'=='Foo'"" Include='i0'/>
                            <i Include='i1'/>
                            <i Include='$(p)X;i3'/>
                        </ItemGroup>
                        <Target Name='t'>
                            <task/>
                        </Target>
                    </Project>
                ");

            return projectFileContent;
        }

        /// <summary>
        /// Check the items and properties from the sample project
        /// </summary>
        private void VerifyContentOfSampleProject(Project project)
        {
            Assert.Equal("v2", project.GetProperty("p").UnevaluatedValue);
            Assert.Equal("Xv2", project.GetProperty("p2").EvaluatedValue);
            Assert.Equal("X$(p)", project.GetProperty("p2").UnevaluatedValue);

            IList<ProjectItem> items = Helpers.MakeList(project.GetItems("i"));
            Assert.Equal(3, items.Count);
            Assert.Equal("i1", items[0].EvaluatedInclude);
            Assert.Equal("v2X", items[1].EvaluatedInclude);
            Assert.Equal("$(p)X;i3", items[1].UnevaluatedInclude);
            Assert.Equal("i3", items[2].EvaluatedInclude);
        }
    }
}
