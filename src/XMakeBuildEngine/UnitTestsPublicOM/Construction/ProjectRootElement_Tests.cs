// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Test the ProjectRootElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if FEATURE_SECURITY_PRINCIPAL_WINDOWS
using System.Security.AccessControl;
using System.Security.Principal;
#endif
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ProjectCollection = Microsoft.Build.Evaluation.ProjectCollection;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectRootElement class
    /// </summary>
    public class ProjectRootElement_Tests
    {
        /// <summary>
        /// Empty project content
        /// </summary>
        [Fact]
        public void EmptyProject()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            Assert.Equal(0, Helpers.Count(project.Children));
            Assert.Equal(string.Empty, project.DefaultTargets);
            Assert.Equal(string.Empty, project.InitialTargets);
            Assert.Equal(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.ToolsVersion);
            Assert.Equal(true, project.HasUnsavedChanges); // it is indeed unsaved
        }

        /// <summary>
        /// Set defaulttargets
        /// </summary>
        [Fact]
        public void SetDefaultTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            project.DefaultTargets = "dt";
            Assert.Equal("dt", project.DefaultTargets);
            Assert.Equal(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set initialtargets
        /// </summary>
        [Fact]
        public void SetInitialTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            project.InitialTargets = "it";
            Assert.Equal("it", project.InitialTargets);
            Assert.Equal(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set toolsversion
        /// </summary>
        [Fact]
        public void SetToolsVersion()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            project.ToolsVersion = "tv";
            Assert.Equal("tv", project.ToolsVersion);
            Assert.Equal(true, project.HasUnsavedChanges);
        }

        /// <summary>
        /// Setting full path should accept and update relative path
        /// </summary>
        [Fact]
        public void SetFullPath()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.FullPath = "X";

            Assert.Equal(project.FullPath, Path.Combine(Directory.GetCurrentDirectory(), "X"));
        }

        /// <summary>
        /// Attempting to load a second ProjectRootElement over the same file path simply
        /// returns the first one.
        /// A ProjectRootElement is notionally a "memory mapped" view of a file, and we assume there is only
        /// one per file path, so we must reject attempts to make another.
        /// </summary>
        [Fact]
        public void ConstructOverSameFileReturnsSame()
        {
            ProjectRootElement projectXml1 = ProjectRootElement.Create();
            projectXml1.Save(FileUtilities.GetTemporaryFile());

            ProjectRootElement projectXml2 = ProjectRootElement.Open(projectXml1.FullPath);

            Assert.Equal(true, object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Attempting to load a second ProjectRootElement over the same file path simply
        /// returns the first one. This should work even if one of the paths is not a full path.
        /// </summary>
        [Fact]
        public void ConstructOverSameFileReturnsSameEvenWithOneBeingRelativePath()
        {
            ProjectRootElement projectXml1 = ProjectRootElement.Create();

            projectXml1.FullPath = Path.Combine(Directory.GetCurrentDirectory(), @"xyz\abc");

            ProjectRootElement projectXml2 = ProjectRootElement.Open(@"xyz\abc");

            Assert.Equal(true, object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Attempting to load a second ProjectRootElement over the same file path simply
        /// returns the first one. This should work even if one of the paths is not a full path.
        /// </summary>
        [Fact]
        public void ConstructOverSameFileReturnsSameEvenWithOneBeingRelativePath2()
        {
            ProjectRootElement projectXml1 = ProjectRootElement.Create();

            projectXml1.FullPath = @"xyz\abc";

            ProjectRootElement projectXml2 = ProjectRootElement.Open(Path.Combine(Directory.GetCurrentDirectory(), @"xyz\abc"));

            Assert.Equal(true, object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Using TextReader
        /// </summary>
        [Fact]
        public void ConstructOverSameFileReturnsSameEvenWithOneBeingRelativePath3()
        {
            string content = "<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\r\n</Project>";

            ProjectRootElement projectXml1 = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            projectXml1.FullPath = @"xyz\abc";

            ProjectRootElement projectXml2 = ProjectRootElement.Open(Path.Combine(Directory.GetCurrentDirectory(), @"xyz\abc"));

            Assert.Equal(true, object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Using TextReader
        /// </summary>
        [Fact]
        public void ConstructOverSameFileReturnsSameEvenWithOneBeingRelativePath4()
        {
            string content = "<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\r\n</Project>";

            ProjectRootElement projectXml1 = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            projectXml1.FullPath = Path.Combine(Directory.GetCurrentDirectory(), @"xyz\abc");

            ProjectRootElement projectXml2 = ProjectRootElement.Open(@"xyz\abc");

            Assert.Equal(true, object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Two ProjectRootElement's over the same file path does not throw (although you shouldn't do it)
        /// </summary>
        [Fact]
        public void SetFullPathProjectXmlAlreadyLoaded()
        {
            ProjectRootElement projectXml1 = ProjectRootElement.Create();
            projectXml1.FullPath = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();

            ProjectRootElement projectXml2 = ProjectRootElement.Create();
            projectXml2.FullPath = projectXml1.FullPath;
        }

        /// <summary>
        /// Invalid XML
        /// </summary>
        [Fact]
        public void InvalidXml()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader("XXX")));
            }
           );
        }

        /// <summary>
        /// Valid Xml, invalid namespace on the root
        /// </summary>
        [Fact]
        public void InvalidNamespace()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                var content = @"<Project xmlns='XXX'/>";
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        /// <summary>
        /// Invalid root tag
        /// </summary>
        [Fact]
        public void InvalidRootTag()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <XXX xmlns='http://schemas.microsoft.com/developer/msbuild/2003'/>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Valid Xml, invalid syntax below the root
        /// </summary>
        [Fact]
        public void InvalidChildBelowRoot()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <XXX/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Root indicates upgrade needed
        /// </summary>
        [Fact]
        public void NeedsUpgrade()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <VisualStudioProject/>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Valid Xml, invalid namespace below the root
        /// </summary>
        [Fact]
        public void InvalidNamespaceBelowRoot()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <PropertyGroup xmlns='XXX'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Tests that the namespace error reports are correct
        /// </summary>
        [Fact]
        public void InvalidNamespaceErrorReport()
        {
            string content = @"
<msb:Project xmlns:msb=`http://schemas.microsoft.com/developer/msbuild/2003`>
    <msb:Target Name=`t`>
        <msb:Message Text=`[t]`/>
    </msb:Target>
</msb:Project>
                ";

            content = content.Replace("`", "\"");
            MockLogger logger = new MockLogger();
            bool exceptionThrown = false;
            try
            {
                Project project = new Project(XmlReader.Create(new StringReader(content)));
            }
            catch (InvalidProjectFileException ex)
            {
                exceptionThrown = true;

                // MSB4068: The element <msb:Project> is unrecognized, or not supported in this context.
                Assert.NotEqual(ex.ErrorCode, "MSB4068");

                // MSB4041: The default XML namespace of the project must be the MSBuild XML namespace.
                Assert.Equal("MSB4041", ex.ErrorCode);
            }

            Assert.True(exceptionThrown); // "ERROR: An invalid project file exception should have been thrown."
        }

        /// <summary>
        /// Valid Xml, invalid syntax thrown by child element parsing
        /// </summary>
        [Fact]
        public void ValidXmlInvalidSyntaxInChildElement()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                           <XXX YYY='ZZZ'/>
                        </ItemGroup> 
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Valid Xml, invalid syntax, should not get added to the Xml cache and
        /// thus returned on the second request!
        /// </summary>
        [Fact]
        public void ValidXmlInvalidSyntaxOpenFromDiskTwice()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                           <XXX YYY='ZZZ'/>
                        </ItemGroup> 
                    </Project>
                ";

                string path = null;

                try
                {
                    try
                    {
                        path = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                        File.WriteAllText(path, content);

                        ProjectRootElement.Open(path);
                    }
                    catch (InvalidProjectFileException)
                    {
                    }

                    // Should throw again, not get from cache
                    ProjectRootElement.Open(path);
                }
                finally
                {
                    File.Delete(path);
                }
            }
           );
        }
        /// <summary>
        /// Verify that opening project using XmlTextReader does not add it to the Xml cache
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        public void ValidXmlXmlTextReaderNotCache()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    </Project>
                ";

            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile();
                File.WriteAllText(path, content);

                var reader1 = XmlReader.Create(path);
                ProjectRootElement root1 = ProjectRootElement.Create(reader1);
                root1.AddItem("type", "include");

                // If it's in the cache, then the 2nd document won't see the add.
                var reader2 = XmlReader.Create(path);
                ProjectRootElement root2 = ProjectRootElement.Create(reader2);

                Assert.Equal(1, root1.Items.Count);
                Assert.Equal(0, root2.Items.Count);

                reader1.Dispose();
                reader2.Dispose();
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Verify that opening project using the same path adds it to the Xml cache
        /// </summary>
        [Fact]
        public void ValidXmlXmlReaderCache()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    </Project>
                ";

            string content2 = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' DefaultTargets='t'>
                    </Project>
                ";

            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFile();
                File.WriteAllText(path, content);

                ProjectRootElement root1 = ProjectRootElement.Create(path);

                File.WriteAllText(path, content2);

                // If it went in the cache, and this path also reads from the cache,
                // then we'll see the first version of the file.
                ProjectRootElement root2 = ProjectRootElement.Create(path);

                Assert.Equal(string.Empty, root1.DefaultTargets);
                Assert.Equal(string.Empty, root2.DefaultTargets);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// A simple "system" test: load microsoft.*.targets and verify we don't throw
        /// </summary>
        [Fact]
        public void LoadCommonTargets()
        {
            ProjectCollection projectCollection = new ProjectCollection();
            string toolsPath = projectCollection.Toolsets.Where(toolset => (string.Compare(toolset.ToolsVersion, ObjectModelHelpers.MSBuildDefaultToolsVersion, StringComparison.OrdinalIgnoreCase) == 0)).First().ToolsPath;

            string[] targets =
            {
                "Microsoft.Common.targets",
                "Microsoft.CSharp.targets",
                "Microsoft.VisualBasic.targets"
            };

            foreach (string target in targets)
            {
                string path = Path.Combine(toolsPath, target);
                ProjectRootElement project = ProjectRootElement.Open(path);
                Console.WriteLine(@"Loaded target: {0}", target);
                Console.WriteLine(@"Children: {0}", Helpers.Count(project.Children));
                Console.WriteLine(@"Targets: {0}", Helpers.MakeList(project.Targets).Count);
                Console.WriteLine(@"Root ItemGroups: {0}", Helpers.MakeList(project.ItemGroups).Count);
                Console.WriteLine(@"Root PropertyGroups: {0}", Helpers.MakeList(project.PropertyGroups).Count);
                Console.WriteLine(@"UsingTasks: {0}", Helpers.MakeList(project.UsingTasks).Count);
                Console.WriteLine(@"ItemDefinitionGroups: {0}", Helpers.MakeList(project.ItemDefinitionGroups).Count);
            }
        }

        /// <summary>
        /// Save project loaded from TextReader, without setting FullPath.
        /// </summary>
        [Fact]
        public void InvalidSaveWithoutFullPath()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                XmlReader reader = XmlReader.Create(new StringReader("<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"/>"));
                ProjectRootElement project = ProjectRootElement.Create(reader);

                project.Save();
            }
           );
        }
        /// <summary>
        /// Save content with transforms.
        /// The ">" should not turn into "&lt;"
        /// </summary>
        [Fact]
        public void SaveWithTransforms()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "@(h->'%(x)')");

            StringBuilder builder = new StringBuilder();
            StringWriter writer = new StringWriter(builder);

            project.Save(writer);

            // UTF-16 because writer.Encoding is UTF-16
            string expected = ObjectModelHelpers.CleanupFileContents(
@"<?xml version=""1.0"" encoding=""utf-16""?>
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""@(h->'%(x)')"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertLineByLine(expected, builder.ToString());
        }

        /// <summary>
        /// Save content with transforms to a file.
        /// The ">" should not turn into "&lt;"
        /// </summary>
        [Fact]
        public void SaveWithTransformsToFile()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "@(h->'%(x)')");

            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();

                project.Save(file);

                string expected = ObjectModelHelpers.CleanupFileContents(
    @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""@(h->'%(x)')"" />
  </ItemGroup>
</Project>");

                string actual = File.ReadAllText(file);
                Helpers.VerifyAssertLineByLine(expected, actual);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Save should create a directory if it is missing
        /// </summary>
        [Fact]
        public void SaveToNonexistentDirectory()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            string directory = null;

            try
            {
                directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                string file = "foo.proj";
                string path = Path.Combine(directory, file);

                project.Save(path);

                Assert.True(File.Exists(path));
                Assert.Equal(path, project.FullPath);
                Assert.Equal(directory, project.DirectoryPath);
            }
            finally
            {
                FileUtilities.DeleteWithoutTrailingBackslash(directory, true);
            }
        }

        /// <summary>
        /// Save should create a directory if it is missing
        /// </summary>
        [Fact]
        public void SaveToNonexistentDirectoryRelativePath()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            string directory = null;

            string savedCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(Path.GetTempPath()); // should be used for project.DirectoryPath; it must exist
                // Use the *real* current directory for constructing the path
                var curDir = Directory.GetCurrentDirectory();

                string file = "bar" + Path.DirectorySeparatorChar + "foo.proj";
                string path = Path.Combine(curDir, file);
                directory = Path.Combine(curDir, "bar");

                project.Save(file); // relative path: file and a single directory only; should create the "bar" part

                Assert.True(File.Exists(file));
                Assert.Equal(path, project.FullPath);
                Assert.Equal(directory, project.DirectoryPath);
            }
            finally
            {
                FileUtilities.DeleteWithoutTrailingBackslash(directory, true);

                Directory.SetCurrentDirectory(savedCurrentDirectory);
            }
        }

        /// <summary>
        /// Saving an unnamed project without a path specified should give a nice exception
        /// </summary>
        [Fact]
        public void SaveUnnamedProject()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                project.Save();
            }
           );
        }
        /// <summary>
        /// Verifies that the ProjectRootElement.Encoding property getter returns values
        /// that are based on the XML declaration in the file.
        /// </summary>
        [Fact]
        public void EncodingGetterBasedOnXmlDeclaration()
        {
            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-16""?>
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
</Project>"))));
            Assert.Equal(Encoding.Unicode, project.Encoding);

            project = ProjectRootElement.Create(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
</Project>"))));
            Assert.Equal(Encoding.UTF8, project.Encoding);

            project = ProjectRootElement.Create(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""us-ascii""?>
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
</Project>"))));
            Assert.Equal(Encoding.ASCII, project.Encoding);
        }

        /// <summary>
        /// Verifies that ProjectRootElement.Encoding returns the correct value
        /// after reading a file off disk, even if no xml declaration is present.
        /// </summary>
#if FEATURE_ENCODING_DEFAULT
        [Fact]
#else
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/301")]
#endif
        [Trait("Category", "netcore-osx-failing")]
        public void EncodingGetterBasedOnActualEncodingWhenXmlDeclarationIsAbsent()
        {
            string projectFullPath = FileUtilities.GetTemporaryFile();
            try
            {
                VerifyLoadedProjectHasEncoding(projectFullPath, Encoding.UTF8);
                VerifyLoadedProjectHasEncoding(projectFullPath, Encoding.Unicode);

                // We don't test ASCII, since there is no byte order mark for it,
                // and the XmlReader will legitimately decide to intrepret it as UTF8,
                // which would fail the test although it's a reasonable assumption
                // when no xml declaration is present.
                ////VerifyLoadedProjectHasEncoding(projectFullPath, Encoding.ASCII);
            }
            finally
            {
                File.Delete(projectFullPath);
            }
        }

        /// <summary>
        /// Verifies that the Save method saves an otherwise unmodified project
        /// with a specified file encoding.
        /// </summary>
        [Fact]
        public void SaveUnmodifiedWithNewEncoding()
        {
            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
</Project>"))));
            project.FullPath = FileUtilities.GetTemporaryFile();
            string projectFullPath = project.FullPath;
            try
            {
                project.Save();
                project = null;

                // We haven't made any changes to the project, but we want to save it using various encodings.
                SaveProjectWithEncoding(projectFullPath, Encoding.Unicode);
                SaveProjectWithEncoding(projectFullPath, Encoding.ASCII);
                SaveProjectWithEncoding(projectFullPath, Encoding.UTF8);
            }
            finally
            {
                File.Delete(projectFullPath);
            }
        }

        /// <summary>
        /// Enumerate over all properties from the project directly.
        /// It should traverse into Choose's.
        /// </summary>
        [Fact]
        public void PropertiesEnumerator()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                  @"<?xml version=""1.0"" encoding=""utf-16""?>
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <PropertyGroup Condition=""false"">
                            <p>p1</p>
                            <q>q1</q>
                        </PropertyGroup>
                        <PropertyGroup/>
                        <PropertyGroup>
                            <r>r1</r>
                        </PropertyGroup>
                        <Choose>
                            <When Condition=""true"">
                                <Choose>
                                    <When Condition=""true"">
                                        <PropertyGroup>
                                            <s>s1</s>
                                        </PropertyGroup>
                                    </When>
                                </Choose>
                            </When>
                            <When Condition=""false"">
                                <PropertyGroup>
                                    <s>s2</s> <!-- both esses -->
                                </PropertyGroup>
                            </When>
                            <Otherwise>
                                <Choose>
                                    <When Condition=""false""/>
                                    <Otherwise>
                                        <PropertyGroup>
                                            <t>t1</t>
                                        </PropertyGroup>
                                    </Otherwise>
                                </Choose>
                            </Otherwise>
                        </Choose>
                    </Project>");

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            List<ProjectPropertyElement> properties = Helpers.MakeList(project.Properties);

            Assert.Equal(6, properties.Count);

            Assert.Equal("q", properties[1].Name);
            Assert.Equal("r1", properties[2].Value);
            Assert.Equal("t1", properties[5].Value);
        }

        /// <summary>
        /// Enumerate over all items from the project directly.
        /// It should traverse into Choose's.
        /// </summary>
        [Fact]
        public void ItemsEnumerator()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
                  @"<?xml version=""1.0"" encoding=""utf-16""?>
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <ItemGroup Condition=""false"">
                            <i Include=""i1""/>
                            <j Include=""j1""/>
                        </ItemGroup>
                        <ItemGroup/>
                        <ItemGroup>
                            <k Include=""k1""/>
                        </ItemGroup>
                        <Choose>
                            <When Condition=""true"">
                                <Choose>
                                    <When Condition=""true"">
                                        <ItemGroup>
                                            <k Include=""k2""/>
                                        </ItemGroup>
                                    </When>
                                </Choose>
                            </When>
                            <When Condition=""false"">
                                <ItemGroup>
                                    <k Include=""k3""/>
                                </ItemGroup>
                            </When>
                            <Otherwise>
                                <Choose>
                                    <When Condition=""false""/>
                                    <Otherwise>
                                        <ItemGroup>
                                            <k Include=""k4""/>
                                        </ItemGroup>
                                    </Otherwise>
                                </Choose>
                            </Otherwise>
                        </Choose>
                    </Project>");

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            List<ProjectItemElement> items = Helpers.MakeList(project.Items);

            Assert.Equal(6, items.Count);

            Assert.Equal("j", items[1].ItemType);
            Assert.Equal("k1", items[2].Include);
            Assert.Equal("k4", items[5].Include);
        }

