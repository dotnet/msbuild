// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;
using Xunit.Abstractions;
using Shouldly;
using System.Reflection;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectCollection
    /// </summary>
    public class ProjectCollection_Tests : IDisposable
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public ITestOutputHelper TestOutput { get; set; }

        public ProjectCollection_Tests(ITestOutputHelper outputHelper)
        {
            TestOutput = outputHelper;
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

            Assert.Empty(ProjectCollection.GlobalProjectCollection.GlobalProperties);
        }

        /// <summary>
        /// Add a single project from disk and verify it's put in the global project collection
        /// </summary>
        [Fact]
        public void AddProjectFromDisk()
        {
            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile();
                ProjectRootElement xml = ProjectRootElement.Create(path);
                xml.Save();

                Project project = new Project(path);

                Project project2 = ProjectCollection.GlobalProjectCollection.LoadProject(path);
                Assert.True(ReferenceEquals(project, project2));
            }
            finally
            {
                if (path != null)
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// When an unnamed project is saved, it gets a name, and should be entered into 
        /// the appropriate project collection.
        /// </summary>
        [Fact]
        public void AddProjectOnSave()
        {
            string path = null;

            try
            {
                Project project = new Project();
                Assert.Equal(0, ProjectCollection.GlobalProjectCollection.Count);

                path = FileUtilities.GetTemporaryFile();
                project.Save(path);

                Project project2 = ProjectCollection.GlobalProjectCollection.LoadProject(path);
                Assert.True(ReferenceEquals(project, project2));
            }
            finally
            {
                if (path != null)
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// When an unnamed project is saved, it gets a name, and should be entered into 
        /// the appropriate project collection.
        /// </summary>
        [Fact]
        public void AddProjectOnSave_SpecifiedProjectCollection()
        {
            string path = null;

            try
            {
                ProjectCollection collection = new ProjectCollection();
                Project project = new Project(collection);

                path = FileUtilities.GetTemporaryFile();
                project.Save(path);

                Project project2 = collection.LoadProject(path);
                Assert.True(ReferenceEquals(project, project2));
            }
            finally
            {
                if (path != null) File.Delete(path);
            }
        }

        /// <summary>
        /// When an unnamed project is given a name, it should be entered into its
        /// project collection.
        /// </summary>
        [Fact]
        public void AddProjectOnSetName()
        {
            var project = new Project { FullPath = "c:\\x" };
            Project project2 = ProjectCollection.GlobalProjectCollection.LoadProject("c:\\x");
            Assert.True(ReferenceEquals(project, project2));
        }

        /// <summary>
        /// Loading a project from a file inherits the project collection's global properties
        /// </summary>
        [Fact]
        public void GlobalPropertyInheritLoadFromFile()
        {
            string path = null;

            try
            {
                path = CreateProjectFile();

                var collection = new ProjectCollection();
                collection.SetGlobalProperty("p", "v");
                Project project = collection.LoadProject(path);

                Assert.Equal("v", project.GlobalProperties["p"]);
            }
            finally
            {
                if (path != null)
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Loading a project from a file inherits the project collection's global properties
        /// </summary>
#if FEATURE_INSTALLED_MSBUILD
        [Fact]
#else
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/276")]
#endif
        public void GlobalPropertyInheritLoadFromFile2()
        {
            string path = null;

            try
            {
                path = CreateProjectFile();

                ProjectCollection collection = new ProjectCollection();
                collection.SetGlobalProperty("p", "v");
                Project project = collection.LoadProject(path, "4.0");

                Assert.Equal("v", project.GlobalProperties["p"]);
            }
            finally
            {
                if (path != null)
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Loading a project from a file inherits the project collection's global properties
        /// </summary>
#if FEATURE_INSTALLED_MSBUILD
        [Fact]
#else
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/276")]
#endif
        public void GlobalPropertyInheritLoadFromFile3()
        {
            string path = null;

            try
            {
                path = CreateProjectFile();

                ProjectCollection collection = new ProjectCollection();
                collection.SetGlobalProperty("p", "v");
                Project project = collection.LoadProject(path, null, "4.0");

                Assert.Equal("v", project.GlobalProperties["p"]);
            }
            finally
            {
                if (path != null)
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Loading a project from a reader inherits the project collection's global properties
        /// </summary>
        [Fact]
        public void GlobalPropertyInheritLoadFromXml1()
        {
            XmlReader reader = CreateProjectXmlReader();

            var collection = new ProjectCollection();
            collection.SetGlobalProperty("p", "v");

            Project project = collection.LoadProject(reader);

            Assert.Equal("v", project.GlobalProperties["p"]);
        }

        /// <summary>
        /// Loading a project from a reader inherits the project collection's global properties
        /// </summary>
        [Fact]
        public void GlobalPropertyInheritLoadFromXml2()
        {
            XmlReader reader = CreateProjectXmlReader();

            var collection = new ProjectCollection();
            collection.SetGlobalProperty("p", "v");

            Project project = collection.LoadProject(reader, ObjectModelHelpers.MSBuildDefaultToolsVersion);

            Assert.Equal("v", project.GlobalProperties["p"]);
        }

        /// <summary>
        /// Creating a project inherits the project collection's global properties
        /// </summary>
        [Fact]
        public void GlobalPropertyInheritProjectConstructor()
        {
            var collection = new ProjectCollection();
            collection.SetGlobalProperty("p", "v");

            var project = new Project(collection);

            Assert.Equal("v", project.GlobalProperties["p"]);
        }

        /// <summary>
        /// Load project should load a project, if it wasn't already loaded.
        /// </summary>
        [Fact]
        public void GetLoadedProjectNonExistent()
        {
            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile();
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.Save(path);
                Assert.Equal(0, ProjectCollection.GlobalProjectCollection.Count);

                ProjectCollection.GlobalProjectCollection.LoadProject(path);

                Assert.Equal(1, ProjectCollection.GlobalProjectCollection.Count);
            }
            finally
            {
                if (path != null)
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Verify that one project collection doesn't contain the projects of another
        /// </summary>
        [Fact]
        public void GetLoadedProjectWrongCollection()
        {
            var project1 = new Project { FullPath = "c:\\1" };

            var collection = new ProjectCollection();
            var project2 = new Project(collection) { FullPath = "c:\\1" };

            Assert.True(ReferenceEquals(project2, collection.LoadProject("c:\\1")));
            Assert.False(ReferenceEquals(project1, collection.LoadProject("c:\\1")));
        }

        /// <summary>
        /// Verify that one project collection doesn't contain the ProjectRootElements of another
        /// -- because they don't share a ProjectRootElementCache
        /// </summary>
        [Fact]
        public void GetLoadedProjectRootElementWrongCollection()
        {
            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile();
                ProjectRootElement.Create(path).Save();

                ProjectCollection collection1 = new ProjectCollection();
                Project project1 = collection1.LoadProject(path);
                Project project1b = collection1.LoadProject(path);

                Assert.True(ReferenceEquals(project1.Xml, project1b.Xml));

                ProjectCollection collection2 = new ProjectCollection();
                Project project2 = collection2.LoadProject(path);

                Assert.False(ReferenceEquals(project1.Xml, project2.Xml));
            }
            finally
            {
                if (path != null)
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Attempt to have two equivalent projects in a project collection fails.
        /// </summary>
        [Fact]
        public void ErrorTwoProjectsEquivalentOneCollection()
        {
            _ = new Project { FullPath = "c:\\x" };
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = new Project { FullPath = "c:\\x" };
            });
        }

        /// <summary>
        /// Validates that when loading two projects with nominally different global properties, but that match when we take 
        /// into account the ProjectCollection's global properties, we get the pre-existing project if one exists. 
        /// </summary>
        [Fact]
        public void TwoProjectsEquivalentWhenOneInheritsFromProjectCollection()
        {
            var project = new Project { FullPath = "c:\\1" };

            // Set a global property on the project collection -- this should be passed on to all 
            // loaded projects. 
            ProjectCollection.GlobalProjectCollection.SetGlobalProperty("Configuration", "Debug");

            Assert.Equal("Debug", project.GlobalProperties["Configuration"]);

            // now create a global properties dictionary to pass to a new project 
            var globals = new Dictionary<string, string> { { "Configuration", "Debug" } };

            ProjectCollection.GlobalProjectCollection.LoadProject("c:\\1", globals, null);

            Assert.Equal(1, ProjectCollection.GlobalProjectCollection.Count);
        }

        /// <summary>
        /// Two projects may have the same path but different global properties.
        /// </summary>
        [Fact]
        public void TwoProjectsDistinguishedByGlobalPropertiesOnly()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            string projectDirectory = NativeMethodsShared.IsWindows ? "c:\\1" : "/l";

            var globalProperties1 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "p", "v1" } };
            var project1 =
                new Project(xml, globalProperties1, ObjectModelHelpers.MSBuildDefaultToolsVersion)
                {
                    FullPath = projectDirectory
                };

            var globalProperties2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "p", "v2" } };
            var project2 =
                new Project(xml, globalProperties2, ObjectModelHelpers.MSBuildDefaultToolsVersion)
                {
                    FullPath = projectDirectory
                };

            Assert.True(ReferenceEquals(project1, ProjectCollection.GlobalProjectCollection.LoadProject(projectDirectory, globalProperties1, ObjectModelHelpers.MSBuildDefaultToolsVersion)));
            Assert.True(ReferenceEquals(project2, ProjectCollection.GlobalProjectCollection.LoadProject(projectDirectory, globalProperties2, ObjectModelHelpers.MSBuildDefaultToolsVersion)));

            List<Project> projects = Helpers.MakeList(ProjectCollection.GlobalProjectCollection.LoadedProjects);

            Assert.Equal(2, projects.Count);
            Assert.Equal(2, ProjectCollection.GlobalProjectCollection.Count);
            Assert.Contains(project1, projects);
            Assert.Contains(project2, projects);
        }

        /// <summary>
        /// Validates that we can correctly load two of the same project file with different global properties, even when
        /// those global properties are applied to the project by the project collection (and then overridden in one case). 
        /// </summary>
        [Fact]
        public void TwoProjectsDistinguishedByGlobalPropertiesOnly_ProjectOverridesProjectCollection()
        {
            var project = new Project { FullPath = "c:\\1" };

            // Set a global property on the project collection -- this should be passed on to all 
            // loaded projects. 
            ProjectCollection.GlobalProjectCollection.SetGlobalProperty("Configuration", "Debug");

            Assert.Equal("Debug", project.GlobalProperties["Configuration"]);

            // Differentiate this project from the one below
            project.SetGlobalProperty("MyProperty", "MyValue");

            // now create a global properties dictionary to pass to a new project 
            var project2Globals =
                new Dictionary<string, string> { { "Configuration", "Release" }, { "Platform", "Win32" } };

            Project project2 = ProjectCollection.GlobalProjectCollection.LoadProject("c:\\1", project2Globals, null);

            Assert.Equal("Release", project2.GlobalProperties["Configuration"]);

            // Setting a global property on the project collection overrides all contained projects, 
            // whether they were initially loaded with the global project collection's value or not. 
            ProjectCollection.GlobalProjectCollection.SetGlobalProperty("Platform", "X64");
            Assert.Equal("X64", project.GlobalProperties["Platform"]);
            Assert.Equal("X64", project2.GlobalProperties["Platform"]);

            // But setting a global property on the project directly should override that.
            project2.SetGlobalProperty("Platform", "Itanium");
            Assert.Equal("Itanium", project2.GlobalProperties["Platform"]);

            // Now set global properties such that the two projects have an identical set.  
            ProjectCollection.GlobalProjectCollection.SetGlobalProperty("Configuration", "Debug2");
            ProjectCollection.GlobalProjectCollection.SetGlobalProperty("Platform", "X86");

            bool exceptionCaught = false;
            try
            {
                // This will make it identical, so we should get a throw here. 
                ProjectCollection.GlobalProjectCollection.SetGlobalProperty("MyProperty", "MyValue2");
            }
            catch (InvalidOperationException)
            {
                exceptionCaught = true;
            }

            Assert.True(exceptionCaught); // "Should have caused the two projects to be identical, causing an exception to be thrown"
        }

        /// <summary>
        /// Two projects may have the same path but different tools version.
        /// </summary>
        [Fact]
        public void TwoProjectsDistinguishedByToolsVersionOnly()
        {
            if (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35) == null)
            {
                // "Requires 3.5 to be installed"
                return;
            }

            if (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20) == null)
            {
                // ".NET Framework 2.0 is required to be installed for this test, but it is not installed."
                return;
            }

            ProjectRootElement xml = ProjectRootElement.Create();

            var project1 = new Project(xml, null, "2.0") { FullPath = "c:\\1" };

            var project2 = new Project(xml, null, ObjectModelHelpers.MSBuildDefaultToolsVersion) { FullPath = "c:\\1" };
            Assert.True(ReferenceEquals(project1, ProjectCollection.GlobalProjectCollection.LoadProject("c:\\1", null, "2.0")));
            Assert.True(ReferenceEquals(project2, ProjectCollection.GlobalProjectCollection.LoadProject("c:\\1", null, ObjectModelHelpers.MSBuildDefaultToolsVersion)));
        }

        /// <summary>
        /// If the ToolsVersion in the project file is bogus, we'll default to the current ToolsVersion and successfully 
        /// load it.  Make sure we can RE-load it, too, and successfully pick up the correct copy of the loaded project. 
        /// </summary>
        [Fact]
        public void ReloadProjectWithInvalidToolsVersionInFile()
        {
            const string content = @"
                    <Project ToolsVersion='bogus' xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'/>
                    </Project>
                ";

            var project = new Project(XmlReader.Create(new StringReader(content))) { FullPath = "c:\\123.proj" };

            Project project2 = ProjectCollection.GlobalProjectCollection.LoadProject("c:\\123.proj", null, null);

            Assert.True(ReferenceEquals(project, project2));
        }

        /// <summary>
        /// Make sure we can reload a project that has a ToolsVersion that doesn't match what it ends up getting 
        /// forced to by default (current). 
        /// </summary>
        [Fact]
        public void ReloadProjectWithProjectToolsVersionDifferentFromEffectiveToolsVersion()
        {
            const string content = @"
                    <Project ToolsVersion='4.0' xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'/>
                    </Project>
                ";

            var project = new Project(XmlReader.Create(new StringReader(content))) { FullPath = "c:\\123.proj" };

            Project project2 = ProjectCollection.GlobalProjectCollection.LoadProject("c:\\123.proj", null, null);

            Assert.True(ReferenceEquals(project, project2));
        }

        /// <summary>
        /// Collection stores projects distinguished by path, global properties, and tools version.
        /// Changing global properties should update the collection.
        /// </summary>
        [Fact]
        public void ChangingGlobalPropertiesUpdatesCollection()
        {
            ProjectCollection collection = new ProjectCollection();
            var project = new Project(collection) { FullPath = "c:\\x" };
            project.SetGlobalProperty("p", "v1"); // should update collection

            var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "p", "v1" } };
            Project newProject = collection.LoadProject("c:\\x", globalProperties, null);

            Assert.True(ReferenceEquals(project, newProject));
        }

        /// <summary>
        /// Changing global properties on collection should update the collection's defaults,
        /// and any projects even if they have defined the same global properties
        /// </summary>
        [Fact]
        public void SettingGlobalPropertiesOnCollectionUpdatesProjects()
        {
            var collection = new ProjectCollection();
            var project1 = new Project(collection) { FullPath = "c:\\y" };
            Assert.Empty(project1.GlobalProperties);

            collection.SetGlobalProperty("g1", "v1");
            collection.SetGlobalProperty("g2", "v2");
            collection.SetGlobalProperty("g2", "v2"); // try dupe

            Assert.Equal(2, project1.GlobalProperties.Count);

            collection.RemoveGlobalProperty("g2");
            var project2 = new Project(collection) { FullPath = "c:\\x" };

            Assert.Single(project1.GlobalProperties);
            Assert.Equal("v1", project2.GlobalProperties["g1"]);

            Assert.Single(project2.GlobalProperties);
            Assert.Equal("v1", project2.GlobalProperties["g1"]);
        }

        /// <summary>
        /// Changing global properties on collection should update the collection's defaults,
        /// and any projects even if they have defined the same global properties
        /// </summary>
        [Fact]
        public void SettingGlobalPropertiesOnCollectionUpdatesProjects2()
        {
            var collection = new ProjectCollection();
            var project1 = new Project(collection) { FullPath = "c:\\y" };
            // load into collection
            project1.SetGlobalProperty("g1", "v0");
            Helpers.ClearDirtyFlag(project1.Xml);

            collection.SetGlobalProperty("g1", "v1");
            collection.SetGlobalProperty("g2", "v2");

            Assert.Equal(2, project1.GlobalProperties.Count);
            Assert.Equal("v1", project1.GlobalProperties["g1"]);
            Assert.Equal("v2", project1.GlobalProperties["g2"]); // Got overwritten
            Assert.True(project1.IsDirty);
        }

        /// <summary>
        /// Changing global properties on collection should update the collection's defaults,
        /// and all projects as well
        /// </summary>
        [Fact]
        public void RemovingGlobalPropertiesOnCollectionUpdatesProjects()
        {
            var collection = new ProjectCollection();
            var project1 = new Project(collection) { FullPath = "c:\\y" };
            Assert.Empty(project1.GlobalProperties);

            Helpers.ClearDirtyFlag(project1.Xml);

            collection.SetGlobalProperty("g1", "v1"); // should make both dirty
            collection.SetGlobalProperty("g2", "v2"); // should make both dirty

            Assert.True(project1.IsDirty);

            var project2 = new Project(collection) { FullPath = "c:\\x" };

            Assert.True(project2.IsDirty);

            Assert.Equal(2, project1.GlobalProperties.Count);
            Assert.Equal("v1", project2.GlobalProperties["g1"]);

            Assert.Equal(2, project2.GlobalProperties.Count);
            Assert.Equal("v1", project2.GlobalProperties["g1"]);

            Helpers.ClearDirtyFlag(project1.Xml);
            Helpers.ClearDirtyFlag(project2.Xml);

            collection.RemoveGlobalProperty("g2"); // should make both dirty

            Assert.True(project1.IsDirty);
            Assert.True(project2.IsDirty);

            Assert.Single(project1.GlobalProperties);
            Assert.Single(project2.GlobalProperties);

            collection.RemoveGlobalProperty("g1");

            Assert.Empty(project1.GlobalProperties);
            Assert.Empty(project2.GlobalProperties);
        }

        /// <summary>
        /// Changing global properties on collection should update the collection's defaults,
        /// and all projects as well
        /// </summary>
        [Fact]
        public void RemovingGlobalPropertiesOnCollectionUpdatesProjects2()
        {
            var collection = new ProjectCollection();
            collection.SetGlobalProperty("g1", "v1");

            var project1 = new Project(collection) { FullPath = "c:\\y" };
            project1.SetGlobalProperty("g1", "v0"); // mask collection property
            Helpers.ClearDirtyFlag(project1.Xml);

            collection.RemoveGlobalProperty("g1"); // should modify the project

            Assert.Empty(project1.GlobalProperties);
            Assert.True(project1.IsDirty);
        }

        /// <summary>
        /// Unloading a project should remove it from the project collection
        /// </summary>
        [Fact]
        public void UnloadProject()
        {
            var project = new Project { FullPath = "c:\\x" };

            Assert.Equal(1, ProjectCollection.GlobalProjectCollection.Count);

            ProjectCollection.GlobalProjectCollection.UnloadProject(project); // should not throw

            Assert.Equal(0, ProjectCollection.GlobalProjectCollection.Count);
            Assert.Empty(Helpers.MakeList(ProjectCollection.GlobalProjectCollection.LoadedProjects));
        }

        /// <summary>
        /// Unloading project XML should remove it from the weak cache.
        /// </summary>
        [Fact]
        public void UnloadProjectXml()
        {
            var project = new Project { FullPath = "c:\\x" };
            ProjectRootElement xml = project.Xml;

            // Unload the evaluation project, and then the XML.
            ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            ProjectCollection.GlobalProjectCollection.UnloadProject(xml);

            try
            {
                // If the ProjectRootElement was unloaded from the cache, then
                // an attempt to load it by the pretend filename should fail,
                // so it makes a good test to see that the UnloadProject method worked.
                ProjectCollection.GlobalProjectCollection.LoadProject(xml.FullPath);
                Assert.True(false, "An InvalidProjectFileException was expected.");
            }
            catch (InvalidProjectFileException)
            {
            }
        }

        /// <summary>
        /// Unloading project XML while it is in use should result in an exception.
        /// </summary>
        [Fact]
        public void UnloadProjectXmlWhileInDirectUse()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                var project = new Project { FullPath = "c:\\x" };

                // Attempt to unload the xml before unloading the project evaluation.
                ProjectCollection.GlobalProjectCollection.UnloadProject(project.Xml);
            });
        }

        /// <summary>
        /// Unloading project XML while it is in use should result in an exception.
        /// </summary>
        [Fact]
        public void UnloadProjectXmlWhileInImportUse()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                var mainProject = new Project { FullPath = "c:\\main" };

                var importProject = new Project { FullPath = "c:\\import" };
                ProjectRootElement importedXml = importProject.Xml;

                // Import into main project
                mainProject.Xml.PrependChild(mainProject.Xml.CreateImportElement(importProject.FullPath));
                mainProject.ReevaluateIfNecessary();

                // Unload the import evaluation, but not the main project that still has a reference to it.
                ProjectCollection.GlobalProjectCollection.UnloadProject(importProject);

                // Attempt to unload the import xml before unloading the project that still references it.
                try
                {
                    ProjectCollection.GlobalProjectCollection.UnloadProject(importedXml);
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine(ex.Message);
                    throw;
                }
            }
           );
        }

        /// <summary>
        /// Renaming a project should correctly update the project collection's set of loaded projects.
        /// </summary>
        [Fact]
        public void RenameProject()
        {
            var project = new Project { FullPath = "c:\\x" };

            project.FullPath = "c:\\y";

            Assert.Equal(1, ProjectCollection.GlobalProjectCollection.Count);

            Assert.True(ReferenceEquals(project, Helpers.MakeList(ProjectCollection.GlobalProjectCollection.LoadedProjects)[0]));

            ProjectCollection.GlobalProjectCollection.UnloadProject(project); // should not throw

            Assert.Equal(0, ProjectCollection.GlobalProjectCollection.Count);
        }

        /// <summary>
        /// Validates that we don't somehow lose the ProjectCollection global properties when renaming the project. 
        /// </summary>
        [Fact]
        public void RenameProjectAndVerifyStillContainsProjectCollectionGlobalProperties()
        {
            var project = new Project { FullPath = "c:\\1" };

            // Set a global property on the project collection -- this should be passed on to all 
            // loaded projects. 
            ProjectCollection.GlobalProjectCollection.SetGlobalProperty("Configuration", "Debug");

            Assert.Equal("Debug", project.GlobalProperties["Configuration"]);

            project.FullPath = "c:\\2";

            Assert.Equal("Debug", project.GlobalProperties["Configuration"]);
        }

        /// <summary>
        /// Saving a project to a new name should correctly update the project collection's set of loaded projects.
        /// Reported by F#.
        /// </summary>
        [Fact]
        public void SaveToNewNameAndUnload()
        {
            string file1 = null;
            string file2 = null;

            try
            {
                file1 = FileUtilities.GetTemporaryFile();
                file2 = FileUtilities.GetTemporaryFile();

                Project project = new Project();
                project.Save(file1);

                ProjectCollection collection = new ProjectCollection();

                Project project2 = collection.LoadProject(file1);
                project2.Save(file2);

                collection.UnloadProject(project2);
            }
            finally
            {
                if (file1 != null) File.Delete(file1);
                if (file2 != null) File.Delete(file2);
            }
        }

        /// <summary>
        /// Saving a project to a new name after loading, unloading, and reloading, should work.
        /// Reported by F#.
        /// </summary>
        [Fact]
        public void LoadUnloadReloadSaveToNewName()
        {
            string file1 = null;
            string file2 = null;

            try
            {
                file1 = FileUtilities.GetTemporaryFile();
                file2 = FileUtilities.GetTemporaryFile();

                var project = new Project();
                project.Save(file1);
                project.ProjectCollection.UnloadProject(project);

                var collection = new ProjectCollection();

                Project project2 = collection.LoadProject(file1);
                collection.UnloadProject(project2);

                Project project3 = collection.LoadProject(file1);
                project3.Save(file2); // should not crash

                collection.UnloadProject(project3);
            }
            finally
            {
                if (file1 != null) File.Delete(file1);
                if (file2 != null) File.Delete(file2);
            }
        }

        /// <summary>
        /// Saving a project to a new name after loading, unloading, and reloading, should work.
        /// Reported by F#.
        /// </summary>
        [Fact]
        public void LoadUnloadAllReloadSaveToNewName()
        {
            string file1 = null;
            string file2 = null;

            try
            {
                file1 = FileUtilities.GetTemporaryFile();
                file2 = FileUtilities.GetTemporaryFile();

                var project = new Project();
                project.Save(file1);
                project.ProjectCollection.UnloadProject(project);

                var collection = new ProjectCollection();

                collection.LoadProject(file1);
                collection.UnloadAllProjects();

                Project project3 = collection.LoadProject(file1);
                project3.Save(file2); // should not crash

                collection.UnloadProject(project3);
            }
            finally
            {
                if (file1 != null) File.Delete(file1);
                if (file2 != null) File.Delete(file2);
            }
        }

        /// <summary>
        /// Add a toolset
        /// </summary>
        [Fact]
        public void AddToolset()
        {
            var collection = new ProjectCollection();
            collection.RemoveAllToolsets();

            var toolset = new Toolset("x", "c:\\y", collection, null);
            collection.AddToolset(toolset);

            Assert.Equal(toolset, collection.GetToolset("x"));
            Assert.True(collection.ContainsToolset("x"));

            List<Toolset> toolsets = Helpers.MakeList(collection.Toolsets);
            Assert.Single(toolsets);
            Assert.Equal(toolset, toolsets[0]);
        }

        /// <summary>
        /// Add two toolsets
        /// </summary>
        [Fact]
        public void AddTwoToolsets()
        {
            var collection = new ProjectCollection();
            collection.RemoveAllToolsets();

            var toolset1 = new Toolset("x", "c:\\y", collection, null);
            var toolset2 = new Toolset("y", "c:\\z", collection, null);

            collection.AddToolset(toolset1);
            collection.AddToolset(toolset2);

            Assert.Equal(toolset1, collection.GetToolset("x"));
            Assert.Equal(toolset2, collection.GetToolset("y"));

            List<Toolset> toolsets = Helpers.MakeList(collection.Toolsets);
            Assert.Equal(2, toolsets.Count);
            Assert.Contains(toolset1, toolsets);
            Assert.Contains(toolset2, toolsets);
        }

        /// <summary>
        /// Add a toolset that overrides another
        /// </summary>
        [Fact]
        public void ReplaceToolset()
        {
            var collection = new ProjectCollection();
            collection.RemoveAllToolsets();

            var toolset1 = new Toolset("x", "c:\\y", collection, null);
            var toolset2 = new Toolset("x", "c:\\z", collection, null);

            collection.AddToolset(toolset1);
            collection.AddToolset(toolset2);

            Assert.Equal(toolset2, collection.GetToolset("x"));

            List<Toolset> toolsets = Helpers.MakeList(collection.Toolsets);
            Assert.Single(toolsets);
            Assert.Equal(toolset2, toolsets[0]);
        }

        /// <summary>
        /// Attempt to add a null toolset
        /// </summary>
        [Fact]
        public void AddNullToolset()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectCollection.GlobalProjectCollection.AddToolset(null);
            });
        }

        /// <summary>
        /// Remove a toolset
        /// </summary>
        [Fact]
        public void RemoveToolset()
        {
            var collection = new ProjectCollection();

            var toolset1 = new Toolset("x", "c:\\y", collection, null);
            var toolset2 = new Toolset("y", "c:\\z", collection, null);

            int initial = Helpers.MakeList(collection.Toolsets).Count;

            collection.AddToolset(toolset1);
            collection.AddToolset(toolset2);

            Assert.True(collection.RemoveToolset("x"));
            Assert.False(collection.ContainsToolset("x"));

            Assert.Equal(1, Helpers.MakeList(collection.Toolsets).Count - initial);
        }

        /// <summary>
        /// Remove a nonexistent toolset
        /// </summary>
        [Fact]
        public void RemoveNonexistentToolset()
        {
            var collection = new ProjectCollection();
            Assert.False(collection.RemoveToolset("nonexistent"));
        }

        /// <summary>
        /// Attempt to remove a null tools version
        /// </summary>
        [Fact]
        public void RemoveNullToolsVersion()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectCollection.GlobalProjectCollection.RemoveToolset(null);
            });
        }

        /// <summary>
        /// Attempt to remove an empty string toolsversion
        /// </summary>
        [Fact]
        public void RemoveEmptyToolsVersion()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectCollection.GlobalProjectCollection.RemoveToolset(String.Empty);
            });
        }

        /// <summary>
        /// Current default from registry is 2.0 if 2.0 is installed
        /// </summary>
        [Fact]
        public void DefaultToolsVersion()
        {
            if (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20) == null)
            {
                // "Requires 2.0 to be installed"
                return;
            }

            var collection = new ProjectCollection();
