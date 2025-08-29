// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Xunit;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for the ProjectImportGroupElement class
    /// </summary>
    public class ProjectImportGroupElement_Tests
    {
        /// <summary>
        /// Tests that an import is added at the end of the file
        /// when no import group exists
        /// </summary>
        [Fact]
        public void AddImportWhenNoImportGroupExists()
        {
            string content = @"
                    <Project>
                        <Import Project='a.proj' />
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            project.AddImport("b.proj");

            string expectedContent = @"
                    <Project>
                        <Import Project='a.proj' />
                        <Import Project='b.proj' />
                    </Project>
                ";

            Helpers.CompareProjectXml(expectedContent, project.RawXml);
        }

        /// <summary>
        /// Tests that an import is added to (the last) (non-conditioned)
        /// import group if one exists
        /// </summary>
        [Fact]
        public void AddImportToLastImportGroupWithNoCondition()
        {
            string content = @"
                    <Project>
                        <Import Project='a.proj' />
                        <ImportGroup>
                            <Import Project='b.proj' />
                        </ImportGroup>
                        <ImportGroup Condition='c1'>
                            <Import Project='c.proj' />
                        </ImportGroup>
                        <ImportGroup>
                            <Import Project='d.proj' />
                        </ImportGroup>
                        <ImportGroup Condition='c2'>
                            <Import Project='f.proj' />
                        </ImportGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            project.AddImport("e.proj");

            string expectedContent = @"
                    <Project>
                        <Import Project='a.proj' />
                        <ImportGroup>
                            <Import Project='b.proj' />
                        </ImportGroup>
                        <ImportGroup Condition='c1'>
                            <Import Project='c.proj' />
                        </ImportGroup>
                        <ImportGroup>
                            <Import Project='d.proj' />
                            <Import Project='e.proj' />
                        </ImportGroup>
                        <ImportGroup Condition='c2'>
                            <Import Project='f.proj' />
                        </ImportGroup>
                    </Project>
                ";

            Helpers.CompareProjectXml(expectedContent, project.RawXml);
        }

        /// <summary>
        /// Tests that an import is added at the end of the file
        /// when no import group exists
        /// </summary>
        [Fact]
        public void AddImportOnlyConditionedImportGroupsExist()
        {
            string content = @"
                    <Project>
                        <Import Project='a.proj' />
                        <ImportGroup Condition='c1'>
                            <Import Project='b.proj' />
                        </ImportGroup>
                        <ImportGroup Condition='c2'>
                            <Import Project='c.proj' />
                        </ImportGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            project.AddImport("d.proj");

            string expectedContent = @"
                    <Project>
                        <Import Project='a.proj' />
                        <ImportGroup Condition='c1'>
                            <Import Project='b.proj' />
                        </ImportGroup>
                        <ImportGroup Condition='c2'>
                            <Import Project='c.proj' />
                        </ImportGroup>
                        <Import Project='d.proj' />
                    </Project>
                ";

            Helpers.CompareProjectXml(expectedContent, project.RawXml);
        }

        /// <summary>
        /// Read project with no imports
        /// </summary>
        [Fact]
        public void ReadNone()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            Assert.Empty(project.Imports);
        }

        /// <summary>
        /// An empty import group does nothing, but also shouldn't error
        /// </summary>
        [Fact]
        public void ReadNoChild()
        {
            string content = @"
                    <Project>
                        <ImportGroup />
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            ProjectImportGroupElement importGroup = (ProjectImportGroupElement)Helpers.GetFirst(project.ImportGroups);

            Assert.Empty(project.Imports);
            Assert.Equal(0, Helpers.Count(importGroup.Imports));
        }

        /// <summary>
        /// Read import group with a contained import that has no project attribute
        /// </summary>
        [Fact]
        public void ReadInvalidChildMissingProject()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ImportGroup>
                            <Import/>
                        </ImportGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Checks that an InvalidProjectFileException is thrown when an invalid
        /// child type is placed inside an ImportGroup.
        /// </summary>
        [Fact]
        public void ReadInvalidChildType()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ImportGroup>
                            <PropertyGroup />
                        </ImportGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Checks that an InvalidProjectFileException is thrown when an ImportGroup is placed
        /// inside an invalid parent.
        /// </summary>
        [Fact]
        public void ReadInvalidParentType()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <PropertyGroup>
                            <ImportGroup />
                        </PropertyGroup>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read import group with unexpected attribute
        /// </summary>
        [Fact]
        public void ReadInvalidAttribute()
        {
            Assert.Throws<InvalidProjectFileException>(() =>
            {
                string content = @"
                    <Project>
                        <ImportGroup X='Y'/>
                    </Project>
                ";

                ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            });
        }
        /// <summary>
        /// Read basic valid import group
        /// </summary>
        [Fact]
        public void ReadBasic()
        {
            string content = @"
                    <Project>
                        <ImportGroup>
                            <Import Project='i1.proj' />
                            <Import Project='i2.proj' Condition='c'/>
                        </ImportGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            List<ProjectImportElement> imports = Helpers.MakeList(project.Imports);
            List<ProjectImportGroupElement> importGroups = Helpers.MakeList(project.ImportGroups);

            Assert.Single(importGroups);
            Assert.Equal(2, importGroups[0].Count);
            Assert.Equal(2, imports.Count);
            Assert.Equal("i1.proj", imports[0].Project);
            Assert.Equal("i2.proj", imports[1].Project);
            Assert.Equal("c", imports[1].Condition);
        }

        /// <summary>
        /// Multiple import groups should all show up in the project's imports
        /// </summary>
        [Fact]
        public void ReadMultipleImportGroups()
        {
            string content = @"
                    <Project>
                        <ImportGroup>
                            <Import Project='i1.proj' />
                            <Import Project='i2.proj' Condition='c'/>
                        </ImportGroup>
                        <ImportGroup Label='second'>
                            <Import Project='i3.proj' />
                        </ImportGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            List<ProjectImportElement> imports = Helpers.MakeList(project.Imports);
            List<ProjectImportGroupElement> importGroups = Helpers.MakeList(project.ImportGroups);

            Assert.Equal(2, importGroups.Count);
            Assert.Equal(2, importGroups[0].Count);
            Assert.Equal(1, importGroups[1].Count);
            Assert.Equal("second", importGroups[1].Label);

            Assert.Equal(3, imports.Count);
            Assert.Equal("i1.proj", imports[0].Project);
            Assert.Equal("i2.proj", imports[1].Project);
            Assert.Equal("c", imports[1].Condition);
            Assert.Equal("i3.proj", imports[2].Project);
        }

        /// <summary>
        /// Set valid project on import
        /// </summary>
        [Fact]
        public void SetProjectValid()
        {
            string content = @"
                    <Project>
                        <ImportGroup>
                            <Import Project='i1.proj' />
                        </ImportGroup>
                    </Project>
                ";

            ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

            ProjectImportGroupElement importGroup = (ProjectImportGroupElement)Helpers.GetFirst(project.Children);

            ProjectImportElement import = (ProjectImportElement)Helpers.GetFirst(importGroup.Imports);

            import.Project = "i1b.proj";
            Assert.Equal("i1b.proj", import.Project);
            Assert.True(project.HasUnsavedChanges);
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
                    <Project>
                        <ImportGroup>
                            <Import Project='i1.proj' />
                        </ImportGroup>
                    </Project>
                ";

                ProjectRootElement project = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));

                ProjectImportGroupElement importGroup = (ProjectImportGroupElement)Helpers.GetFirst(project.Children);

                ProjectImportElement import = (ProjectImportElement)Helpers.GetFirst(importGroup.Imports);

                import.Project = String.Empty;
            });
        }
        /// <summary>
        /// Set the condition value
        /// </summary>
        [Fact]
        public void SetCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddImportGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectImportGroupElement importGroup = Helpers.GetFirst(project.ImportGroups);
            importGroup.Condition = "c";

            Assert.Equal("c", importGroup.Condition);
            Assert.True(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Set the label value
        /// </summary>
        [Fact]
        public void SetLabel()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddImportGroup();
            Helpers.ClearDirtyFlag(project);

            ProjectImportGroupElement importGroup = Helpers.GetFirst(project.ImportGroups);
            importGroup.Label = "c";

            Assert.Equal("c", importGroup.Label);
            Assert.True(project.HasUnsavedChanges);
        }
    }
}