#if FEATURE_SECURITY_PRINCIPAL_WINDOWS
        /// <summary>
        /// Build a solution file that can't be accessed
        /// </summary>
        [Fact]
        public void SolutionCanNotBeOpened()
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                // Security classes are not supported on Unix
                return;
            }

            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string solutionFile = null;
                string tempFileSentinel = null;

                IdentityReference identity = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                FileSystemAccessRule rule = new FileSystemAccessRule(identity, FileSystemRights.Read, AccessControlType.Deny);

                FileSecurity security = null;

                try
                {
                    tempFileSentinel = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                    solutionFile = Path.ChangeExtension(tempFileSentinel, ".sln");
                    File.Copy(tempFileSentinel, solutionFile);

                    security = new FileSecurity(solutionFile, System.Security.AccessControl.AccessControlSections.All);

                    security.AddAccessRule(rule);

                    File.SetAccessControl(solutionFile, security);

                    ProjectRootElement p = ProjectRootElement.Open(solutionFile);
                }
                catch (PrivilegeNotHeldException)
                {
                    throw new InvalidProjectFileException("Running unelevated so skipping this scenario.");
                }
                finally
                {
                    if (security != null)
                    {
                        security.RemoveAccessRule(rule);
                    }

                    File.Delete(solutionFile);
                    File.Delete(tempFileSentinel);
                    Assert.Equal(false, File.Exists(solutionFile));
                }
            }
           );
        }

        /// <summary>
        /// Build a project file that can't be accessed
        /// </summary>
        [Fact]
        public void ProjectCanNotBeOpened()
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                return; // FileSecurity class is not supported on Unix
            }

            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string projectFile = null;

                IdentityReference identity = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                FileSystemAccessRule rule = new FileSystemAccessRule(identity, FileSystemRights.Read, AccessControlType.Deny);

                FileSecurity security = null;

                try
                {
                    // Does not have .sln or .vcproj extension so loads as project
                    projectFile = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();

                    security = new FileSecurity(projectFile, System.Security.AccessControl.AccessControlSections.All);
                    security.AddAccessRule(rule);

                    File.SetAccessControl(projectFile, security);

                    ProjectRootElement p = ProjectRootElement.Open(projectFile);
                }
                catch (PrivilegeNotHeldException)
                {
                    throw new InvalidProjectFileException("Running unelevated so skipping the scenario.");
                }
                finally
                {
                    if (security != null)
                    {
                        security.RemoveAccessRule(rule);
                    }

                    File.Delete(projectFile);
                    Assert.Equal(false, File.Exists(projectFile));
                }
            }
           );
        }
