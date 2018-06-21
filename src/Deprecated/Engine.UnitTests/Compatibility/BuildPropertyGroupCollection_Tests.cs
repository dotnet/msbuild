// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Tests for BuildPropertyGroupCollection
    /// </summary>
    [TestFixture]
    public class BuildPropertyGroupCollection_Tests
    {
        #region Helper Fields
        /// <summary>
        /// Basic Project Contents with 1 Property Group
        /// </summary>
        private string basicProjectContentsOnePropertyGroup = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <n>v</n>
                    </PropertyGroup>
                </Project>
                "; 
        #endregion

        #region Count Tests
        /// <summary>
        /// Tests BuildPropertyGroupCollection Count when Zero property groups exist
        /// </summary>
        [Test]
        public void CountZero()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);

            BuildPropertyGroupCollection groups = p.PropertyGroups;
            Assertion.AssertEquals(0, groups.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroupCollection Count when One property group exists
        /// </summary>
        [Test]
        public void CountOne()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsOnePropertyGroup);

            BuildPropertyGroupCollection groups = p.PropertyGroups;
            Assertion.AssertEquals(1, groups.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroupCollection Count when Many property groups exist
        /// </summary>
        [Test]
        public void CountMany()
        {
            string projectContents = @" 
                <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <Optimize>true</Optimize>
                        <WarningLevel>4</WarningLevel>
                        <n1>v1</n1>
                    </PropertyGroup>

                    <PropertyGroup>
                        <OutputPath>bin\debug\</OutputPath>
                        <n2>v2</n2>
                    </PropertyGroup>

                    <PropertyGroup>
                        <n3>v3</n3>
                    </PropertyGroup>

                    <PropertyGroup>
                        <n4>v4</n4>
                    </PropertyGroup>

                    <PropertyGroup>
                        <n5>v5</n5>
                    </PropertyGroup>
                </Project>
                ";

            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(projectContents);

            BuildPropertyGroupCollection groups = p.PropertyGroups;
            Assertion.AssertEquals(5, groups.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroupCollection Count when all property groups come from an import
        /// </summary>
        [Test]
        public void CountFromImports()
        {
            Project p = GetProjectWithTwoImportProjects();
            BuildPropertyGroupCollection groups = p.PropertyGroups;

            Assertion.AssertEquals(5, groups.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroupCollection Count that a Property Group that's created within a Target isn't included in the count
        /// </summary>
        [Test]
        public void CountDoesntIncludeGroupCreatedInTarget()
        {
            string projectContent = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <n1>v1</n1>
                    </PropertyGroup>
                    <Target Name=`t`>
                        <PropertyGroup>
                            <n2>v2</n2>
                        </PropertyGroup>
                    </Target>
                </Project>
            ";
            Project p = ObjectModelHelpers.CreateInMemoryProject(projectContent);
            BuildPropertyGroupCollection groups = p.PropertyGroups;

            Assertion.AssertEquals(1, groups.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroupCollection Count after adding a new Property Group
        /// </summary>
        [Test]
        public void CountAfterAddingNewGroup()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsOnePropertyGroup);
            p.AddNewPropertyGroup(true);

            BuildPropertyGroupCollection groups = p.PropertyGroups;
            Assertion.AssertEquals(2, groups.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroupCollection Count after removing a newly added Property Group
        /// </summary>
        [Test]
        public void CountAfterRemovingNewGroup()
        {
            Project p = Build.UnitTests.ObjectModelHelpers.CreateInMemoryProject(basicProjectContentsOnePropertyGroup);
            BuildPropertyGroup newGroup = p.AddNewPropertyGroup(true);
            p.RemovePropertyGroup(newGroup);

            BuildPropertyGroupCollection groups = p.PropertyGroups;
            Assertion.AssertEquals(1, groups.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroupCollection Count after removing an existing Property Group from the Main project
        /// </summary>
        [Test]
        public void CountAfterRemovingGroupFromMainProject()
        {
            Project p = GetProjectWithOneImportProject();

            RemovePropertyGroupThatContainsSpecifiedBuildProperty(p, "main");

            BuildPropertyGroupCollection groups = p.PropertyGroups;
            Assertion.AssertEquals(1, groups.Count);
        }

        /// <summary>
        /// Tests BuildPropertyGroupCollection Count after attempting to removing an existing Property Group from an imported project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CountAfterRemovingGroupFromImportedProject()
        {
            Project p = GetProjectWithOneImportProject();

            RemovePropertyGroupThatContainsSpecifiedBuildProperty(p, "imported");
        }
        #endregion

        #region CopyTo Tests
        /// <summary>
        /// Tests BuildPropertyGroupCollection CopyTo Basic case
        /// </summary>
        [Test]
        public void CopyToSimple()
        {
            Project p = GetProjectWithTwoImportProjects();
            BuildPropertyGroupCollection groups = p.PropertyGroups;

            object[] array = new object[groups.Count];
            groups.CopyTo(array, 0);

            int count = 0;
            foreach (BuildPropertyGroup group in groups)
            {
                Assertion.AssertEquals(array[count].ToString(), group.ToString());
                count++;
            }
        }

        /// <summary>
        /// Tests BuildPropertyGroupCollection CopyTo when you attempt CopyTo into an Array that's not long enough
        /// </summary>
        [Test]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void CopyToArrayThatsNotLargeEnough()
        {
            Project p = GetProjectWithTwoImportProjects();
            BuildPropertyGroupCollection groups = p.PropertyGroups;

            object[] array = new object[2]; ////groups.Count is 5
            groups.CopyTo(array, 0);
        }
        #endregion;

        #region IsSynchronized Tests
        /// <summary>
        /// Tests BuildPropertyGroupCollection  IsSynchronized for the default case (you load a project p and get the build property group collection from p)
        /// </summary>
        [Test]
        public void IsSynchronizedDefault()
        {
            Project p = GetProjectWithTwoImportProjects();
            BuildPropertyGroupCollection groups = p.PropertyGroups;

            Assertion.AssertEquals(false, groups.IsSynchronized);
        } 
        #endregion

        #region Helper Methods
        /// <summary>
        /// Helper method to Remove a BuildPropertyGroup from a Project p by specifying a BuildProperty that is contained
        ///     within the BuildPropertyGroup that you want to remove
        /// </summary>
        /// <param name="p">Project</param>
        /// <param name="buildPropertyName">Name of the BuildProperty that you want to key off of</param>
        private static void RemovePropertyGroupThatContainsSpecifiedBuildProperty(Project p, string buildPropertyName)
        {
            foreach (BuildPropertyGroup group in p.PropertyGroups)
            {
                foreach (BuildProperty property in group)
                {
                    if (String.Equals(property.Name, buildPropertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        p.RemovePropertyGroup(group);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Helper Method to create a Main project that imports 1 other project.  Each project contains only one Build Property Group
        ///     and each of those Build Property Groups contain a Build Property of a specific name (which is used as a flag/key) to 
        ///     identify which Build Property Group we're working with.
        /// </summary>
        /// <returns>Project</returns>
        private static Project GetProjectWithOneImportProject()
        {
            string importProjectContents = @" 
                    <Project xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                            <imported>v</imported>
                        </PropertyGroup>
                    </Project>
                ";

            string projectContents = @" 
                    <Project xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                            <main>v</main>
                        </PropertyGroup>
                        <Import Project=`import1.proj` />
                        <Target Name=`Build`>
                            <WashCar/>
                        </Target>
                    </Project>
                ";

            ObjectModelHelpers.DeleteTempProjectDirectory();
            ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.proj", importProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", projectContents);

            Project p = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);
            return p;
        }
        
        /// <summary>
        /// Helper Method to create a Main project that imports 2 other projects, each with build property groups
        /// </summary>
        /// <returns>Project</returns>
        private static Project GetProjectWithTwoImportProjects()
        {
            string subProjectContents = @" 
                    <Project xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                            <n1>v1</n1>
                        </PropertyGroup>
                        <PropertyGroup>
                            <n2>v2</n2>
                        </PropertyGroup>
                    </Project>
                ";

            string projectContents = @" 
                    <Project xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                            <n>v</n>
                        </PropertyGroup>
                        <Import Project=`import1.proj` />
                        <Import Project=`import2.proj` />
                        <Target Name=`Build`>
                            <WashCar/>
                        </Target>
                    </Project>
                ";

            ObjectModelHelpers.DeleteTempProjectDirectory();
            ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.proj", subProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.proj", subProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", projectContents);

            Project p = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);
            return p;
        }
        #endregion
    }
}
