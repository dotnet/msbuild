// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectImportElement class
    /// </summary>
    [TestClass]
    public class ProjectImportElement_Tests
    {
        /// <summary>
        /// Read project with no imports
        /// </summary>
        [MSBuildTestMethod]
        public void ReadNone()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            Assert.IsEmpty(project.Imports);
        }

        /// <summary>
        /// Read import with no project attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidMissingProject()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Import/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read import with empty project attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidEmptyProject()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Import Project=''/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read import with unexpected attribute
        /// </summary>
        [MSBuildTestMethod]
        public void ReadInvalidAttribute()
        {
            Assert.ThrowsExactly<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <Import Project='p' X='Y'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read basic valid imports
        /// </summary>
        [MSBuildTestMethod]
        public void ReadBasic()
        {
            string content = @"
                    <Project>
                        <Import Project='i1.proj' />
                        <Import Project='i2.proj' Condition='c'/>
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;

            List<ProjectImportElement> imports = Helpers.MakeList(project.Imports);

            Assert.AreEqual(2, imports.Count);
            Assert.AreEqual("i1.proj", imports[0].Project);
            Assert.AreEqual("i2.proj", imports[1].Project);
            Assert.AreEqual("c", imports[1].Condition);
        }

        /// <summary>
        /// Set valid project on import
        /// </summary>
        [MSBuildTestMethod]
        public void SetProjectValid()
        {
            string content = @"
                    <Project>
                        <Import Project='i1.proj' />
                    </Project>
                ";

            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement project = projectRootElementFromString.Project;

            ProjectImportElement import = (ProjectImportElement)Helpers.GetFirst(project.Children);

            import.Project = "i1b.proj";
            Assert.AreEqual("i1b.proj", import.Project);
        }

        /// <summary>
        /// Set invalid empty project value on import
        /// </summary>
        [MSBuildTestMethod]
        public void SetProjectInvalidEmpty()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                string content = @"
                    <Project>
                        <Import Project='i1.proj' />
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

                ProjectImportElement import = (ProjectImportElement)Helpers.GetFirst(project.Children);

                import.Project = String.Empty;
            });
        }
        /// <summary>
        /// Setting the project attribute should dirty the project
        /// </summary>
        [MSBuildTestMethod]
        public void SettingProjectDirties()
        {
            string file1 = null;
            string file2 = null;

            try
            {
                file1 = FileUtilities.GetTemporaryFileName();
                ProjectRootElement importProject1 = ProjectRootElement.Create();
                importProject1.AddProperty("p", "v1");
                importProject1.Save(file1);

                file2 = FileUtilities.GetTemporaryFileName();
                ProjectRootElement importProject2 = ProjectRootElement.Create();
                importProject2.AddProperty("p", "v2");
                importProject2.Save(file2);

                string content = String.Format(
    @"<Project>
    <Import Project='{0}'/>
</Project>",
                    file1);

                using ProjectFromString projectFromString = new(content);
                Project project = projectFromString.Project;
                ProjectImportElement import = Helpers.GetFirst(project.Xml.Imports);
                import.Project = file2;

                Assert.AreEqual("v1", project.GetPropertyValue("p"));

                project.ReevaluateIfNecessary();

                Assert.AreEqual("v2", project.GetPropertyValue("p"));
            }
            finally
            {
                File.Delete(file1);
                File.Delete(file2);
            }
        }

        /// <summary>
        /// Setting the condition should dirty the project
        /// </summary>
        [MSBuildTestMethod]
        public void SettingConditionDirties()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFileName();
                ProjectRootElement importProject = ProjectRootElement.Create();
                importProject.AddProperty("p", "v1");
                importProject.Save(file);

                string content = String.Format(
    @"<Project>
    <Import Project='{0}'/>
</Project>",
                    file);

                using ProjectFromString projectFromString = new(content);
                Project project = projectFromString.Project;
                ProjectImportElement import = Helpers.GetFirst(project.Xml.Imports);
                import.Condition = "false";

                Assert.AreEqual("v1", project.GetPropertyValue("p"));

                project.ReevaluateIfNecessary();

                Assert.AreEqual(String.Empty, project.GetPropertyValue("p"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Importing a project which has a relative path
        /// </summary>
        [MSBuildTestMethod]
        public void ImportWithRelativePath()
        {
            string tempPath = Path.GetTempPath();
            string testTempPath = Path.Combine(tempPath, "UnitTestsPublicOm");
            string projectfile = Path.Combine(testTempPath, "a.proj");
            string targetsFile = Path.Combine(tempPath, "x.targets");
            string projectfileContent = String.Format(
                @"
                    <Project>
                        <Import Project='{0}'/>
                    </Project>
                ",
                 testTempPath + "\\..\\x.targets");
            string targetsfileContent = @"
                    <Project>
                    </Project>
                ";
            try
            {
                Directory.CreateDirectory(testTempPath);
                using ProjectRootElementFromString projectFileProject = new(projectfileContent);
                ProjectRootElement project = projectFileProject.Project;
                project.Save(projectfile);
                using ProjectRootElementFromString targetsFileProject = new(targetsfileContent);
                project = targetsFileProject.Project;
                project.Save(targetsFile);
                Project msbuildProject = new Project(projectfile);
            }
            finally
            {
                if (Directory.Exists(testTempPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testTempPath, true);
                }

                if (File.Exists(targetsFile))
                {
                    File.Delete(targetsFile);
                }
            }
        }
    }
}