#endif

        /// <summary>
        /// Build a corrupt solution
        /// </summary>
        [Fact]
        public void SolutionCorrupt()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string solutionFile = null;

                try
                {
                    solutionFile = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();

                    // Arbitrary corrupt content
                    string content = @"Microsoft Visual Studio Solution File, Format Version 10.00
# Visual Studio Codename Orcas
Project(""{";

                    File.WriteAllText(solutionFile, content);

                    ProjectRootElement p = ProjectRootElement.Open(solutionFile);
                }
                finally
                {
                    File.Delete(solutionFile);
                }
            }
           );
        }
        /// <summary>
        /// Open lots of projects concurrently to try to trigger problems
        /// </summary>
        [Fact]
        public void ConcurrentProjectOpenAndCloseThroughProject()
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                return; // TODO: This test hangs on Linux. Investigate
            }

            int iterations = 500;
            string[] paths = ObjectModelHelpers.GetTempFiles(iterations);

            try
            {
                Project[] projects = new Project[iterations];

                for (int i = 0; i < iterations; i++)
                {
                    CreatePREWithSubstantialContent().Save(paths[i]);
                }

                var collection = new ProjectCollection();
                int counter = 0;
                int remaining = iterations;
                var done = new ManualResetEvent(false);

                for (int i = 0; i < iterations; i++)
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        var current = Interlocked.Increment(ref counter) - 1;

                        projects[current] = collection.LoadProject(paths[current]);

                        if (Interlocked.Decrement(ref remaining) == 0)
                        {
                            done.Set();
                        }
                    });
                }

                done.WaitOne();

                Assert.Equal(iterations, collection.LoadedProjects.Count);

                counter = 0;
                remaining = iterations;
                done.Reset();

                for (int i = 0; i < iterations; i++)
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        var current = Interlocked.Increment(ref counter) - 1;

                        var pre = projects[current].Xml;
                        collection.UnloadProject(projects[current]);
                        collection.UnloadProject(pre);

                        if (Interlocked.Decrement(ref remaining) == 0)
                        {
                            done.Set();
                        }
                    });
                }

                done.WaitOne();

                Assert.Equal(0, collection.LoadedProjects.Count);
            }
            finally
            {
                for (int i = 0; i < iterations; i++)
                {
                    File.Delete(paths[i]);
                }
            }
        }

        /// <summary>
        /// Open lots of projects concurrently to try to trigger problems
        /// </summary>
        [Fact]
        public void ConcurrentProjectOpenAndCloseThroughProjectRootElement()
        {
            int iterations = 500;
            string[] paths = ObjectModelHelpers.GetTempFiles(iterations);

            try
            {
                var projects = new ProjectRootElement[iterations];

                var collection = new ProjectCollection();
                int counter = 0;
                int remaining = iterations;
                var done = new ManualResetEvent(false);

                for (int i = 0; i < iterations; i++)
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        var current = Interlocked.Increment(ref counter) - 1;

                        CreatePREWithSubstantialContent().Save(paths[current]);
                        projects[current] = ProjectRootElement.Open(paths[current], collection);

                        if (Interlocked.Decrement(ref remaining) == 0)
                        {
                            done.Set();
                        }
                    });
                }

                done.WaitOne();

                counter = 0;
                remaining = iterations;
                done.Reset();

                for (int i = 0; i < iterations; i++)
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        var current = Interlocked.Increment(ref counter) - 1;

                        collection.UnloadProject(projects[current]);

                        if (Interlocked.Decrement(ref remaining) == 0)
                        {
                            done.Set();
                        }
                    });
                }

                done.WaitOne();
            }
            finally
            {
                for (int i = 0; i < iterations; i++)
                {
                    File.Delete(paths[i]);
                }
            }
        }

        /// <summary>
        /// Tests DeepClone and CopyFrom for ProjectRootElements.
        /// </summary>
        [Fact]
        public void DeepClone()
        {
            var pre = ProjectRootElement.Create();
            var pg = pre.AddPropertyGroup();
            pg.AddProperty("a", "$(b)");
            pg.AddProperty("c", string.Empty);

            var ig = pre.AddItemGroup();
            var item = ig.AddItem("Foo", "boo$(hoo)");
            item.AddMetadata("Some", "Value");

            var target = pre.AddTarget("SomeTarget");
            target.Condition = "Some Condition";
            var task = target.AddTask("SomeTask");
            task.AddOutputItem("p1", "it");
            task.AddOutputProperty("prop", "it2");
            target.AppendChild(pre.CreateOnErrorElement("someTarget"));

            var idg = pre.AddItemDefinitionGroup();
            var id = idg.AddItemDefinition("SomeType");
            id.AddMetadata("sm", "sv");

            var ut = pre.AddUsingTask("name", "assembly", null);

            var inlineUt = pre.AddUsingTask("anotherName", "somefile", null);
            inlineUt.TaskFactory = "SomeFactory";
            var utb = inlineUt.AddUsingTaskBody("someEvaluate", "someTaskBody");

            var choose = pre.CreateChooseElement();
            pre.AppendChild(choose);
            var when1 = pre.CreateWhenElement("some condition");
            choose.AppendChild(when1);
            when1.AppendChild(pre.CreatePropertyGroupElement());
            var otherwise = pre.CreateOtherwiseElement();
            choose.AppendChild(otherwise);
            otherwise.AppendChild(pre.CreateItemGroupElement());

            var importGroup = pre.AddImportGroup();
            importGroup.AddImport("Some imported project");
            pre.AddImport("direct import");

            ValidateDeepCloneAndCopyFrom(pre);
        }

        /// <summary>
        /// Tests DeepClone and CopyFrom for ProjectRootElement that contain ProjectExtensions with text inside.
        /// </summary>
        [Fact]
        public void DeepCloneWithProjectExtensionsElementOfText()
        {
            var pre = ProjectRootElement.Create();

            var extensions = pre.CreateProjectExtensionsElement();
            extensions.Content = "Some foo content";
            pre.AppendChild(extensions);

            ValidateDeepCloneAndCopyFrom(pre);
        }

        /// <summary>
        /// Tests DeepClone and CopyFrom for ProjectRootElement that contain ProjectExtensions with xml inside.
        /// </summary>
        [Fact]
        public void DeepCloneWithProjectExtensionsElementOfXml()
        {
            var pre = ProjectRootElement.Create();

            var extensions = pre.CreateProjectExtensionsElement();
            extensions.Content = "<a><b/></a>";
            pre.AppendChild(extensions);

            ValidateDeepCloneAndCopyFrom(pre);
        }

        /// <summary>
        /// Test helper for validating that DeepClone and CopyFrom work as advertised.
        /// </summary>
        private static void ValidateDeepCloneAndCopyFrom(ProjectRootElement pre)
        {
            var pre2 = pre.DeepClone();
            Assert.NotSame(pre2, pre);
            Assert.Equal(pre.RawXml, pre2.RawXml);

            var pre3 = ProjectRootElement.Create();
            pre3.AddPropertyGroup(); // this should get wiped out in the DeepCopyFrom
            pre3.DeepCopyFrom(pre);
            Assert.Equal(pre.RawXml, pre3.RawXml);
        }

        /// <summary>
        /// Re-saves a project with a new encoding and thoroughly verifies that the right things happen.
        /// </summary>
        private void SaveProjectWithEncoding(string projectFullPath, Encoding encoding)
        {
            // Always use a new project collection to guarantee we're reading off disk.
            ProjectRootElement project = ProjectRootElement.Open(projectFullPath, new ProjectCollection());
            project.Save(encoding);
            Assert.Equal(encoding, project.Encoding); // "Changing an unmodified project's encoding failed to update ProjectRootElement.Encoding."

            // Try to verify that the xml declaration was emitted, and that the correct byte order marks
            // are also present.

            using (var reader = FileUtilities.OpenRead(projectFullPath, encoding, true))
            {
                Assert.Equal(encoding, reader.CurrentEncoding);
                string actual = reader.ReadLine();
                string expected = string.Format(@"<?xml version=""1.0"" encoding=""{0}""?>", encoding.WebName);
                Assert.Equal(expected, actual); // "The encoding was not emitted as an XML declaration."
            }

            project = ProjectRootElement.Open(projectFullPath, new ProjectCollection());

            // It's ok for the read Encoding to differ in fields like DecoderFallback,
            // so a pure equality check here is too much.
            Assert.Equal(encoding.CodePage, project.Encoding.CodePage);
            Assert.Equal(encoding.EncodingName, project.Encoding.EncodingName);
        }

        /// <summary>
        /// Creates a project at a given path with a given encoding but without the Xml declaration, 
        /// and then verifies that when loaded by MSBuild, the encoding is correctly reported.
        /// </summary>
        private void VerifyLoadedProjectHasEncoding(string projectFullPath, Encoding encoding)
        {
            CreateProjectWithEncodingWithoutDeclaration(projectFullPath, encoding);

            // Let's just be certain the project has been read off disk...
            ProjectRootElement project = ProjectRootElement.Open(projectFullPath, new ProjectCollection());
            Assert.Equal(encoding.WebName, project.Encoding.WebName);
        }

        /// <summary>
        /// Creates a project file with a specific encoding, but without an XML declaration.
        /// </summary>
        private void CreateProjectWithEncodingWithoutDeclaration(string projectFullPath, Encoding encoding)
        {
            const string EmptyProject = @"<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
</Project>";

            using (StreamWriter writer = FileUtilities.OpenWrite(projectFullPath, false, encoding))
            {
                writer.Write(ObjectModelHelpers.CleanupFileContents(EmptyProject));
            }
        }

        /// <summary>
        /// Create a nice big PRE
        /// </summary>
        private ProjectRootElement CreatePREWithSubstantialContent()
        {
            string content = ObjectModelHelpers.CleanupFileContents(
      @"<?xml version=""1.0"" encoding=""utf-16""?>
                    <Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                        <PropertyGroup Condition=""false"">
                            <p>p1</p>
                            <q>q1</q>
                        </PropertyGroup>
                        <PropertyGroup/>
                        <PropertyGroup>
                            <r>r1</r>
                        </PropertyGroup>
                        <Choose>
                            <When Condition=""true"">
                                <Choose>
                                    <When Condition=""true"">
                                        <PropertyGroup>
                                            <s>s1</s>
                                        </PropertyGroup>
                                    </When>
                                </Choose>
                            </When>
                            <When Condition=""false"">
                                <PropertyGroup>
                                    <s>s2</s> <!-- both esses -->
                                </PropertyGroup>
                            </When>
                            <Otherwise>
                                <Choose>
                                    <When Condition=""false""/>
                                    <Otherwise>
                                        <PropertyGroup>
                                            <t>t1</t>
                                        </PropertyGroup>
                                    </Otherwise>
                                </Choose>
                            </Otherwise>
                        </Choose>
                        <Import Project='$(MSBuildToolsPath)\microsoft.csharp.targets'/>
                    </Project>");

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            return project;
        }
    }
}