#if STANDALONEBUILD
            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, collection.DefaultToolsVersion);
#else
            Assert.Equal("2.0", collection.DefaultToolsVersion);
#endif
        }

        /// <summary>
        /// Current default from registry is 4.0 if 2.0 is not installed
        /// </summary>
#if FEATURE_INSTALLED_MSBUILD
        [Fact]
#else
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/276")]
#endif
        [Trait("Category", "mono-osx-failing")]
        public void DefaultToolsVersion2()
        {
            if (ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20) != null)
            {
                // "Requires 2.0 to NOT be installed"
                return;
            }

            var collection = new ProjectCollection(null, null, ToolsetDefinitionLocations.Registry);
            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, collection.DefaultToolsVersion);
        }

        /// <summary>
        /// Error setting default tools version to empty
        /// </summary>
        [Fact]
        public void SetDefaultToolsVersionEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectCollection.GlobalProjectCollection.DefaultToolsVersion = String.Empty;
            });
        }

        /// <summary>
        /// Error setting default tools version to a toolset that does not exist
        /// </summary>
        [Fact]
        public void SetDefaultToolsVersionNonexistentToolset()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectCollection.GlobalProjectCollection.DefaultToolsVersion = "nonexistent";
            });
        }

        /// <summary>
        /// Set default tools version; subsequent projects should use it 
        /// </summary>
        [Fact]
        public void SetDefaultToolsVersion()
        {
            var collection = new ProjectCollection();
            collection.AddToolset(new Toolset("x", @"c:\y", collection, null));

            collection.DefaultToolsVersion = "x";

            Assert.Equal("x", collection.DefaultToolsVersion);

            string content = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' >
                        <Target Name='t'/>
                    </Project>
                ");

            var project = new Project(XmlReader.Create(new StringReader(content)), null, null, collection);

            // ... and after all that, we end up defaulting to the current ToolsVersion instead.  There's a way 
            // to turn this behavior (new in Dev12) off, but it requires setting an environment variable and 
            // clearing some internal state to make sure that the update environment variable is picked up, so 
            // there's not a good way of doing it from these deliberately public OM only tests. 
            Assert.Equal(project.ToolsVersion, ObjectModelHelpers.MSBuildDefaultToolsVersion);
        }

        /// <summary>
        /// Changes to the ProjectCollection object should raise a ProjectCollectionChanged event.
        /// </summary>
        [Fact]
        public void ProjectCollectionChangedEvent()
        {
            var collection = new ProjectCollection();
            bool dirtyRaised = false;
            ProjectCollectionChangedState expectedChange = ProjectCollectionChangedState.Loggers;
            collection.ProjectCollectionChanged +=
                (sender, e) =>
                {
                    Assert.Same(collection, sender);
                    Assert.Equal(expectedChange, e.Changed);
                    dirtyRaised = true;
                };
            Assert.False(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.DisableMarkDirty;
            dirtyRaised = false;
            collection.DisableMarkDirty = true; // LEAVE THIS TRUE for rest of the test, to verify it doesn't suppress these events
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.IsBuildEnabled;
            dirtyRaised = false;
            collection.IsBuildEnabled = !collection.IsBuildEnabled;
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.OnlyLogCriticalEvents;
            dirtyRaised = false;
            collection.OnlyLogCriticalEvents = !collection.OnlyLogCriticalEvents;
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.SkipEvaluation;
            dirtyRaised = false;
            collection.SkipEvaluation = !collection.SkipEvaluation;
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.GlobalProperties;
            dirtyRaised = false;
            collection.SetGlobalProperty("a", "b");
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.GlobalProperties;
            dirtyRaised = false;
            collection.RemoveGlobalProperty("a");
            Assert.True(dirtyRaised);

            // Verify HostServices changes raise the event.
            expectedChange = ProjectCollectionChangedState.HostServices;
            dirtyRaised = false;
            collection.HostServices = new HostServices();
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.Loggers;
            dirtyRaised = false;
            collection.RegisterLogger(new MockLogger());
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.Loggers;
            dirtyRaised = false;
            collection.RegisterLoggers(new Build.Framework.ILogger[] { new MockLogger(), new MockLogger() });
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.Loggers;
            dirtyRaised = false;
            collection.UnregisterAllLoggers();
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.Toolsets;
            dirtyRaised = false;
            collection.AddToolset(new Toolset("testTools", Path.GetTempPath(), collection, Path.GetTempPath()));
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.DefaultToolsVersion;
            dirtyRaised = false;
            collection.DefaultToolsVersion = "testTools";
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.Toolsets;
            dirtyRaised = false;
            collection.RemoveToolset("testTools");
            Assert.True(dirtyRaised);

            expectedChange = ProjectCollectionChangedState.Toolsets;
            dirtyRaised = false;
            collection.RemoveAllToolsets();
            Assert.True(dirtyRaised);
        }

        /// <summary>
        /// Changes to the ProjectCollection object should raise a ProjectCollectionChanged event.
        /// </summary>
        [Fact]
        public void ProjectCollectionChangedEvent2()
        {
            // Verify if the project, project collection and the value we are setting in the project collection are all the same
            // then the projects value for the property should not change and no event should be fired.
            var collection = new ProjectCollection();
            XmlReader reader = CreateProjectXmlReader();
            Project project = collection.LoadProject(reader, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            project.SetProperty("a", "1");
            collection.SetGlobalProperty("a", "1");
            VerifyProjectCollectionEvents(collection, false, "1");

            // Verify if the project, project collection and the value we are setting in the project collection are all the same
            // then the projects value for the property should not change and no event should be fired.
            collection = new ProjectCollection();
            reader = CreateProjectXmlReader();
            project = collection.LoadProject(reader, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            project.SetProperty("a", "%28x86%29");
            collection.SetGlobalProperty("a", "%28x86%29");
            VerifyProjectCollectionEvents(collection, false, "%28x86%29");

            // Verify if the project, project collection have the same value but a new value is set in the project collection
            // then the projects value for the property should be change and an event should be fired.
            collection = new ProjectCollection();
            reader = CreateProjectXmlReader();
            project = collection.LoadProject(reader, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            project.SetProperty("a", "1");
            collection.SetGlobalProperty("a", "1");
            VerifyProjectCollectionEvents(collection, true, "2");

            // Verify if the project, project collection have the same value but a new value is set in the project collection
            // then the projects value for the property should be change and an event should be fired.
            collection = new ProjectCollection();
            reader = CreateProjectXmlReader();
            project = collection.LoadProject(reader, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            project.SetProperty("a", "1");
            collection.SetGlobalProperty("a", "(x86)");
            VerifyProjectCollectionEvents(collection, true, "%28x86%29");

            // Verify if the project has one value and project collection and the property we are setting on the project collection have the same value
            // then the projects value for the property should be change but no event should be fired
            collection = new ProjectCollection();
            reader = CreateProjectXmlReader();
            project = collection.LoadProject(reader, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            project.SetProperty("a", "2");
            collection.SetGlobalProperty("a", "1");

            VerifyProjectCollectionEvents(collection, false, "1");

            // Verify if the project and the property being set have one value but the project collection has another
            // then the projects value for the property should not change and event should be fired
            collection = new ProjectCollection();
            reader = CreateProjectXmlReader();
            project = collection.LoadProject(reader, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            project.SetProperty("a", "1");
            collection.SetGlobalProperty("a", "2");
            VerifyProjectCollectionEvents(collection, true, "1");

            // item is added to project collection for the first time. Make sure it is added to the project and an event is fired.
            collection = new ProjectCollection();
            reader = CreateProjectXmlReader();
            project = collection.LoadProject(reader, ObjectModelHelpers.MSBuildDefaultToolsVersion);
            VerifyProjectCollectionEvents(collection, true, "1");
        }

        /// <summary>
        /// Changes to project XML should raise an ProjectXmlChanged event.
        /// </summary>
        [Fact]
        public void ProjectXmlChangedEvent()
        {
            var collection = new ProjectCollection();
            ProjectRootElement pre = null;
            bool dirtyRaised = false;
            collection.ProjectXmlChanged +=
                (sender, e) =>
                {
                    Assert.Same(collection, sender);
                    Assert.Same(pre, e.ProjectXml);
                    TestOutput.WriteLine(e.Reason ?? String.Empty);
                    dirtyRaised = true;
                };
            Assert.False(dirtyRaised);

            // Ensure that the event is raised even when DisableMarkDirty is set.
            collection.DisableMarkDirty = true;

            // Create a new PRE but don't change the template.
            dirtyRaised = false;
            pre = ProjectRootElement.Create(collection);
            Assert.False(dirtyRaised);

            // Change PRE prior to setting a filename and thus associating the PRE with the ProjectCollection.
            dirtyRaised = false;
            pre.AppendChild(pre.CreatePropertyGroupElement());
            Assert.False(dirtyRaised);

            // Associate with the ProjectCollection
            dirtyRaised = false;
            pre.FullPath = FileUtilities.GetTemporaryFile();
            Assert.True(dirtyRaised);

            // Now try dirtying again and see that the event is raised this time.
            dirtyRaised = false;
            pre.AppendChild(pre.CreatePropertyGroupElement());
            Assert.True(dirtyRaised);

            // Make sure that project collection global properties don't raise this event.
            dirtyRaised = false;
            collection.SetGlobalProperty("a", "b");
            Assert.False(dirtyRaised);

            // Change GlobalProperties on a project to see that doesn't propagate as an XML change.
            dirtyRaised = false;
            var project = new Project(pre);
            project.SetGlobalProperty("q", "s");
            Assert.False(dirtyRaised);

            // Change XML via the Project to verify the event is raised.
            dirtyRaised = false;
            project.SetProperty("z", "y");
            Assert.True(dirtyRaised);
        }

        /// <summary>
        /// Changes to a Project evaluation object should raise a ProjectChanged event.
        /// </summary>
        [Fact]
        public void ProjectChangedEvent()
        {
            var collection = new ProjectCollection();
            Project project = null;
            bool dirtyRaised = false;
            collection.ProjectChanged +=
                (sender, e) =>
                {
                    Assert.Same(collection, sender);
                    Assert.Same(project, e.Project);
                    dirtyRaised = true;
                };
            Assert.False(dirtyRaised);

            ProjectRootElement pre = ProjectRootElement.Create(collection);
            project = new Project(pre, null, null, collection);

            // all these should still pass with disableMarkDirty set
            collection.DisableMarkDirty = true;
            project.DisableMarkDirty = true;

            dirtyRaised = false;
            pre.AppendChild(pre.CreatePropertyGroupElement());
            Assert.False(dirtyRaised); // "Dirtying the XML directly should not result in a ProjectChanged event."

            // No events should be raised before we associate a filename with the PRE
            dirtyRaised = false;
            project.SetGlobalProperty("someGlobal", "someValue");
            Assert.False(dirtyRaised);

            dirtyRaised = false;
            project.SetProperty("someProp", "someValue");
            Assert.False(dirtyRaised);

            pre.FullPath = FileUtilities.GetTemporaryFile();
            dirtyRaised = false;
            project.SetGlobalProperty("someGlobal", "someValue2");
            Assert.True(dirtyRaised);

            dirtyRaised = false;
            project.RemoveGlobalProperty("someGlobal");
            Assert.True(dirtyRaised);

            dirtyRaised = false;
            collection.SetGlobalProperty("somePCglobal", "someValue");
            Assert.True(dirtyRaised);

            dirtyRaised = false;
            project.SetProperty("someProp", "someValue2");
            Assert.True(dirtyRaised);
        }

        /// <summary>
        /// Verifies that the <see cref="ProjectCollection.Version"/> is correct.
        /// </summary>
        /// <remarks>
        /// This test was written because on OS X and Ubuntu, the version is showing "0.0.0.0"
        /// while Windows was working just fine.
        /// </remarks>
        [Fact]
        public void ProjectCollectionVersionIsCorrect()
        {
            Version expectedVersion = new Version(this.GetType().GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version);

            ProjectCollection.Version.Major.ShouldBe(expectedVersion.Major);
            ProjectCollection.Version.Minor.ShouldBe(expectedVersion.Minor);
        }

        /// <summary>
        /// Create an empty project file and return the path
        /// </summary>
        private static string CreateProjectFile()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            string path = FileUtilities.GetTemporaryFile();
            xml.Save(path);
            return path;
        }

        /// <summary>
        /// Create an XmlReader around an empty project file content
        /// </summary>
        private static XmlReader CreateProjectXmlReader()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            XmlReader reader = XmlReader.Create(new StringReader(xml.RawXml));
            return reader;
        }

        /// <summary>
        /// Verify that when a property is set on the project collection that the correct events are fired.
        /// </summary>
        private static void VerifyProjectCollectionEvents(ProjectCollection collection, bool expectEventRaised, string propertyValue)
        {
            bool raisedEvent = false;
            ProjectCollectionChangedState expectedChange = ProjectCollectionChangedState.Loggers;
            collection.ProjectCollectionChanged +=
                (sender, e) =>
                {
                    Assert.Same(collection, sender);
                    Assert.Equal(expectedChange, e.Changed);
                    raisedEvent = true;
                };

            expectedChange = ProjectCollectionChangedState.GlobalProperties;
            collection.SetGlobalProperty("a", propertyValue);
            Assert.Equal(raisedEvent, expectEventRaised);
            ProjectPropertyInstance property = collection.GetGlobalProperty("a");
            Assert.NotNull(property);
            Assert.Equal(ProjectCollection.Unescape(propertyValue), property.EvaluatedValue);
        }
    }
}
