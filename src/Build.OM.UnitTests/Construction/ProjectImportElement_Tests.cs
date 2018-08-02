// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectImportElement class
    /// </summary>
    public class ProjectImportElement_Tests
    {
        /// <summary>
        /// Read project with no imports
        /// </summary>
        [Fact]
        public void ReadNone()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            Assert.Equal(null, project.Imports.GetEnumerator().Current);
        }

        /// <summary>
        /// Read import with no project attribute
        /// </summary>
        [Fact]
        public void ReadInvalidMissingProject()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Import/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read import with empty project attribute
        /// </summary>
        [Fact]
        public void ReadInvalidEmptyProject()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Import Project=''/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read import with unexpected attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Import Project='p' X='Y'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            }
           );
        }
        /// <summary>
        /// Read basic valid imports
        /// </summary>
        [Fact]
        public void ReadBasic()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Import Project='i1.proj' />
                        <Import Project='i2.proj' Condition='c'/>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            List<ProjectImportElement> imports = Helpers.MakeList(project.Imports);

            Assert.Equal(2, imports.Count);
            Assert.Equal("i1.proj", imports[0].Project);
            Assert.Equal("i2.proj", imports[1].Project);
            Assert.Equal("c", imports[1].Condition);
        }

        /// <summary>
        /// Set valid project on import
        /// </summary>
        [Fact]
        public void SetProjectValid()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Import Project='i1.proj' />
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            ProjectImportElement import = (ProjectImportElement)Helpers.GetFirst(project.Children);

            import.Project = "i1b.proj";
            Assert.Equal("i1b.proj", import.Project);
        }

        /// <summary>
        /// Set invalid empty project value on import
        /// </summary>
        [Fact]
        public void SetProjectInvalidEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Import Project='i1.proj' />
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

                ProjectImportElement import = (ProjectImportElement)Helpers.GetFirst(project.Children);

                import.Project = String.Empty;
            }
           );
        }
        /// <summary>
        /// Setting the project attribute should dirty the project
        /// </summary>
        [Fact]
        public void SettingProjectDirties()
        {
            string file1 = null;
            string file2 = null;

            try
            {
                file1 = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                ProjectRootElement importProject1 = ProjectRootElement.Create();
                importProject1.AddProperty("p", "v1");
                importProject1.Save(file1);

                file2 = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                ProjectRootElement importProject2 = ProjectRootElement.Create();
                importProject2.AddProperty("p", "v2");
                importProject2.Save(file2);

                string content = String.Format
                    (
    @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
    <Import Project='{0}'/>
</Project>",
                    file1
                    );

                Project project = new Project(XmlReader.Create(new StringReader(content)));
                ProjectImportElement import = Helpers.GetFirst(project.Xml.Imports);
                import.Project = file2;

                Assert.Equal("v1", project.GetPropertyValue("p"));

                project.ReevaluateIfNecessary();

                Assert.Equal("v2", project.GetPropertyValue("p"));
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
        [Fact]
        public void SettingConditionDirties()
        {
            string file = null;

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
                ProjectRootElement importProject = ProjectRootElement.Create();
                importProject.AddProperty("p", "v1");
                importProject.Save(file);

                string content = String.Format
                    (
    @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
    <Import Project='{0}'/>
</Project>",
                    file
                    );

                Project project = new Project(XmlReader.Create(new StringReader(content)));
                ProjectImportElement import = Helpers.GetFirst(project.Xml.Imports);
                import.Condition = "false";

                Assert.Equal("v1", project.GetPropertyValue("p"));

                project.ReevaluateIfNecessary();

                Assert.Equal(String.Empty, project.GetPropertyValue("p"));
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Importing a project which has a relative path
        /// </summary>
        [Fact]
        public void ImportWithRelativePath()
        {
            string tempPath = Path.GetTempPath();
            string testTempPath = Path.Combine(tempPath, "UnitTestsPublicOm");
            string projectfile = Path.Combine(testTempPath, "a.proj");
            string targetsFile = Path.Combine(tempPath, "x.targets");
            string projectfileContent = String.Format
                (
                @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Import Project='{0}'/>
                    </Project>
                ",
                 testTempPath + "\\..\\x.targets"
                 );
            string targetsfileContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                    </Project>
                ";
            try
            {
                Directory.CreateDirectory(testTempPath);
                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(projectfileContent)));
                project.Save(projectfile);
                project = ProjectRootElement.Create(XmlReader.Create(new StringReader(targetsfileContent)));
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
