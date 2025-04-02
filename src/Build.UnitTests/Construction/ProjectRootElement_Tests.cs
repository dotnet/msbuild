// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Construction
{
    /// <summary>
    /// Tests for the ElementLocation class
    /// </summary>
    public class ProjectRootElement_Tests
    {
        [Theory]
        [InlineData("", true)]
        [InlineData("", false)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>", true)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>", false)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
", true)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
", false)]
        public void IsEmptyXmlFileReturnsTrue(string contents, bool useByteOrderMark)
        {
            string path = useByteOrderMark ?
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents, Encoding.UTF8) :
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents);

            Assert.True(ProjectRootElement.IsEmptyXmlFile(path));
        }

        [Theory]
        [InlineData("<Foo/>", true)]
        [InlineData("Foo/>", false)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Foo/>", true)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Foo/>", false)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
bar", true)]
        [InlineData(@"<?xml version=""1.0"" encoding=""utf-8""?>
bar", false)]
        public void IsEmptyXmlFileReturnsFalse(string contents, bool useByteOrderMark)
        {
            string path = useByteOrderMark ?
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents, Encoding.UTF8) :
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents);

            Assert.False(ProjectRootElement.IsEmptyXmlFile(path));
        }

        [Fact]
        public void ProjectLoadedPreservingCommentsAndWhiteSpaceIsNotReadOnly()
        {
            var projectContents =
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                  <!--Initial Comment-->

                  <!--Ending Comment-->
                </Project>
                ";

            using (var env = TestEnvironment.Create())
            {
                // reset all hooks
                XmlDocumentWithLocation.ClearReadOnlyFlags_UnitTestsOnly();
                env.SetEnvironmentVariable("MSBUILDLOADALLFILESASREADONLY", null); // clear
                env.SetEnvironmentVariable("MSBuildLoadMicrosoftTargetsReadOnly", null); // clear
                env.SetEnvironmentVariable("MSBUILDLOADALLFILESASWRITEABLE", null); // clear
                var testFiles = env.CreateTestProjectWithFiles(projectContents, Array.Empty<string>());
                ProjectRootElement xml = ProjectRootElement.Open(testFiles.ProjectFile);

                Assert.False(xml.XmlDocument.IsReadOnly);
                var children = xml.XmlDocument.ChildNodes;
                Assert.Single(children);
                Assert.Equal("Project", children[0].Name);
                Assert.Equal(2, children[0].ChildNodes.Count);
                Assert.Equal("Initial Comment", children[0].ChildNodes[0].Value);
                Assert.Equal("Ending Comment", children[0].ChildNodes[1].Value);
            }
        }

        [Fact]
        public void ProjectLoadedStrippingCommentsAndWhiteSpaceIsReadOnly()
        {
            var projectContents =
                @"<Project ToolsVersion='msbuilddefaulttoolsversion' DefaultTargets='Build' xmlns='msbuildnamespace'>
                  <!--Initial Comment-->

                  <!--Ending Comment-->
                </Project>
                ";

            using (var env = TestEnvironment.Create())
            {
                // set the hook for the desired read-only mode and reset the hook for the other modes
                XmlDocumentWithLocation.ClearReadOnlyFlags_UnitTestsOnly();
                env.SetEnvironmentVariable("MSBUILDLOADALLFILESASREADONLY", "1");
                env.SetEnvironmentVariable("MSBuildLoadMicrosoftTargetsReadOnly", null); // clear
                env.SetEnvironmentVariable("MSBUILDLOADALLFILESASWRITEABLE", null); // clear

                var testFiles = env.CreateTestProjectWithFiles(projectContents, Array.Empty<string>());
                ProjectRootElement xml = ProjectRootElement.Open(testFiles.ProjectFile);

                Assert.True(xml.XmlDocument.IsReadOnly);
                var children = xml.XmlDocument.ChildNodes;
                Assert.Single(children);
                Assert.Equal("Project", children[0].Name);
                Assert.Equal(2, children[0].ChildNodes.Count);
                Assert.Equal(string.Empty, children[0].ChildNodes[0].Value);
                Assert.Equal(string.Empty, children[0].ChildNodes[1].Value);
            }
        }

        [Fact]
        public void CreateEphemeralCannotBeDirtied()
        {
            var projectRootElement = ProjectRootElement.CreateEphemeral(ProjectCollection.GlobalProjectCollection.ProjectRootElementCache);
            var versionBeforeMarkDirty = projectRootElement.Version;

            projectRootElement.MarkDirty("test", "test");

            Assert.Equal(projectRootElement.Version, versionBeforeMarkDirty);
        }
    }
}
