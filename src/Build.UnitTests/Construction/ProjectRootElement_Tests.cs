// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

#nullable disable

namespace Microsoft.Build.UnitTests.Construction
{
    /// <summary>
    /// Tests for the ElementLocation class
    /// </summary>
    [TestClass]
    public class ProjectRootElement_Tests
    {
        [MSBuildTestMethod]
        [DataRow("", true)]
        [DataRow("", false)]
        [DataRow(@"<?xml version=""1.0"" encoding=""utf-8""?>", true)]
        [DataRow(@"<?xml version=""1.0"" encoding=""utf-8""?>", false)]
        [DataRow(@"<?xml version=""1.0"" encoding=""utf-8""?>
", true)]
        [DataRow(@"<?xml version=""1.0"" encoding=""utf-8""?>
", false)]
        public void IsEmptyXmlFileReturnsTrue(string contents, bool useByteOrderMark)
        {
            string path = useByteOrderMark ?
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents, Encoding.UTF8) :
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents);

            Assert.IsTrue(ProjectRootElement.IsEmptyXmlFile(path));
        }

        [MSBuildTestMethod]
        [DataRow("<Foo/>", true)]
        [DataRow("Foo/>", false)]
        [DataRow(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Foo/>", true)]
        [DataRow(@"<?xml version=""1.0"" encoding=""utf-8""?>
<Foo/>", false)]
        [DataRow(@"<?xml version=""1.0"" encoding=""utf-8""?>
bar", true)]
        [DataRow(@"<?xml version=""1.0"" encoding=""utf-8""?>
bar", false)]
        public void IsEmptyXmlFileReturnsFalse(string contents, bool useByteOrderMark)
        {
            string path = useByteOrderMark ?
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents, Encoding.UTF8) :
                ObjectModelHelpers.CreateFileInTempProjectDirectory(Guid.NewGuid().ToString("N"), contents);

            Assert.IsFalse(ProjectRootElement.IsEmptyXmlFile(path));
        }

        [MSBuildTestMethod]
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

                Assert.IsFalse(xml.XmlDocument.IsReadOnly);
                var children = xml.XmlDocument.ChildNodes;
                Assert.ContainsSingle(children);
                Assert.AreEqual("Project", children[0].Name);
                Assert.AreEqual(2, children[0].ChildNodes.Count);
                Assert.AreEqual("Initial Comment", children[0].ChildNodes[0].Value);
                Assert.AreEqual("Ending Comment", children[0].ChildNodes[1].Value);
            }
        }

        [MSBuildTestMethod]
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

                Assert.IsTrue(xml.XmlDocument.IsReadOnly);
                var children = xml.XmlDocument.ChildNodes;
                Assert.ContainsSingle(children);
                Assert.AreEqual("Project", children[0].Name);
                Assert.AreEqual(2, children[0].ChildNodes.Count);
                Assert.AreEqual(string.Empty, children[0].ChildNodes[0].Value);
                Assert.AreEqual(string.Empty, children[0].ChildNodes[1].Value);

                // We cleared at the beginning, but then we set MSBUILDLOADALLFILESASREADONLY to 1.
                // This means that opening the project will cache s_readOnlyFlags as ReadOnlyLoadFlags.LoadAllReadOnly
                // Keeping the cached ReadOnlyLoadFlags.LoadAllReadOnly can impact subsequent tests that are running in the same process.
                // So ensure to re-clear.
                XmlDocumentWithLocation.ClearReadOnlyFlags_UnitTestsOnly();
            }
        }

        [MSBuildTestMethod]
        public void CreateEphemeralCannotBeDirtied()
        {
            var projectRootElement = ProjectRootElement.CreateEphemeral(ProjectCollection.GlobalProjectCollection.ProjectRootElementCache);
            var versionBeforeMarkDirty = projectRootElement.Version;

            projectRootElement.MarkDirty("test", "test");

            Assert.AreEqual(projectRootElement.Version, versionBeforeMarkDirty);
        }
    }
}
