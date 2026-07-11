// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Test the ProjectRootElement class
    /// </summary>
    [TestClass]
    public class ProjectRootElement_Tests
    {
        private const string SimpleProject =
@"<Project xmlns=`msbuildnamespace`>

  <PropertyGroup>
    <P>property value</P>
  </PropertyGroup>

  <PropertyGroup>
    <ProjectFile>$(MSBuildProjectFullPath)</ProjectFile>
    <ThisFile>$(MSBuildThisFileFullPath)</ThisFile>
  </PropertyGroup>

  <ItemGroup>
    <i Include=`a`>
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>";

        private const string ComplexProject =
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
    <PropertyGroup>
      <ProjectFile>$(MSBuildProjectFullPath)</ProjectFile>
      <ThisFile>$(MSBuildThisFileFullPath)</ThisFile>
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
    <Import Project='$(MSBuildToolsPath)\Microsoft.CSharp.targets'/>
</Project>";

        /// <summary>
        /// Empty project content
        /// </summary>
        [MSBuildTestMethod]
        public void EmptyProject()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            Assert.AreEqual(0, Helpers.Count(project.Children));
            Assert.AreEqual(string.Empty, project.DefaultTargets);
            Assert.AreEqual(string.Empty, project.InitialTargets);
            Assert.AreEqual(ObjectModelHelpers.MSBuildDefaultToolsVersion, project.ToolsVersion);
            Assert.IsTrue(project.HasUnsavedChanges); // it is indeed unsaved
        }

        /// <summary>
        /// Set default targets
        /// </summary>
        [MSBuildTestMethod]
        public void SetDefaultTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            project.DefaultTargets = "dt";
            Assert.AreEqual("dt", project.DefaultTargets);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set initialtargets
        /// </summary>
        [MSBuildTestMethod]
        public void SetInitialTargets()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            project.InitialTargets = "it";
            Assert.AreEqual("it", project.InitialTargets);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set toolsversion
        /// </summary>
        [MSBuildTestMethod]
        public void SetToolsVersion()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            project.ToolsVersion = "tv";
            Assert.AreEqual("tv", project.ToolsVersion);
            Assert.IsTrue(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Setting full path should accept and update relative path
        /// </summary>
        [MSBuildTestMethod]
        public void SetFullPath()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.FullPath = "X";

            Assert.AreEqual(project.FullPath, Path.Combine(Directory.GetCurrentDirectory(), "X"));
        }

        /// <summary>
        /// Attempting to load a second ProjectRootElement over the same file path simply
        /// returns the first one.
        /// A ProjectRootElement is notionally a "memory mapped" view of a file, and we assume there is only
        /// one per file path, so we must reject attempts to make another.
        /// </summary>
        [MSBuildTestMethod]
        public void ConstructOverSameFileReturnsSame()
        {
            ProjectRootElement projectXml1 = ProjectRootElement.Create();
            projectXml1.Save(FileUtilities.GetTemporaryFileName());

            ProjectRootElement projectXml2 = ProjectRootElement.Open(projectXml1.FullPath);

            Assert.IsTrue(object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Attempting to load a second ProjectRootElement over the same file path simply
        /// returns the first one. This should work even if one of the paths is not a full path.
        /// </summary>
        [MSBuildTestMethod]
        public void ConstructOverSameFileReturnsSameEvenWithOneBeingRelativePath()
        {
            ProjectRootElement projectXml1 = ProjectRootElement.Create();

            projectXml1.FullPath = Path.Combine(Directory.GetCurrentDirectory(), @"xyz\abc");

            ProjectRootElement projectXml2 = ProjectRootElement.Open(@"xyz\abc");

            Assert.IsTrue(object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Attempting to load a second ProjectRootElement over the same file path simply
        /// returns the first one. This should work even if one of the paths is not a full path.
        /// </summary>
        [MSBuildTestMethod]
        public void ConstructOverSameFileReturnsSameEvenWithOneBeingRelativePath2()
        {
            ProjectRootElement projectXml1 = ProjectRootElement.Create();

            projectXml1.FullPath = @"xyz\abc";

            ProjectRootElement projectXml2 = ProjectRootElement.Open(Path.Combine(Directory.GetCurrentDirectory(), @"xyz\abc"));

            Assert.IsTrue(object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Using TextReader
        /// </summary>
        [MSBuildTestMethod]
        public void ConstructOverSameFileReturnsSameEvenWithOneBeingRelativePath3()
        {
            string content = "<Project ToolsVersion=\"4.0\">\r\n</Project>";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement projectXml1 = projectRootElementFromString.Project;


            projectXml1.FullPath = @"xyz\abc";

            ProjectRootElement projectXml2 = ProjectRootElement.Open(Path.Combine(Directory.GetCurrentDirectory(), @"xyz\abc"));

            Assert.IsTrue(object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Using TextReader
        /// </summary>
        [MSBuildTestMethod]
        public void ConstructOverSameFileReturnsSameEvenWithOneBeingRelativePath4()
        {
            string content = "<Project ToolsVersion=\"4.0\">\r\n</Project>";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement projectXml1 = projectRootElementFromString.Project;


            projectXml1.FullPath = Path.Combine(Directory.GetCurrentDirectory(), @"xyz\abc");

            ProjectRootElement projectXml2 = ProjectRootElement.Open(@"xyz\abc");

            Assert.IsTrue(object.ReferenceEquals(projectXml1, projectXml2));
        }

        /// <summary>
        /// Two ProjectRootElement's over the same file path does not throw (although you shouldn't do it)
        /// </summary>
        [MSBuildTestMethod]
        public void SetFullPathProjectXmlAlreadyLoaded()
        {
            ProjectRootElement projectXml1 = ProjectRootElement.Create();
            projectXml1.FullPath = FileUtilities.GetTemporaryFile();

            ProjectRootElement projectXml2 = ProjectRootElement.Create();
            projectXml2.FullPath = projectXml1.FullPath;
        }

        /// <summary>
        /// Invalid XML
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidXml()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                ProjectRootElement.Create(XmlReader.Create(new StringReader("XXX")));
            });
        }

        /// <summary>
        /// Valid Xml, invalid namespace on the root
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidNamespace()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                var content = @"<Project xmlns='XXX'/>";
                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }

        /// <summary>
        /// Invalid root tag
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidRootTag()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <XXX />
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Valid Xml, invalid syntax below the root
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidChildBelowRoot()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <XXX/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Root indicates upgrade needed
        /// </summary>
        [MSBuildTestMethod]
        public void NeedsUpgrade()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <VisualStudioProject/>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Valid Xml, invalid namespace below the root
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidNamespaceBelowRoot()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <PropertyGroup xmlns='XXX'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Tests that the namespace error reports are correct
        /// </summary>
        [MSBuildTestMethod]
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
            bool exceptionThrown = false;
            try
            {
                using ProjectRootElementFromString projectRootElementFromString = new(content);
                ProjectRootElement project = projectRootElementFromString.Project;
            }
            catch (InvalidProjectFileException ex)
            {
                exceptionThrown = true;

                // MSB4068: The element <msb:Project> is unrecognized, or not supported in this context.
                Assert.AreNotEqual("MSB4068", ex.ErrorCode);

                // MSB4041: The default XML namespace of the project must be the MSBuild XML namespace.
                Assert.AreEqual("MSB4041", ex.ErrorCode);
            }

            Assert.IsTrue(exceptionThrown); // "ERROR: An invalid project file exception should have been thrown."
        }

        /// <summary>
        /// Valid Xml, invalid syntax thrown by child element parsing
        /// </summary>
        [MSBuildTestMethod]
        public void ValidXmlInvalidSyntaxInChildElement()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ItemGroup>
                           <XXX YYY='ZZZ'/>
                        </ItemGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Valid Xml, invalid syntax, should not get added to the Xml cache and
        /// thus returned on the second request!
        /// </summary>
        [MSBuildTestMethod]
        public void ValidXmlInvalidSyntaxOpenFromDiskTwice()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
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
                        path = FileUtilities.GetTemporaryFileName();
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
            });
        }
        /// <summary>
        /// Verify that opening project using XmlTextReader does not add it to the Xml cache
        /// </summary>
        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void ValidXmlXmlTextReaderNotCache()
        {
            string content = @"<Project />";

            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFileName();
                File.WriteAllText(path, content);

                var reader1 = XmlReader.Create(path);
                ProjectRootElement root1 = ProjectRootElement.Create(reader1);
                root1.AddItem("type", "include");

                // If it's in the cache, then the 2nd document won't see the add.
                var reader2 = XmlReader.Create(path);
                ProjectRootElement root2 = ProjectRootElement.Create(reader2);

                Assert.ContainsSingle(root1.Items);
                Assert.IsEmpty(root2.Items);

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
        [MSBuildTestMethod]
        public void ValidXmlXmlReaderCache()
        {
            string content = @"<Project />";

            string content2 = @"<Project DefaultTargets='t' />";

            string path = null;

            try
            {
                path = FileUtilities.GetTemporaryFileName();
                File.WriteAllText(path, content);

                ProjectRootElement root1 = ProjectRootElement.Create(path);

                File.WriteAllText(path, content2);

                // If it went in the cache, and this path also reads from the cache,
                // then we'll see the first version of the file.
                ProjectRootElement root2 = ProjectRootElement.Create(path);

                Assert.AreEqual(string.Empty, root1.DefaultTargets);
                Assert.AreEqual(string.Empty, root2.DefaultTargets);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// A simple "system" test: load microsoft.*.targets and verify we don't throw
        /// </summary>
        [MSBuildTestMethod]
        public void LoadCommonTargets()
        {
            using ProjectCollection projectCollection = new ProjectCollection();
            string toolsPath = projectCollection.Toolsets.Where(toolset => (string.Equals(toolset.ToolsVersion, ObjectModelHelpers.MSBuildDefaultToolsVersion, StringComparison.OrdinalIgnoreCase))).First().ToolsPath;

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
        [MSBuildTestMethod]
        public void InvalidSaveWithoutFullPath()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                XmlReader reader = XmlReader.Create(new StringReader("<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"/>"));
                ProjectRootElement project = ProjectRootElement.Create(reader);

                project.Save();
            });
        }
        /// <summary>
        /// Save content with transforms.
        /// The ">" should not turn into "&lt;"
        /// </summary>
        [MSBuildTestMethod]
        public void SaveWithTransforms()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "@(h->'%(x)')");

            StringBuilder builder = new StringBuilder();
            using StringWriter writer = new StringWriter(builder);

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
        [MSBuildTestMethod]
        public void SaveWithTransformsToFile()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "@(h->'%(x)')");

            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFileName();

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
        [MSBuildTestMethod]
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

                Assert.IsTrue(File.Exists(path));
                Assert.AreEqual(path, project.FullPath);
                Assert.AreEqual(directory, project.DirectoryPath);
            }
            finally
            {
                FileUtilities.DeleteWithoutTrailingBackslash(directory, true);
            }
        }

        /// <summary>
        /// Save should create a directory if it is missing
        /// </summary>
        [MSBuildTestMethod]
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

                Assert.IsTrue(File.Exists(file));
                Assert.AreEqual(path, project.FullPath);
                Assert.AreEqual(directory, project.DirectoryPath);
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
        [MSBuildTestMethod]
        public void SaveUnnamedProject()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                project.Save();
            });
        }
        /// <summary>
        /// Verifies that the ProjectRootElement.Encoding property getter returns values
        /// that are based on the XML declaration in the file.
        /// </summary>
        [MSBuildTestMethod]
        public void EncodingGetterBasedOnXmlDeclaration()
        {
            using ProjectRootElementFromString projectRootElementFromStringUTF16 = new(ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-16""?>
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
</Project>"));
            ProjectRootElement project = projectRootElementFromStringUTF16.Project;
            Assert.AreEqual(Encoding.Unicode, project.Encoding);

            using ProjectRootElementFromString projectRootElementFromStringUTF8 = new(ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
</Project>"));
            project = projectRootElementFromStringUTF8.Project;
            Assert.AreEqual(Encoding.UTF8, project.Encoding);

            using ProjectRootElementFromString projectRootElementFromStringASCII = new(ObjectModelHelpers.CleanupFileContents(@"<?xml version=""1.0"" encoding=""us-ascii""?>
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
</Project>"));
            project = projectRootElementFromStringASCII.Project;
            Assert.AreEqual(Encoding.ASCII, project.Encoding);
        }

        /// <summary>
        /// Verifies that ProjectRootElement.Encoding returns the correct value
        /// after reading a file off disk, even if no xml declaration is present.
        /// </summary>
        [MSBuildTestMethod]
        public void EncodingGetterBasedOnActualEncodingWhenXmlDeclarationIsAbsent()
        {
            string projectFullPath = FileUtilities.GetTemporaryFileName();
            try
            {
                VerifyLoadedProjectHasEncoding(projectFullPath, Encoding.UTF8);
                VerifyLoadedProjectHasEncoding(projectFullPath, Encoding.Unicode);

                // We don't test ASCII, since there is no byte order mark for it,
                // and the XmlReader will legitimately decide to interpret it as UTF8,
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
        [MSBuildTestMethod]
        public void SaveUnmodifiedWithNewEncoding()
        {
            using ProjectRootElementFromString projectRootElementFromString = new(ObjectModelHelpers.CleanupFileContents(@"
<Project DefaultTargets=""Build"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
</Project>"));
            ProjectRootElement project = projectRootElementFromString.Project;

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
        [MSBuildTestMethod]
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

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;

            List<ProjectPropertyElement> properties = Helpers.MakeList(project.Properties);

            Assert.AreEqual(6, properties.Count);

            Assert.AreEqual("q", properties[1].Name);
            Assert.AreEqual("r1", properties[2].Value);
            Assert.AreEqual("t1", properties[5].Value);
        }

        /// <summary>
        /// Enumerate over all items from the project directly.
        /// It should traverse into Choose's.
        /// </summary>
        [MSBuildTestMethod]
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

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;

            List<ProjectItemElement> items = Helpers.MakeList(project.Items);

            Assert.AreEqual(6, items.Count);

            Assert.AreEqual("j", items[1].ItemType);
            Assert.AreEqual("k1", items[2].Include);
            Assert.AreEqual("k4", items[5].Include);
        }

#if FEATURE_SECURITY_PRINCIPAL_WINDOWS
        /// <summary>
        /// Build a solution file that can't be accessed.
        /// </summary>
        [WindowsOnlyFact(additionalMessage: "Security classes are not supported on Unix.")]
        public void SolutionCanNotBeOpened()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string solutionFile = null;
                string tempFileSentinel = null;

                IdentityReference identity = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                FileSystemAccessRule rule = new FileSystemAccessRule(identity, FileSystemRights.Read, AccessControlType.Deny);

                FileSecurity security = null;

                try
                {
                    tempFileSentinel = FileUtilities.GetTemporaryFile();
                    solutionFile = Path.ChangeExtension(tempFileSentinel, ".sln");
                    File.Copy(tempFileSentinel, solutionFile);

                    security = new FileSecurity(solutionFile, System.Security.AccessControl.AccessControlSections.All);

                    security.AddAccessRule(rule);

                    File.SetAccessControl(solutionFile, security);

                    ProjectRootElement.Open(solutionFile);
                }
                catch (PrivilegeNotHeldException)
                {
                    throw new InvalidProjectFileException("Running unelevated so skipping this scenario.");
                }
                finally
                {
                    security?.RemoveAccessRule(rule);

                    File.Delete(solutionFile);
                    File.Delete(tempFileSentinel);
                    Assert.IsFalse(File.Exists(solutionFile));
                }
            });
        }

        /// <summary>
        /// Build a project file that can't be accessed.
        /// </summary>
        [WindowsOnlyFact(additionalMessage: "FileSecurity class is not supported on Unix.")]
        public void ProjectCanNotBeOpened()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string projectFile = null;

                IdentityReference identity = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                FileSystemAccessRule rule = new FileSystemAccessRule(identity, FileSystemRights.Read, AccessControlType.Deny);

                FileSecurity security = null;

                try
                {
                    // Does not have .sln or .vcproj extension so loads as project
                    projectFile = FileUtilities.GetTemporaryFile();

                    security = new FileSecurity(projectFile, System.Security.AccessControl.AccessControlSections.All);
                    security.AddAccessRule(rule);

                    File.SetAccessControl(projectFile, security);

                    ProjectRootElement.Open(projectFile);
                }
                catch (PrivilegeNotHeldException)
                {
                    throw new InvalidProjectFileException("Running unelevated so skipping the scenario.");
                }
                finally
                {
                    security?.RemoveAccessRule(rule);

                    File.Delete(projectFile);
                    Assert.IsFalse(File.Exists(projectFile));
                }
            });
        }
#endif

        /// <summary>
        /// Build a corrupt solution
        /// </summary>
        [MSBuildTestMethod]
        public void SolutionCorrupt()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string solutionFile = null;

                try
                {
                    solutionFile = FileUtilities.GetTemporaryFileName();

                    // Arbitrary corrupt content
                    string content = @"Microsoft Visual Studio Solution File, Format Version 10.00
# Visual Studio Codename Orcas
Project(""{";

                    File.WriteAllText(solutionFile, content);

                    ProjectRootElement.Open(solutionFile);
                }
                finally
                {
                    File.Delete(solutionFile);
                }
            });
        }
        /// <summary>
        /// Open lots of projects concurrently to try to trigger problems
        /// </summary>
        [WindowsOnlyFact]
        public void ConcurrentProjectOpenAndCloseThroughProject()
        {
            int iterations = 500;
            string[] paths = ObjectModelHelpers.GetTempFiles(iterations);

            try
            {
                Project[] projects = new Project[iterations];

                for (int i = 0; i < iterations; i++)
                {
                    CreatePREWithSubstantialContent().Save(paths[i]);
                }

                using var collection = new ProjectCollection();
                int counter = 0;
                int remaining = iterations;
                using var done = new ManualResetEvent(false);

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

                Assert.AreEqual(iterations, collection.LoadedProjects.Count);

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

                Assert.IsEmpty(collection.LoadedProjects);
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
        [MSBuildTestMethod]
        public void ConcurrentProjectOpenAndCloseThroughProjectRootElement()
        {
            int iterations = 500;
            string[] paths = ObjectModelHelpers.GetTempFiles(iterations);

            try
            {
                var projects = new ProjectRootElement[iterations];

                using var collection = new ProjectCollection();
                int counter = 0;
                int remaining = iterations;
                using var done = new ManualResetEvent(false);

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
        [MSBuildTestMethod]
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

            pre.AddUsingTask("name", "assembly", null);

            var inlineUt = pre.AddUsingTask("anotherName", "somefile", null);
            inlineUt.TaskFactory = "SomeFactory";
            inlineUt.AddUsingTaskBody("someEvaluate", "someTaskBody");

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
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void DeepCloneWithProjectExtensionsElementOfXml()
        {
            var pre = ProjectRootElement.Create();

            var extensions = pre.CreateProjectExtensionsElement();
            extensions.Content = "<a><b/></a>";
            pre.AppendChild(extensions);

            ValidateDeepCloneAndCopyFrom(pre);
        }

        /// <summary>
        /// Tests DeepClone and CopyFrom when there is metadata expressed as attributes
        /// </summary>
        [MSBuildTestMethod]
        public void DeepCloneWithMetadataAsAttributes()
        {
            var project =
@"<?xml version=`1.0` encoding=`utf-8`?>
  <Project xmlns = 'msbuildnamespace'>

    <ItemDefinitionGroup>
      <Compile A=`a`/>
      <B M1=`dv1`>
        <M2>dv2</M2>
      </B>
      <C/>
    </ItemDefinitionGroup>

    <ItemGroup>
      <Compile Include=`Class1.cs` A=`a` />
      <Compile Include=`Class2.cs` />
    </ItemGroup>

    <PropertyGroup>
      <P>val</P>
    </PropertyGroup>

    <Target Name=`Build`>
      <ItemGroup>
        <A Include=`a` />
        <B Include=`b` M1=`v1`>
          <M2>v2</M2>
        </B>
        <C Include=`c`/>
      </ItemGroup>
    </Target>

  </Project>";

            using ProjectRootElementFromString projectRootElementFromString = new(ObjectModelHelpers.CleanupFileContents(project));
            ProjectRootElement pre = projectRootElementFromString.Project;

            ValidateDeepCloneAndCopyFrom(pre);
        }

        /// <summary>
        /// Tests TryOpen when preserveFormatting is the same and different than the cached project.
        /// </summary>
        [MSBuildTestMethod]
        public void TryOpenWithPreserveFormatting()
        {
            string project =
@"<?xml version=`1.0` encoding=`utf-8`?>
<Project xmlns = 'msbuildnamespace'>
</Project>";

            using (var env = TestEnvironment.Create())
            using (var projectCollection = new ProjectCollection())
            {
                var projectFiles = env.CreateTestProjectWithFiles("", new[] { "build.proj" });
                var projectFile = projectFiles.CreatedFiles.First();
                using ProjectRootElementFromString projectRootElementFromString = new(
                ObjectModelHelpers.CleanupFileContents(project),
                projectCollection,
                preserveFormatting: true);
                ProjectRootElement projectXml = projectRootElementFromString.Project;

                projectXml.Save(projectFile);

                var xml0 = ProjectRootElement.TryOpen(projectXml.FullPath, projectCollection, preserveFormatting: true);
                Assert.IsTrue(xml0.PreserveFormatting);

                var xml1 = ProjectRootElement.TryOpen(projectXml.FullPath, projectCollection, preserveFormatting: false);
                Assert.IsFalse(xml1.PreserveFormatting);

                var xml2 = ProjectRootElement.TryOpen(projectXml.FullPath, projectCollection, preserveFormatting: null);
                // reuses existing setting
                Assert.IsFalse(xml2.PreserveFormatting);

                Assert.IsNotNull(xml0);

                Assert.AreSame(xml0, xml1);
                Assert.AreSame(xml0, xml2);
            }
        }

        [MSBuildTestMethod]
        [DataRow(true, false, false)]
        [DataRow(true, true, true)]
        [DataRow(false, false, false)]
        [DataRow(false, true, true)]
        [DataRow(true, null, true)]
        [DataRow(false, null, false)]
        public void ReloadCanSpecifyPreserveFormatting(bool initialPreserveFormatting, bool? reloadShouldPreserveFormatting, bool expectedFormattingAfterReload)
        {
            using (var env = TestEnvironment.Create())
            {
                var testFiles = env.CreateTestProjectWithFiles("", new[] { "build.proj" });
                var projectFile = testFiles.CreatedFiles.First();

                var projectElement = ObjectModelHelpers.CreateInMemoryProjectRootElement(SimpleProject, null, initialPreserveFormatting);
                projectElement.Save(projectFile);
                Assert.AreEqual(initialPreserveFormatting, projectElement.PreserveFormatting);

                projectElement.Reload(false, reloadShouldPreserveFormatting);
                Assert.AreEqual(expectedFormattingAfterReload, projectElement.PreserveFormatting);

                // reset project to original preserve formatting
                projectElement.Reload(false, initialPreserveFormatting);
                Assert.AreEqual(initialPreserveFormatting, projectElement.PreserveFormatting);

                using var xmlReader = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(SimpleProject)));
                projectElement.ReloadFrom(xmlReader, false, reloadShouldPreserveFormatting);
                Assert.AreEqual(expectedFormattingAfterReload, projectElement.PreserveFormatting);

                // reset project to original preserve formatting
                projectElement.Reload(false, initialPreserveFormatting);
                Assert.AreEqual(initialPreserveFormatting, projectElement.PreserveFormatting);

                projectElement.ReloadFrom(projectFile, false, reloadShouldPreserveFormatting);
                Assert.AreEqual(expectedFormattingAfterReload, projectElement.PreserveFormatting);
            }
        }

        [MSBuildTestMethod]

        // same content should still dirty the project
        [DataRow(
SimpleProject,
SimpleProject,
true, false, false)]

        // new comment
        [DataRow(
@"<Project xmlns=`msbuildnamespace`>

  <PropertyGroup>
    <P>property value</P>
  </PropertyGroup>

  <ItemGroup>
    <i Include=`a`>
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>",
@"
<!-- new comment -->
<Project xmlns=`msbuildnamespace`>
  <PropertyGroup>
    <P><!-- new comment -->property value<!-- new comment --></P>
  </PropertyGroup>

  <!-- new comment -->
  <ItemGroup>
    <i Include=`a`>
      <!-- new comment -->
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>",
true, false, true)]

        // changed comment
        [DataRow(
@"
<!-- new comment -->
<Project xmlns=`msbuildnamespace`>
  <PropertyGroup>
    <P><!-- new comment -->property value<!-- new comment --></P>
  </PropertyGroup>

  <!-- new comment -->
  <ItemGroup>
    <i Include=`a`>
      <!-- new comment -->
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>",
@"
<!-- changed comment -->
<Project xmlns=`msbuildnamespace`>
  <PropertyGroup>
    <P><!-- changed comment -->property value<!-- changed comment --></P>
  </PropertyGroup>

  <!-- changed comment -->
  <ItemGroup>
    <i Include=`a`>
      <!-- changed comment -->
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>",
true, false, true)]

        // deleted comment
        [DataRow(
@"
<!-- new comment -->
<Project xmlns=`msbuildnamespace`>
  <PropertyGroup>
    <P><!-- new comment -->property value<!-- new comment --></P>
  </PropertyGroup>

  <!-- new comment -->
  <ItemGroup>
    <i Include=`a`>
      <!-- new comment -->
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>",
@"
<Project xmlns=`msbuildnamespace`>
  <PropertyGroup>
    <P>property value</P>
  </PropertyGroup>

  <ItemGroup>
    <i Include=`a`>
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>",
true, false, true)]

        // new comments and changed code
        [DataRow(
@"<Project xmlns=`msbuildnamespace`>

  <PropertyGroup>
    <P>property value</P>
  </PropertyGroup>

  <ItemGroup>
    <i Include=`a`>
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>",
@"
<!-- new comment -->
<Project xmlns=`msbuildnamespace`>
  <PropertyGroup>
    <P>property value</P>
    <P2><!-- new comment -->property value<!-- new comment --></P2>
  </PropertyGroup>

</Project>",
true, true, true)]

        // commented out code
        [DataRow(
@"<Project xmlns=`msbuildnamespace`>

  <PropertyGroup>
    <P>property value</P>
  </PropertyGroup>

  <ItemGroup>
    <i Include=`a`>
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>",
@"<Project xmlns=`msbuildnamespace`>
  <PropertyGroup>
    <P>property value</P>
  </PropertyGroup>

<!--
  <ItemGroup>
    <i Include=`a`>
      <m>metadata value</m>
    </i>
  </ItemGroup>
-->
</Project>",
true, true, true)]
        public void ReloadRespectsNewContents(string initialProjectContents, string changedProjectContents, bool versionChanged, bool msbuildChildrenChanged, bool xmlChanged)
        {
            Action<ProjectRootElement, string> act = (p, c) =>
            {
                p.ReloadFrom(
                    XmlReader.Create(new StringReader(c)),
                    throwIfUnsavedChanges: false);
            };

            AssertReload(initialProjectContents, changedProjectContents, versionChanged, msbuildChildrenChanged, xmlChanged, act);
        }

        [MSBuildTestMethod]
        public void ReloadedStateIsResilientToChangesAndDiskRoundtrip()
        {
            var initialProjectContents = ObjectModelHelpers.CleanupFileContents(
@"
<!-- new comment -->
<Project xmlns=`msbuildnamespace`>

  <!-- new comment -->
  <ItemGroup>
    <i Include=`a`>
      <!-- new comment -->
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>");

            var changedProjectContents1 = ObjectModelHelpers.CleanupFileContents(
@"
<!-- changed comment -->
<Project xmlns=`msbuildnamespace`>

  <!-- changed comment -->
  <ItemGroup>

    <i Include=`a`>
         <!-- changed comment -->
           <m>metadata value</m>
    </i>

  </ItemGroup>

</Project>");

            var changedProjectContents2 = ObjectModelHelpers.CleanupFileContents(
// spurious comment placement issue: https://github.com/dotnet/msbuild/issues/1503
@"
<!-- changed comment -->
<Project xmlns=`msbuildnamespace`>

  <!-- changed comment -->
  <PropertyGroup>
    <P>v</P>
  </PropertyGroup>
  <ItemGroup>

    <i Include=`a`>
         <!-- changed comment -->
           <m>metadata value</m>
    </i>

  </ItemGroup>

</Project>");

            using (var env = TestEnvironment.Create())
            {
                var projectCollection1 = env.CreateProjectCollection().Collection;
                var projectCollection2 = env.CreateProjectCollection().Collection;

                var testFiles = env.CreateTestProjectWithFiles("", new[] { "build.proj" });
                var projectPath = testFiles.CreatedFiles.First();

                var projectElement = ObjectModelHelpers.CreateInMemoryProjectRootElement(initialProjectContents, projectCollection1, preserveFormatting: true);
                projectElement.Save(projectPath);

                using var xmlReader = XmlReader.Create(new StringReader(changedProjectContents1));
                projectElement.ReloadFrom(xmlReader);

                VerifyAssertLineByLine(changedProjectContents1, projectElement.RawXml);

                projectElement.AddProperty("P", "v");

                VerifyAssertLineByLine(changedProjectContents2, projectElement.RawXml);

                projectElement.Save();

                var projectElement2 = ProjectRootElement.Open(projectPath, projectCollection2, preserveFormatting: true);

                VerifyAssertLineByLine(changedProjectContents2, projectElement2.RawXml);
            }
        }

        [MSBuildTestMethod]
        public void ReloadThrowsOnInvalidXmlSyntax()
        {
            var missingClosingTag =
@"<Project xmlns=`msbuildnamespace`>

  <PropertyGroup>
    <P>property value</P>
  </PropertyGroup>

  <ItemGroup>
    <i Include=`a`>
      <m>metadata value<m>
    </i>
  </ItemGroup>

</Project>";

            Action<ProjectRootElement, string> act = (p, c) =>
            {
                var exception =
                    Assert.ThrowsExactly<InvalidProjectFileException>(
                        () =>
                            p.ReloadFrom(
                                XmlReader.Create(
                                    new StringReader(c)),
                                throwIfUnsavedChanges: false));

                Assert.Contains(@"The project file could not be loaded. The 'm' start tag on line", exception.Message);
            };

            // reload does not mutate the project element
            AssertReload(SimpleProject, missingClosingTag, false, false, false, act);
        }

        [MSBuildTestMethod]
        public void ReloadThrowsForInMemoryProjectsWithoutAPath()
        {
            Action<ProjectRootElement, string> act = (p, c) =>
            {
                var exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () =>
                            p.Reload(throwIfUnsavedChanges: false));

                Assert.Contains(@"Value not set:", exception.Message);
            };

            // reload does not mutate the project element
            AssertReload(SimpleProject, ComplexProject, false, false, false, act);
        }

        [MSBuildTestMethod]
        public void ReloadFromAPathThrowsOnMissingPath()
        {
            Action<ProjectRootElement, string> act = (p, c) =>
            {
                var fullPath = Path.GetFullPath("foo");

                var exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () =>
                            p.ReloadFrom(fullPath, throwIfUnsavedChanges: false));

                Assert.Contains(@"File to reload from does not exist:", exception.Message);
            };

            // reload does not mutate the project element
            AssertReload(SimpleProject, ComplexProject, false, false, false, act);
        }

        [MSBuildTestMethod]
        public void ReloadFromMemoryWhenProjectIsInMemoryKeepsProjectFileEmpty()
        {
            AssertProjectFileAfterReload(
                true,
                true,
                (initial, reload, actualFile) => { Assert.AreEqual(String.Empty, actualFile); });
        }

        [MSBuildTestMethod]
        public void ReloadFromMemoryWhenProjectIsInFileKeepsProjectFile()
        {
            AssertProjectFileAfterReload(
                false,
                true,
                (initial, reload, actualFile) => { Assert.AreEqual(initial, actualFile); });
        }

        [MSBuildTestMethod]
        public void ReloadFromFileWhenProjectIsInMemorySetsProjectFile()
        {
            AssertProjectFileAfterReload(
                true,
                false,
                (initial, reload, actualFile) => { Assert.AreEqual(reload, actualFile); });
        }

        [MSBuildTestMethod]
        public void ReloadFromFileWhenProjectIsInFileUpdatesProjectFile()
        {
            AssertProjectFileAfterReload(
                false,
                false,
                (initial, reload, actualFile) => { Assert.AreEqual(reload, actualFile); });
        }

        private void AssertProjectFileAfterReload(
            bool initialProjectFromMemory,
            bool reloadProjectFromMemory,
            Action<string, string, string> projectFileAssert)
        {
            using (var env = TestEnvironment.Create())
            {
                var projectCollection = env.CreateProjectCollection().Collection;

                string initialLocation = null;
                ProjectRootElement rootElement = null;

                if (initialProjectFromMemory)
                {
                    rootElement = ObjectModelHelpers.CreateInMemoryProjectRootElement(SimpleProject, projectCollection);
                }
                else
                {
                    initialLocation = env.CreateFile().Path;
                    File.WriteAllText(initialLocation, ObjectModelHelpers.CleanupFileContents(SimpleProject));
                    rootElement = ProjectRootElement.Open(initialLocation, projectCollection);
                }

                var project = new Project(rootElement, null, null, projectCollection);

                string reloadLocation = null;

                if (reloadProjectFromMemory)
                {
                    using var xmlReader = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(ComplexProject)));
                    rootElement.ReloadFrom(xmlReader, false);
                }
                else
                {
                    reloadLocation = env.CreateFile().Path;
                    File.WriteAllText(reloadLocation, ObjectModelHelpers.CleanupFileContents(ComplexProject));
                    rootElement.ReloadFrom(reloadLocation, false);
                }

                project.ReevaluateIfNecessary();

                projectFileAssert.Invoke(initialLocation, reloadLocation, EmptyIfNull(rootElement.FullPath));
                projectFileAssert.Invoke(initialLocation, reloadLocation, rootElement.ProjectFileLocation.File);
                projectFileAssert.Invoke(initialLocation, reloadLocation, rootElement.AllChildren.Last().Location.File);
                projectFileAssert.Invoke(initialLocation, reloadLocation, project.GetPropertyValue("ProjectFile"));
                projectFileAssert.Invoke(initialLocation, reloadLocation, project.GetPropertyValue("ThisFile"));

                if (initialProjectFromMemory && reloadProjectFromMemory)
                {
                    Assert.AreEqual(Environment.CurrentDirectory, rootElement.DirectoryPath);
                }
                else
                {
                    projectFileAssert.Invoke(Path.GetDirectoryName(initialLocation), Path.GetDirectoryName(reloadLocation), rootElement.DirectoryPath);
                }
            }
        }

        private static string EmptyIfNull(string aString) => aString ?? string.Empty;

        [MSBuildTestMethod]
        public void ReloadThrowsOnInvalidMsBuildSyntax()
        {
            var unknownAttribute =
@"<Project xmlns=`msbuildnamespace`>

  <PropertyGroup Foo=`bar`>
    <P>property value</P>
  </PropertyGroup>

  <ItemGroup>
    <i Include=`a`>
      <m>metadata value</m>
    </i>
  </ItemGroup>

</Project>";

            Action<ProjectRootElement, string> act = (p, c) =>
            {
                var exception =
                    Assert.ThrowsExactly<InvalidProjectFileException>(
                        () =>
                            p.ReloadFrom(
                                XmlReader.Create(
                                    new StringReader(c)),
                                throwIfUnsavedChanges: false));

                Assert.Contains(@"The attribute ""Foo"" in element <PropertyGroup> is unrecognized", exception.Message);
            };

            // reload does not mutate the project element
            AssertReload(SimpleProject, unknownAttribute, false, false, false, act);
        }

        [MSBuildTestMethod]
        public void ReloadThrowsByDefaultIfThereAreUnsavedChanges()
        {
            Action<ProjectRootElement, string> act = (p, c) =>
            {
                var exception =
                    Assert.ThrowsExactly<InvalidOperationException>(
                        () =>
                            p.ReloadFrom(
                                XmlReader.Create(
                                    new StringReader(c))));

                Assert.Contains(@"ProjectRootElement can't reload if it contains unsaved changes.", exception.Message);
            };

            // reload does not mutate the project element
            AssertReload(SimpleProject, ComplexProject, false, false, false, act);
        }

        [MSBuildTestMethod]
        public void ReloadCanOverwriteUnsavedChanges()
        {
            Action<ProjectRootElement, string> act = (p, c) =>
            {
                p.ReloadFrom(
                    XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(c))),
                    throwIfUnsavedChanges: false);
            };

            AssertReload(SimpleProject, ComplexProject, true, true, true, act);
        }

        private void AssertReload(
            string initialContents,
            string changedContents,
            bool versionChanged,
            bool msbuildChildrenChanged,
            bool xmlChanged,
            Action<ProjectRootElement, string> act)
        {
            var projectElement = ObjectModelHelpers.CreateInMemoryProjectRootElement(initialContents);

            var version = projectElement.Version;
            var childrenCount = projectElement.AllChildren.Count();
            var xml = projectElement.RawXml;

            Assert.IsTrue(projectElement.HasUnsavedChanges);
            act(projectElement, ObjectModelHelpers.CleanupFileContents(changedContents));

            if (versionChanged)
            {
                Assert.AreNotEqual(version, projectElement.Version);
            }
            else
            {
                Assert.AreEqual(version, projectElement.Version);
            }

            if (msbuildChildrenChanged)
            {
                Assert.AreNotEqual(childrenCount, projectElement.AllChildren.Count());
            }
            else
            {
                Assert.AreEqual(childrenCount, projectElement.AllChildren.Count());
            }

            if (xmlChanged)
            {
                Assert.AreNotEqual(xml, projectElement.RawXml);
            }
            else
            {
                Assert.AreEqual(xml, projectElement.RawXml);
            }
        }

        /// <summary>
        /// Test helper for validating that DeepClone and CopyFrom work as advertised.
        /// </summary>
        private static void ValidateDeepCloneAndCopyFrom(ProjectRootElement pre)
        {
            var pre2 = pre.DeepClone();
            Assert.AreNotSame(pre2, pre);
            Assert.AreEqual(pre.RawXml, pre2.RawXml);

            var pre3 = ProjectRootElement.Create();
            pre3.AddPropertyGroup(); // this should get wiped out in the DeepCopyFrom
            pre3.DeepCopyFrom(pre);
            Assert.AreEqual(pre.RawXml, pre3.RawXml);
        }

        /// <summary>
        /// Re-saves a project with a new encoding and thoroughly verifies that the right things happen.
        /// </summary>
        private void SaveProjectWithEncoding(string projectFullPath, Encoding encoding)
        {
            // Always use a new project collection to guarantee we're reading off disk.
            using var collection = new ProjectCollection();
            ProjectRootElement project = ProjectRootElement.Open(projectFullPath, collection);
            project.Save(encoding);
            Assert.AreEqual(encoding, project.Encoding); // "Changing an unmodified project's encoding failed to update ProjectRootElement.Encoding."

            // Try to verify that the xml declaration was emitted, and that the correct byte order marks
            // are also present.

            using (var reader = FileUtilities.OpenRead(projectFullPath, encoding, true))
            {
                Assert.AreEqual(encoding, reader.CurrentEncoding);
                string actual = reader.ReadLine();
                string expected = string.Format(@"<?xml version=""1.0"" encoding=""{0}""?>", encoding.WebName);
                Assert.AreEqual(expected, actual); // "The encoding was not emitted as an XML declaration."
            }

            using var projectCollection = new ProjectCollection();
            project = ProjectRootElement.Open(projectFullPath, projectCollection);

            // It's ok for the read Encoding to differ in fields like DecoderFallback,
            // so a pure equality check here is too much.
            Assert.AreEqual(encoding.CodePage, project.Encoding.CodePage);
            Assert.AreEqual(encoding.EncodingName, project.Encoding.EncodingName);
        }

        /// <summary>
        /// Creates a project at a given path with a given encoding but without the Xml declaration,
        /// and then verifies that when loaded by MSBuild, the encoding is correctly reported.
        /// </summary>
        private void VerifyLoadedProjectHasEncoding(string projectFullPath, Encoding encoding)
        {
            CreateProjectWithEncodingWithoutDeclaration(projectFullPath, encoding);

            // Let's just be certain the project has been read off disk...
            using var collection = new ProjectCollection();
            ProjectRootElement project = ProjectRootElement.Open(projectFullPath, collection);
            Assert.AreEqual(encoding.WebName, project.Encoding.WebName);
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
            string content = ObjectModelHelpers.CleanupFileContents(ComplexProject);

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;

            return project;
        }

        private void VerifyAssertLineByLine(string expected, string actual)
        {
            Helpers.VerifyAssertLineByLine(expected, actual, false);
        }
    }
}
