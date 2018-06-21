// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using System.Collections;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Test Fixture Class for the v9 Object Model Public Interface Compatibility Tests for the BuildItemGroupCollection Class.
    /// </summary>
    [TestFixture]
    public sealed class BuildItemGroupCollection_Tests
    {
        #region Common Helpers
        /// <summary>
        /// Basic Project XML Content
        /// </summary>
        private const string ProjectContentWithThreeBuildItemGroups = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup Condition=""'A'=='B'"">
                            <item1n1 Include='item1i1' Exclude='item1e1'>
                                <item1n1m1>item1n1m1value</item1n1m1>
                            </item1n1>
                            <item1n2 Include='item1i2' Condition=""'a2' == 'b2'"" />
                        </ItemGroup>

                        <ItemGroup>
                            <item2n1 Include='item2i1' Exclude='item2e1'>
                                <item2n1m1>item2n1m1value</item2n1m1>
                                <item2n1m2>item2n1m2value</item2n1m2>
                            </item2n1>
                            <item2n2 Include='item2i2' Condition=""'a2' == 'b2'"" />
                        </ItemGroup>

                        <ItemGroup Condition=""'true'=='T'"">
                            <item3n1 Include='item3i1' />
                            <item3n2 Include='item3i2' Condition=""'a2' == 'b2'"" />
                            <item3n3 Include='item3i3' />
                            <item3n4 Include='item3i4' />
                        </ItemGroup>
                    </Project>
                    ";

        /// <summary>
        /// Engine that is used through out test class
        /// </summary>
        private Engine engine;

        /// <summary>
        /// Project that is used through out test class
        /// </summary>
        private Project project;

        /// <summary>
        /// Creates the engine and parent object. Also registers the mock logger.
        /// </summary>
        [SetUp()]
        public void Initialize()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            engine = new Engine();
            project = new Project(engine);
        }

        /// <summary>
        /// Unloads projects and un-registers logger.
        /// </summary>
        [TearDown()]
        public void Cleanup()
        {
            engine.UnloadProject(project);
            engine.UnloadAllProjects();

            ObjectModelHelpers.DeleteTempProjectDirectory();
        }
        #endregion

        #region Count Tests
        /// <summary>
        /// Tests BuildItemGroupCollection.Count simple/basic case
        /// </summary>
        [Test]
        public void CountSimple()
        {
            project.LoadXml(ProjectContentWithThreeBuildItemGroups);
            BuildItemGroupCollection groups = project.ItemGroups;

            Assertion.AssertEquals(3, groups.Count);
        }

        /// <summary>
        /// Tests BuildItemGroupCollection.Count when no BuildItemGroups exist
        /// </summary>
        [Test]
        public void CountZero()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    </Project>
                    ";

            project.LoadXml(projectContents);
            BuildItemGroupCollection groups = project.ItemGroups;

            Assertion.AssertEquals(0, groups.Count);
        }

        /// <summary>
        /// Tests BuildItemGroupCollection.Count when only one BuildItemGroup exists
        /// </summary>
        [Test]
        public void CountOne()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i' />
                        </ItemGroup>
                    </Project>
                    ";

            project.LoadXml(projectContents);
            BuildItemGroupCollection groups = project.ItemGroups;

            Assertion.AssertEquals(1, groups.Count);
        }

        /// <summary>
        /// Tests BuildItemGroupCollection.Count when all BuildItemGroups come from
        ///     Imported Project
        /// </summary>
        [Test]
        public void CountImportedOnly()
        {
            string parentProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <Import Project='import.proj' />
                    </Project>
                ";

            Project p = GetProjectThatImportsAnotherProject(null, parentProjectContents);

            BuildItemGroupCollection groups = p.ItemGroups;
            Assertion.AssertEquals(2, groups.Count);
        }

        /// <summary>
        /// Tests BuildItemGroupCollection.Count when BuildItemGroups come from both
        ///     parent project and imported project
        /// </summary>
        [Test]
        public void CountParentAndImported()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);

            BuildItemGroupCollection groups = p.ItemGroups;
            Assertion.AssertEquals(5, groups.Count);
        }

        /// <summary>
        /// Tests BuildItemGroupCollection.Count after adding a new BuildItemGroup
        /// </summary>
        [Test]
        public void CountAfterAddingNewGroup()
        {
            project.LoadXml(ProjectContentWithThreeBuildItemGroups);
            BuildItemGroupCollection groups = project.ItemGroups;

            project.AddNewItemGroup();

            Assertion.AssertEquals(4, groups.Count);
        }

        /// <summary>
        /// Tests BuildItemGroupCollection.Count after removing all BuildItemGroups
        /// </summary>
        [Test]
        public void CountAfterRemovingAllGroups()
        {
            project.LoadXml(ProjectContentWithThreeBuildItemGroups);
            BuildItemGroupCollection groups = project.ItemGroups;

            project.RemoveAllItemGroups();

            Assertion.AssertEquals(0, groups.Count);
        }
        #endregion

        #region CopyTo Tests
        /// <summary>
        /// Tests BuildItemGroupCollection.CopyTo simple/basic case
        /// </summary>
        [Test]
        public void CopyToSimple()
        {
            project.LoadXml(ProjectContentWithThreeBuildItemGroups);
            BuildItemGroupCollection groups = project.ItemGroups;

            object[] array = new object[groups.Count];
            groups.CopyTo(array, 0);

            int count = 0;
            foreach (BuildItemGroup group in groups)
            {
                Assertion.AssertEquals(array[count].ToString(), group.ToString());
                count++;
            }
        }

        /// <summary>
        /// Tests BuildItemGroupCollection.Copyto when you attempt CopyTo
        ///     into an Array that's not long enough
        /// </summary>
        [Test]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void CopyToArrayThatsNotLargeEnough()
        {
            project.LoadXml(ProjectContentWithThreeBuildItemGroups);
            BuildItemGroupCollection groups = project.ItemGroups;

            object[] array = new object[groups.Count - 1];
            groups.CopyTo(array, 0);
        }
        #endregion

        #region IsSynchronized Tests
        /// <summary>
        /// Tests BuildItemGroupCollection.IsSynchronized for the default case (you load a project p and get the build item group collection from p)
        /// </summary>
        [Test]
        public void IsSynchronizedDefault()
        {
            project.LoadXml(ProjectContentWithThreeBuildItemGroups);
            BuildItemGroupCollection groups = project.ItemGroups;

            Assertion.AssertEquals(false, groups.IsSynchronized);
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Gets a Project that imports another Project
        /// </summary>
        /// <param name="importProjectContents">Project Contents of the imported Project, to get default content, pass in an empty string</param>
        /// <param name="parentProjectContents">Project Contents of the Parent Project, to get default content, pass in an empty string</param>
        /// <returns>Project</returns>
        private Project GetProjectThatImportsAnotherProject(string importProjectContents, string parentProjectContents)
        {
            if (String.IsNullOrEmpty(importProjectContents))
            {
                importProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1ImportedItem1 Include='n1iImportedItem1' />
                            <n2ImportedItem1 Include='n2iImportedItem1'>
                                <n1ImportedMeta1>n1Importedvalue1</n1ImportedMeta1>
                            </n2ImportedItem1>
                        </ItemGroup>

                        <ItemGroup>
                            <nImportedItem2 Include='nImportedItem2' />
                        </ItemGroup>
                    </Project>
                ";
            }

            if (String.IsNullOrEmpty(parentProjectContents))
            {
                parentProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <nMainItem1 Include='nMainItem1' />
                        </ItemGroup>

                        <ItemGroup>
                            <nMainItem2 Include='nMainItem2' />
                        </ItemGroup>

                        <ItemGroup>
                            <n1MainItem3 Include='n1iMainItem3' />
                            <n2MainItem3 Include='n2iMainItem3' >
                                <n2MainMetaItem3>n2MainValueItem3</n2MainMetaItem3>
                            </n2MainItem3>
                        </ItemGroup>
                        <Import Project='import.proj' />
                    </Project>
                ";
            }

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", importProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", parentProjectContents);
            return ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);
        }
        #endregion
    }
}
