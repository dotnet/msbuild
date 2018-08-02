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
    /// Test Fixture Class for the v9 Object Model Public Interface Compatibility Tests for the BuildItem Class.
    /// </summary>
    [TestFixture]
    public sealed class BuildItem_Tests
    {
        #region Common Helpers
        /// <summary>
        /// Basic Project XML Content with One BuildItemGroup, which contain One BuildItem
        /// </summary>
        private const string ProjectContentOneItemGroupWithOneBuildItem = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i' />
                        </ItemGroup>
                    </Project>
                    ";

        /// <summary>
        /// Basic Project XML Conent with a BuildItemGroup, which contains one builditem with a condition
        /// </summary>
        private const string ProjectContentOneItemGroupWithOneBuildItemWithCondition = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i' Condition=""'a' == 'b'"" />
                        </ItemGroup>
                    </Project>
                    ";

        /// <summary>
        /// Basic Project XML Content with One BuildItemGroup, which contains several BuildItems
        /// </summary>
        private const string ProjectContentOneItemGroupWithSeveralBuildItems = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1 Include='i1' />
                            <n2 Include='i2' />
                            <n3 Include='i3' />
                            <n4 Include='i4' />
                        </ItemGroup>
                    </Project>
                    ";

        /// <summary>
        /// Basic Project XML Content with One BuildItemGroup, which contains several BuildItems
        ///     that have Includes and Excludes
        /// </summary>
        private const string ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1 Include='i1' Exclude='e1' />
                            <n2 Include='i2' Exclude='e2' />
                            <n3 Include='i3' Exclude='e3' />
                            <n4 Include='i4' Exclude='e4' />
                        </ItemGroup>
                    </Project>
                    ";

        /// <summary>
        /// Basic project XML Content with one BuildItemGroup, which contains one BuildItem
        ///     that has one custom metadata
        /// </summary>
        private const string ProjectContentOneItemGroupOneBuildItemOneMetadata = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                                <Meta>value</Meta>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";

        /// <summary>
        /// Basic Project XML Content with One BuildItemGroup, which contains one BuildItem
        ///     that has some Custom Metadata
        /// </summary>
        private const string ProjectContentOneItemGroupOneBuildItemWithCustomMetadata = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                                <a_Meta>a</a_Meta>
                                <b_Meta>b</b_Meta>
                                <c_Meta>c</c_Meta>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";

        /// <summary>
        /// String of Special Characters to use in tests
        /// </summary>
        private const string SpecialCharacters = "%24%40%3b%5c%25";

        /// <summary>
        /// String of EscapableCharacters to use in tests
        /// </summary>
        private const string EscapableCharacters = @"%*?@$();\";

        /// <summary>
        /// string array of all Reserved Names for BuildItem name
        /// </summary>
        private string[] reservedNames = new string[] 
                { 
                    "VisualStudioProject",
                    "Target",
                    "PropertyGroup",
                    "Output",
                    "ItemGroup",
                    "UsingTask",
                    "ProjectExtensions",
                    "OnError",
                    "Choose",
                    "When",
                    "Otherwise"
                };

        /// <summary>
        /// string array of all Build in BuildItem Metadata Names
        /// </summary>
        private string[] builtInMetadataNames = new string[]
                {
                    "FullPath",
                    "RootDir",
                    "Filename",
                    "Extension",
                    "RelativeDir",
                    "Directory",
                    "RecursiveDir",
                    "Identity",
                    "ModifiedTime",
                    "CreatedTime",
                    "AccessedTime"
                };

        /// <summary>
        /// Engine that is used through out test class
        /// </summary>
        private Engine engine;

        /// <summary>
        /// Project that is used through out test class
        /// </summary>
        private Project project;

        /// <summary>
        /// MockLogger that is used through out test class
        /// </summary>
        private MockLogger logger;

        /// <summary>
        /// enum of test types to be able to enable helper methods to know which test type
        /// </summary>
        private enum TypeOfTest
        {
            /// <summary>
            /// Used when test case is a BuildItem Constructor Test
            /// </summary>
            ConstructorTest,

            /// <summary>
            /// Used when test case is a BuildItem.SetName Test
            /// </summary>
            SetNameTest,

            /// <summary>
            /// Used when test case is a BuildItem.SetMetadataTest
            /// </summary>
            SetMetadataTest
        }

        /// <summary>
        /// Creates the engine and parent object. Also registers the mock logger.
        /// </summary>
        [SetUp()]
        public void Initialize()
        {
            ObjectModelHelpers.DeleteTempProjectDirectory();

            engine = new Engine();
            project = new Project(engine);
            logger = new MockLogger();
            project.ParentEngine.RegisterLogger(logger);
        }

        /// <summary>
        /// Unloads projects and un-registers logger.
        /// </summary>
        [TearDown()]
        public void Cleanup()
        {
            engine.UnloadProject(project);
            engine.UnloadAllProjects();
            engine.UnregisterAllLoggers();

            ObjectModelHelpers.DeleteTempProjectDirectory();
        }
        #endregion

        #region Constructor Tests
        /// <summary>
        /// Tests BuildItem Constructor 'public BuildItem(string itemName, string itemInclude)' with simple strings
        /// </summary>
        [Test]
        public void ConstructItemNameItemIncludeSimple()
        {
            BuildItem item = new BuildItem("n", "i");

            Assertion.AssertEquals("n", item.Name);
            Assertion.AssertEquals("i", item.FinalItemSpec);
        }

        /// <summary>
        /// Tests BuildItem Constructor 'public BuildItem(string itemName, string itemInclude)' with Empty String for itemName
        /// </summary>
        [Test]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void ConstructItemNameEmptyString()
        {
            BuildItem item = new BuildItem(String.Empty, "i");
        }

        /// <summary>
        /// Tests BuildItem Constructor 'public BuildItem(string itemName, string itemInclude)' with Empty String for itemInclude
        /// </summary>
        [Test]
        public void ConstructItemIncludeEmptyString()
        {
            BuildItem item = new BuildItem("n", String.Empty);

            Assertion.AssertEquals("n", item.Name);
            Assertion.AssertEquals(String.Empty, item.FinalItemSpec);
        }

        /// <summary>
        /// Tests BuildItem Constructor 'public BuildItem(string itemName, string itemInclude)' with null for itemInclude
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructItemIncludeNull()
        {
            BuildItem item = new BuildItem("n", (string)null);
        }

        /// <summary>
        /// Tests BuildItem Contructor with non valid xml name
        /// </summary>
        /// <remarks>cliffh and jaysh brought this up during dev10 development</remarks>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ConstructItemInvalidXml()
        {
            BuildItem item = new BuildItem("1invalid", "i");
        }

        /// <summary>
        /// Tests BuildItem Constructor 'public BuildItem(string itemName, string itemInclude)' by attempting to set itemName to all of the Reserved Names
        /// </summary>
        [Test]
        public void ConstructItemNameReservedNames()
        {
            VerifyReservedNamesForBuildItemNameThrowExpectedException(TypeOfTest.ConstructorTest);
        }

        /// <summary>
        /// Tests BuildItem Constructor 'public BuildItem(string itemName, string itemInclude)' by attempting to set itemName with any of the Escape characters
        /// </summary>
        [Test]
        public void ContructItemNameEscapableCharacters()
        {
            VerifyEscapeCharactersThrowExpectedException(TypeOfTest.ConstructorTest);
        }

        /// <summary>
        /// Tests BuildItem Constructor 'public BuildItem(string itemName, string itemInclude)' by setting itemInclude with all of the Escape characters
        /// </summary>
        [Test]
        public void ContructItemIncludeEscapableCharacters()
        {
            BuildItem item = new BuildItem("n", EscapableCharacters);
            Assertion.AssertEquals(EscapableCharacters, item.FinalItemSpec);
        }

        /// <summary>
        /// Tests BuildItem Constructor 'public BuildItem(string itemName, ITaskItem taskItem)' with simple strings
        /// </summary>
        [Test]
        public void ConstructItemNameTaskItemSimple()
        {
            MockTaskItem taskItem = new MockTaskItem();
            BuildItem item = new BuildItem("n", taskItem);

            Assertion.AssertEquals("n", item.Name);
            Assertion.AssertEquals("i", item.FinalItemSpec);
        }

        /// <summary>
        /// Tests BuildItem Constructor 'public BuildItem(string itemName, ITaskItem taskItem)' by passing in a null ITaskItem
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructTaskItemNull()
        {
            ITaskItem taskItem = null;
            BuildItem item = new BuildItem("n", taskItem);
        }

        /// <summary>
        /// Tests BuildItem Constructor case that after creating the BuildItem object, HasMetadata
        ///     will return false because it's not yet evaluated (special note in builditem.cs for this)
        /// </summary>
        [Test]
        public void ConstructHasMetadata()
        {
            BuildItem item = new BuildItem("m", "v");

            Assertion.AssertEquals(false, item.HasMetadata("m"));
            Assertion.AssertEquals(string.Empty, item.GetEvaluatedMetadata("m"));
            Assertion.AssertEquals(string.Empty, item.GetMetadata("m"));
            Assertion.AssertEquals(builtInMetadataNames.Length, item.MetadataCount);
            Assertion.AssertEquals(0, item.CustomMetadataCount);
        }
        #endregion

        #region Condition Tests
        /// <summary>
        /// Tests BuildItem.Condition Get for basic/simple case
        /// </summary>
        [Test]
        public void ConditionGetSimple()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i' Exclude='e' Condition=""'a' == 'b'"" />
                        </ItemGroup>
                    </Project>
                    ";

            project.LoadXml(projectContents);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals("'a' == 'b'", item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Get when no condition exists
        /// </summary>
        [Test]
        public void ConditionGetWhenNoCondition()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(String.Empty, item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Get when Condition is an empty string
        /// </summary>
        [Test]
        public void ConditionGetEmptyString()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i' Exclude='e' Condition="""" />
                        </ItemGroup>
                    </Project>
                    ";

            project.LoadXml(projectContents);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(String.Empty, item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Get from an imported Project
        /// </summary>
        [Test]
        public void ConditionGetFromImportedProject()
        {
            string importProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <nImported Include='iImported' Condition=""'a' == 'b'"" />
                        </ItemGroup>
                    </Project>
                ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");

            Assertion.AssertEquals("'a' == 'b'", item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Set when no provious exists
        /// </summary>
        [Test]
        public void ConditionSetWhenNoPreviousConditionExists()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.Condition = "'a' == 'aa'";

            Assertion.AssertEquals("'a' == 'aa'", item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Set when an existing condition exists, changing the condition
        /// </summary>
        [Test]
        public void ConditionSetOverExistingCondition()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItemWithCondition);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.Condition = "'true' == 't'";

            Assertion.AssertEquals("'true' == 't'", item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Set To Empty string
        /// </summary>
        [Test]
        public void ConditionSetToEmptyString()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItemWithCondition);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.Condition = String.Empty;

            Assertion.AssertEquals(String.Empty, item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Set to null
        /// </summary>
        [Test]
        public void ConditionSetToNull()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItemWithCondition);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.Condition = null;

            Assertion.AssertEquals(String.Empty, item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Set to Special Characters
        /// </summary>
        [Test]
        public void ConditionSetToSpecialCharacters()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItemWithCondition);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.Condition = SpecialCharacters;

            Assertion.AssertEquals(SpecialCharacters, item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Set to Escape Characters
        /// </summary>
        [Test]
        public void ConditionSetToEscapableCharacters()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItemWithCondition);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.Condition = EscapableCharacters;

            Assertion.AssertEquals(EscapableCharacters, item.Condition);
        }

        /// <summary>
        /// Tests BuildItem.Condition Set on an Imported Project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ConditionSetOnImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");
            item.Condition = "t";
        }

        /// <summary>
        /// Tests BuildItem.Condition Set, save to disk and verify
        /// </summary>
        [Test]
        public void ConditionSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItemWithCondition);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.Condition = "'t' == 'true'";

            string expectedProjectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i' Condition=""'t' == 'true'"" />
                        </ItemGroup>
                    </Project>
                    ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }
        #endregion

        #region Include/Exclude Tests
        /// <summary>
        /// Tests BuildItem.Include/Exclude Get for simple case
        /// </summary>
        [Test]
        public void IncludeExcludeGetSimple()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes);
            BuildItem item = GetSpecificBuildItem(project, "n1");

            Assertion.AssertEquals("i1", item.Include);
            Assertion.AssertEquals("e1", item.Exclude);
        }

        /// <summary>
        /// Tests BuildItem.Include/Exclude Set for simple case
        /// </summary>
        [Test]
        public void IncludeExcludeSetSimple()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes);
            BuildItem item = GetSpecificBuildItem(project, "n1");

            item.Include = "newinclude";
            item.Exclude = "newexclude";

            Assertion.AssertEquals("newinclude", item.Include);
            Assertion.AssertEquals("newexclude", item.Exclude);
        }

        /// <summary>
        /// Tests BuildItem.Include/Exclude Get on BuildItem that is from an imported Project
        /// </summary>
        [Test]
        public void IncludeExcludeGetFromImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes, null);
            BuildItem item = GetSpecificBuildItem(p, "n1");

            Assertion.AssertEquals("i1", item.Include);
            Assertion.AssertEquals("e1", item.Exclude);
        }

        /// <summary>
        /// Tests BuildItem.Include attempt Set for an Imported BuildItem 
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void IncludeSetImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes, null);
            BuildItem item = GetSpecificBuildItem(p, "n1");

            item.Include = "new";
        }

        /// <summary>
        /// Tests BuildItem.Exclude attempt Set for an Imported Builditem
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ExcludeSetImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes, null);
            BuildItem item = GetSpecificBuildItem(p, "n1");

            item.Exclude = "new";
        }

        /// <summary>
        /// Tests BuildItem.Include/Exclude Set to Empty String
        /// </summary>
        [Test]
        public void IncludeExcludeSetToEmptyString()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes);
            BuildItem item = GetSpecificBuildItem(project, "n1");

            item.Include = String.Empty;
            item.Exclude = String.Empty;

            Assertion.AssertEquals(String.Empty, item.Include);
            Assertion.AssertEquals(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Tests BuildItem.Include attempt to Set to Null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void IncludeSetToNull()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes);
            BuildItem item = GetSpecificBuildItem(project, "n1");

            item.Include = null;
        }

        /// <summary>
        /// Tests BuildItem.Exclude Set to null
        /// </summary>
        [Test]
        public void ExcludeSetToNull()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes);
            BuildItem item = GetSpecificBuildItem(project, "n1");

            item.Exclude = null;

            Assertion.AssertEquals(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Tests BuildItem.Exclude Get when No Exclude exists
        /// </summary>
        [Test]
        public void ExcludeGetExcludeWhenNoExcludeExists()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(String.Empty, item.Exclude);
        }

        /// <summary>
        /// Tests BuildItem.Include/Exclude Set then save to disk and verify
        /// </summary>
        [Test]
        public void IncludeExcludeSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes);
            BuildItem item = GetSpecificBuildItem(project, "n1");
            item.Include = "newinclude";
            item.Exclude = "newexclude";

            string expectedProjectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1 Include='newinclude' Exclude='newexclude' />
                            <n2 Include='i2' Exclude='e2' />
                            <n3 Include='i3' Exclude='e3' />
                            <n4 Include='i4' Exclude='e4' />
                        </ItemGroup>
                    </Project>
                    ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildItem.Include/Exclude Set to Special Characters
        /// </summary>
        [Test]
        public void IncludeExcludeSetToSpecialCharacters()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes);
            BuildItem item = GetSpecificBuildItem(project, "n1");
            item.Include = SpecialCharacters;
            item.Exclude = SpecialCharacters;

            Assertion.AssertEquals(SpecialCharacters, item.Include);
            Assertion.AssertEquals(SpecialCharacters, item.Exclude);
        }

        /// <summary>
        /// Tests BuildItem.Include/Exclude Set to Escape Characters
        /// </summary>
        [Test]
        public void IncludeExcludeSetToEscapableCharacters()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItemsIncludesExcludes);
            BuildItem item = GetSpecificBuildItem(project, "n1");
            item.Include = EscapableCharacters;
            item.Exclude = EscapableCharacters;

            Assertion.AssertEquals(EscapableCharacters, item.Include);
            Assertion.AssertEquals(EscapableCharacters, item.Exclude);
        }
        #endregion

        #region FinalItemSpec Tests
        /// <summary>
        /// Tests BuildItem.FinalItemSpec Get when only one BuildItem exists
        /// </summary>
        [Test]
        public void FinalItemSpecGetWhenOnlyOneBuildItem()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals("i", item.FinalItemSpec);
        }

        /// <summary>
        /// Tests BuildItem.FinalItemSpec Get when several BuildItems exist
        /// </summary>
        [Test]
        public void FinalItemSpecGetWhenMultipleBuildItems()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItems);
            BuildItem item = GetSpecificBuildItem(project, "n2");

            Assertion.AssertEquals("i2", item.FinalItemSpec);
        }

        /// <summary>
        /// Tests BuildItem.FinalItemSpec Get of a BuildItem that's from an imported project
        /// </summary>
        [Test]
        public void FinalItemSpecFromImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");

            Assertion.AssertEquals("iImported", item.FinalItemSpec);
        }
        #endregion

        #region IsImported Tests
        /// <summary>
        /// Tests BuildItem.IsImported when BuildItem does come from an imported project
        /// </summary>
        [Test]
        public void IsImportedExpectedTrue()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");

            Assertion.AssertEquals(true, item.IsImported);
        }

        /// <summary>
        /// Tests BuildItem.IsImported when BuildItem does not come from an imported project
        ///     and no projects are imported.
        /// </summary>
        [Test]
        public void IsImportedExpectedFalseNoImportsExist()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(false, item.IsImported);
        }

        /// <summary>
        /// Tests BuildItem.IsImported when specific BuildItem does not come from an imported project
        ///     but others are imported.
        /// </summary>
        [Test]
        public void IsImportedExpectedFalseImportsDoExist()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildItem item = GetSpecificBuildItem(p, "nMain");

            Assertion.AssertEquals(false, item.IsImported);
        }

        /// <summary>
        /// Tests BuildItem.IsImported when all we have is an in memory BuildItem
        /// </summary>
        [Test]
        public void IsImportedInMemoryBuildItem()
        {
            BuildItem item = new BuildItem("n", "i");
            Assertion.AssertEquals(false, item.IsImported);
        }
        #endregion

        #region Name Tests
        /// <summary>
        /// Tests BuildItem.Name Set itemName to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NameSetToNull()
        {
            BuildItem item = new BuildItem("n", "i");
            item.Name = null;
        }

        /// <summary>
        /// Tests BuildItem.Name Get when itemName is set to null.
        /// Verifying that Microsoft.Build.BuildEngine.Shared.InternalErrorException is thrown
        /// </summary>
        [Test]
        public void NameGetWhenNull()
        {
            BuildItemNameGetSetNullExpectedToThrow(new BuildItem((string)null, "i"), false);
        }

        /// <summary>
        /// Tests BuildItem.Name Set to null when it's been initialized to null
        /// Verifying that Microsoft.Build.BuildEngine.Shared.InternalErrorException is thrown
        /// </summary>
        [Test]
        public void NameSetToNullAfterInitializingToNull()
        {
            BuildItemNameGetSetNullExpectedToThrow(new BuildItem((string)null, "i"), true);
        }

        /// <summary>
        /// Tests BuildItem.Name Attempting to Set to all of the Reserved Names
        /// </summary>
        [Test]
        public void NameSetReservedNames()
        {
            VerifyReservedNamesForBuildItemNameThrowExpectedException(TypeOfTest.SetNameTest);
        }

        /// <summary>
        /// Tests BuildItem.Name Get when only one BuildItem exists
        /// </summary>
        [Test]
        public void NameGetWithOneBuildItem()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals("n", item.Name);
        }

        /// <summary>
        /// Tests BuildItem.Name when many BuildItems exist
        /// </summary>
        [Test]
        public void NameGetWithManyBuildItems()
        {
            project.LoadXml(ProjectContentOneItemGroupWithSeveralBuildItems);
            BuildItem item = GetSpecificBuildItem(project, "n2");

            Assertion.AssertEquals("n2", item.Name);
        }

        /// <summary>
        /// Tests BuildItem.Name Set when only one BuildItem exists
        /// </summary>
        [Test]
        public void NameSetWithOneBuildItem()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.Name = "new";

            Assertion.AssertEquals("new", item.Name);
        }

        /// <summary>
        /// Tests BuildItem.Name Set, save to disk and verify
        /// </summary>
        [Test]
        public void NameSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.Name = "new";

            string expectedProjectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <new Include='i' />
                        </ItemGroup>
                    </Project>
                    ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildItem.Name Get from an imported project
        /// </summary>
        [Test]
        public void NameGetFromImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");

            Assertion.AssertEquals("nImported", item.Name);
        }

        /// <summary>
        /// Tests BuildItem.Name Set on an imported Project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void NameSetFromImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");
            item.Name = "new";
        }
        #endregion

        #region CustomMetadataCount & CustomMetadataNames Tests
        /// <summary>
        /// Tests BuildItem.CustomMetadataCount/CustomMetadataNames when some custom metadata exists
        /// </summary>
        [Test]
        public void CustomMetadataCountNamesWithCustomMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemWithCustomMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            List<string> list = (List<string>)item.CustomMetadataNames;

            Assertion.AssertEquals(3, item.CustomMetadataCount);
            Assertion.AssertEquals(true, list.Contains("a_Meta"));
            Assertion.AssertEquals(true, list.Contains("b_Meta"));
            Assertion.AssertEquals(true, list.Contains("c_Meta"));
            VerifyBuiltInMetadataNamesExistOrNot(list, false);
        }

        /// <summary>
        /// Tests BuildItem.CustomMetadataCount/CustomMetadataNames when no custom metadata exists
        /// </summary>
        [Test]
        public void CustomMetadataCountNamesWithNoCustomMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");
            List<string> list = (List<string>)item.CustomMetadataNames;

            Assertion.AssertEquals(0, item.CustomMetadataCount);
            Assertion.AssertEquals(0, list.Count);
        }

        /// <summary>
        /// Tests BuildItem.CustomMetadataCount/CustomMetadataNames when all Custom Metadata
        ///     comes only from an imported project.
        /// </summary>
        [Test]
        public void CustomMetadataCountNamesWhenComingOnlyFromImportedProject()
        {
            string importProjectContents = @" 
                        <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup>
                                <nImported Include='iImported'>
                                    <ImportedMeta>ImportedMetaValue</ImportedMeta>
                                </nImported>
                            </ItemGroup>
                        </Project>
                    ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, null);

            BuildItem itemImport = GetSpecificBuildItem(p, "nImported");
            List<string> listImport = (List<string>)itemImport.CustomMetadataNames;
            Assertion.AssertEquals(1, itemImport.CustomMetadataCount);
            Assertion.AssertEquals(true, listImport.Contains("ImportedMeta"));
            VerifyBuiltInMetadataNamesExistOrNot(listImport, false);

            BuildItem itemParent = GetSpecificBuildItem(p, "nMain");
            List<string> listParent = (List<string>)itemParent.CustomMetadataNames;
            Assertion.AssertEquals(0, itemParent.CustomMetadataCount);
            VerifyBuiltInMetadataNamesExistOrNot(listParent, false);
        }

        /// <summary>
        /// Tests BuildItem.CustomMetadataCount/CustomMetadataNames when all Custom Metadata
        ///     comes only from the parent project, when a project is imported.
        /// </summary>
        [Test]
        public void CustomMetadataCountNamesWhenComingOnlyFromParentProjectNoneFromImport()
        {
            string parentProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nMain Include='iMain'>
                                        <MainMeta>MainMetaValue</MainMeta>
                                    </nMain>
                                </ItemGroup>
                                <Import Project='import.proj' />
                            </Project>
                        ";

            Project p = GetProjectThatImportsAnotherProject(null, parentProjectContents);

            BuildItem itemImport = GetSpecificBuildItem(p, "nImported");
            List<string> listImport = (List<string>)itemImport.CustomMetadataNames;
            Assertion.AssertEquals(0, itemImport.CustomMetadataCount);
            VerifyBuiltInMetadataNamesExistOrNot(listImport, false);

            BuildItem itemParent = GetSpecificBuildItem(p, "nMain");
            List<string> listParent = (List<string>)itemParent.CustomMetadataNames;
            Assertion.AssertEquals(1, itemParent.CustomMetadataCount);
            Assertion.AssertEquals(true, listParent.Contains("MainMeta"));
            VerifyBuiltInMetadataNamesExistOrNot(listParent, false);
        }

        /// <summary>
        /// Tests BuildItem.CustomMetadataCount/CustomMetadataNames when all Custom Metadata
        ///     comes from both the parent project and an imported project.
        /// </summary>
        [Test]
        public void CustomMetadataCountNamesWhenComingFromBothParentAndImportedProject()
        {
            string importProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nImported Include='iImported'>
                                        <ImportedMeta>ImportedMetaValue</ImportedMeta>
                                    </nImported>
                                </ItemGroup>
                            </Project>
                        ";

            string parentProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nMain Include='iMain'>
                                        <MainMeta>MainMetaValue</MainMeta>
                                    </nMain>
                                </ItemGroup>
                                <Import Project='import.proj' />
                            </Project>
                        ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, parentProjectContents);

            BuildItem itemImport = GetSpecificBuildItem(p, "nImported");
            List<string> listImport = (List<string>)itemImport.CustomMetadataNames;
            Assertion.AssertEquals(1, itemImport.CustomMetadataCount);
            Assertion.AssertEquals(true, listImport.Contains("ImportedMeta"));
            VerifyBuiltInMetadataNamesExistOrNot(listImport, false);

            BuildItem itemParent = GetSpecificBuildItem(p, "nMain");
            List<string> listParent = (List<string>)itemParent.CustomMetadataNames;
            Assertion.AssertEquals(1, itemParent.CustomMetadataCount);
            Assertion.AssertEquals(true, listParent.Contains("MainMeta"));
            VerifyBuiltInMetadataNamesExistOrNot(listParent, false);
        }

        /// <summary>
        /// Tests BuildItem.CustomMetadataCount/CustomMetadataNames when no Custom Metadata
        ///     comes from the parent project or the imported project.
        /// </summary>
        [Test]
        public void CustomMetadataCountNamesWhenNoneInBothParentAndImportedProject()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);

            BuildItem itemImport = GetSpecificBuildItem(p, "nImported");
            List<string> listImport = (List<string>)itemImport.CustomMetadataNames;
            Assertion.AssertEquals(0, itemImport.CustomMetadataCount);
            VerifyBuiltInMetadataNamesExistOrNot(listImport, false);

            BuildItem itemParent = GetSpecificBuildItem(p, "nMain");
            List<string> listParent = (List<string>)itemParent.CustomMetadataNames;
            Assertion.AssertEquals(0, itemParent.CustomMetadataCount);
            VerifyBuiltInMetadataNamesExistOrNot(listParent, false);
        }
        #endregion

        #region MetadataCount & MetadataNames Tests
        /// <summary>
        /// Tests BuildItem.MetadataCount/MetadataNames when some custom Metadata exists
        /// </summary>
        [Test]
        public void MetadataCountNamesWithCustomMetadata()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                                <a_Meta>a</a_Meta>
                                <b_Meta>b</b_Meta>
                                <c_Meta>c</c_Meta>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";

            project.LoadXml(projectContents);
            BuildItem item = GetSpecificBuildItem(project, "n");
            List<string> list = (List<string>)item.MetadataNames;

            Assertion.AssertEquals(builtInMetadataNames.Length + 3, item.MetadataCount);
            Assertion.AssertEquals(true, list.Contains("a_Meta"));
            Assertion.AssertEquals(true, list.Contains("b_Meta"));
            Assertion.AssertEquals(true, list.Contains("c_Meta"));
            VerifyBuiltInMetadataNamesExistOrNot(list, true);
        }

        /// <summary>
        /// Tests BuildItem.MetadataCount/MetadataNames when no custom Metadata exists
        /// </summary>
        [Test]
        public void MetadataCountNamesWithNoCustomMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");
            List<string> list = (List<string>)item.MetadataNames;

            Assertion.AssertEquals(builtInMetadataNames.Length, item.MetadataCount);
            VerifyBuiltInMetadataNamesExistOrNot(list, true);
        }

        /// <summary>
        /// Tests BuildItem.MetadataCount/MetadataNames if an Imported project with no custom Metadata
        /// </summary>
        [Test]
        public void MetadataCountNamesFromImportedProjectNoCustomMetadata()
        {
            Project p = GetProjectThatImportsAnotherProject(null, null);

            BuildItem itemImport = GetSpecificBuildItem(p, "nImported");
            List<string> listImport = (List<string>)itemImport.MetadataNames;
            Assertion.AssertEquals(builtInMetadataNames.Length, itemImport.MetadataCount);
            VerifyBuiltInMetadataNamesExistOrNot(listImport, true);

            BuildItem itemParent = GetSpecificBuildItem(p, "nMain");
            List<string> listParent = (List<string>)itemParent.MetadataNames;
            Assertion.AssertEquals(builtInMetadataNames.Length, itemParent.MetadataCount);
            VerifyBuiltInMetadataNamesExistOrNot(listParent, true);
        }

        /// <summary>
        /// Tests BuildItem.MetadataCount/MetadataNames if an Imported project with custom Metadata
        /// </summary>
        [Test]
        public void MetadataCountNamesFromImportedProjectWithCustomMetadata()
        {
            string importProjectContents = @" 
                        <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup>
                                <nImported Include='iImported'>
                                    <ImportedMeta>ImportedMetaValue</ImportedMeta>
                                </nImported>
                            </ItemGroup>
                        </Project>
                    ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, null);

            BuildItem itemImport = GetSpecificBuildItem(p, "nImported");
            List<string> listImport = (List<string>)itemImport.MetadataNames;
            Assertion.AssertEquals(true, listImport.Contains("ImportedMeta"));
            Assertion.AssertEquals(builtInMetadataNames.Length + 1, itemImport.MetadataCount);
            VerifyBuiltInMetadataNamesExistOrNot(listImport, true);

            BuildItem itemParent = GetSpecificBuildItem(p, "nMain");
            List<string> listParent = (List<string>)itemParent.MetadataNames;
            Assertion.AssertEquals(builtInMetadataNames.Length, itemParent.MetadataCount);
            VerifyBuiltInMetadataNamesExistOrNot(listParent, true);
        }
        #endregion

        #region CopyCustomMetadataTo Tests
        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo a BuildItem that doesn't contain any Custom Metadata
        /// </summary>
        [Test]
        public void CopyCustomMetadataToBuildItemWithNoCustomMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemWithCustomMetadata);
            BuildItem itemFrom = GetSpecificBuildItem(project, "n");
            BuildItem itemTo = new BuildItem("otherN", "otherI");
            itemFrom.CopyCustomMetadataTo(itemTo);

            List<string> listOther = (List<string>)itemTo.CustomMetadataNames;
            Assertion.AssertEquals(3, itemTo.CustomMetadataCount);
            Assertion.AssertEquals(true, listOther.Contains("a_Meta"));
            Assertion.AssertEquals(true, listOther.Contains("b_Meta"));
            Assertion.AssertEquals(true, listOther.Contains("c_Meta"));
        }

        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo a null BuildItem
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CopyCustomMetadataToNullBuildItem()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemWithCustomMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            BuildItem itemOther = null;

            item.CopyCustomMetadataTo(itemOther);
        }

        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo by copying to self
        /// </summary>
        [Test]
        public void CopyCustomMetadataToSelf()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemWithCustomMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.CopyCustomMetadataTo(item);

            List<string> list = (List<string>)item.CustomMetadataNames;
            Assertion.AssertEquals(3, item.CustomMetadataCount);
            Assertion.AssertEquals(true, list.Contains("a_Meta"));
            Assertion.AssertEquals(true, list.Contains("b_Meta"));
            Assertion.AssertEquals(true, list.Contains("c_Meta"));
        }

        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo Another BuildItem within the same project
        ///     that doesn't contain any Custom Metadata.
        /// </summary>
        [Test]
        public void CopyCustomMetadataToAnotherBuildItemWithNoCustomMetadataInSameProject()
        {
            string projectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1 Include='i1'>
                                <a_Meta>a</a_Meta>
                                <b_Meta>b</b_Meta>
                            </n1>
                            <n2 Include='i2' />
                        </ItemGroup>
                    </Project>
                ";

            project.LoadXml(projectContents);
            BuildItem itemFrom = GetSpecificBuildItem(project, "n1");
            BuildItem itemTo = GetSpecificBuildItem(project, "n2");
            itemFrom.CopyCustomMetadataTo(itemTo);

            List<string> list = (List<string>)itemTo.CustomMetadataNames;
            Assertion.AssertEquals(2, itemTo.CustomMetadataCount);
            Assertion.AssertEquals(true, list.Contains("a_Meta"));
            Assertion.AssertEquals(true, list.Contains("b_Meta"));
        }

        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo Another BuildItem that contains different
        ///     Custom Metadata.
        /// </summary>
        [Test]
        public void CopyCustomMetadataToAntherBuildItemWithAllOtherDifferentCustomMetadata()
        {
            string projectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1 Include='i1'>
                                <a_Meta>a</a_Meta>
                                <b_Meta>b</b_Meta>
                            </n1>
                            <n2 Include='i2'>
                                <c_Meta>c</c_Meta>
                                <d_Meta>d</d_Meta>
                            </n2>
                        </ItemGroup>
                    </Project>
                ";

            project.LoadXml(projectContents);
            BuildItem itemFrom = GetSpecificBuildItem(project, "n1");
            BuildItem itemTo = GetSpecificBuildItem(project, "n2");
            itemFrom.CopyCustomMetadataTo(itemTo);

            List<string> list = (List<string>)itemTo.CustomMetadataNames;
            Assertion.AssertEquals(4, itemTo.CustomMetadataCount);
            Assertion.AssertEquals(true, list.Contains("a_Meta"));
            Assertion.AssertEquals(true, list.Contains("b_Meta"));
            Assertion.AssertEquals(true, list.Contains("c_Meta"));
            Assertion.AssertEquals(true, list.Contains("d_Meta"));
        }

        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo Another BuildItem that contains the same
        ///     Custom Metadata.
        /// </summary>
        [Test]
        public void CopyCustomMetadataToAnotherBuildItemWithAllOtherSameCustomMetadata()
        {
            string projectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1 Include='i1'>
                                <a_Meta>a</a_Meta>
                                <b_Meta>b</b_Meta>
                            </n1>
                            <n2 Include='i2'>
                                <a_Meta>a</a_Meta>
                                <b_Meta>other</b_Meta>
                            </n2>
                        </ItemGroup>
                    </Project>
                ";

            project.LoadXml(projectContents);
            BuildItem itemFrom = GetSpecificBuildItem(project, "n1");
            BuildItem itemTo = GetSpecificBuildItem(project, "n2");
            itemFrom.CopyCustomMetadataTo(itemTo);

            List<string> list = (List<string>)itemTo.CustomMetadataNames;
            Assertion.AssertEquals(2, itemTo.CustomMetadataCount);
            Assertion.AssertEquals(true, list.Contains("a_Meta"));
            Assertion.AssertEquals(true, list.Contains("b_Meta"));
            Assertion.AssertEquals("a", itemTo.GetMetadata("a_Meta"));
            Assertion.AssertEquals("b", itemTo.GetMetadata("b_Meta"));
        }

        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo Another BuildItem that contains some of the
        ///     same Custom Metadata and some different.
        /// </summary>
        [Test]
        public void CopyCustomMetadataToAnotherBuildItemWithSomeOtherSameCustomMetadata()
        {
            string projectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1 Include='i1'>
                                <a_Meta>a</a_Meta>
                                <b_Meta>b</b_Meta>
                                <c_Meta>c</c_Meta>
                                <d_Meta>d</d_Meta>
                            </n1>
                            <n2 Include='i2'>
                                <b_Meta>b</b_Meta>
                                <c_Meta>c</c_Meta>
                                <e_Meta>e</e_Meta>
                                <f_Meta>f</f_Meta>
                            </n2>
                        </ItemGroup>
                    </Project>
                ";

            project.LoadXml(projectContents);
            BuildItem itemFrom = GetSpecificBuildItem(project, "n1");
            BuildItem itemTo = GetSpecificBuildItem(project, "n2");
            itemFrom.CopyCustomMetadataTo(itemTo);

            List<string> list = (List<string>)itemTo.CustomMetadataNames;
            Assertion.AssertEquals(6, itemTo.CustomMetadataCount);
            Assertion.AssertEquals(true, list.Contains("a_Meta"));
            Assertion.AssertEquals(true, list.Contains("b_Meta"));
            Assertion.AssertEquals(true, list.Contains("c_Meta"));
            Assertion.AssertEquals(true, list.Contains("d_Meta"));
            Assertion.AssertEquals(true, list.Contains("e_Meta"));
            Assertion.AssertEquals(true, list.Contains("f_Meta"));
        }

        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo Another BuildItem that is not Imported, from
        ///     a BuildItem that is Imported.
        /// </summary>
        [Test]
        public void CopyCustomMetadataToNonImportedBuildItemFromAnImportedBuildItem()
        {
            string importProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nImported Include='iImported'>
                                        <ImportedMeta>ImportedMetaValue</ImportedMeta>
                                    </nImported>
                                </ItemGroup>
                            </Project>
                        ";

            string parentProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nMain Include='iMain'>
                                        <MainMeta>MainMetaValue</MainMeta>
                                    </nMain>
                                </ItemGroup>
                                <Import Project='import.proj' />
                            </Project>
                        ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, parentProjectContents);
            BuildItem itemFrom = GetSpecificBuildItem(p, "nImported");
            BuildItem itemTo = GetSpecificBuildItem(p, "nMain");
            itemFrom.CopyCustomMetadataTo(itemTo);

            List<string> list = (List<string>)itemTo.CustomMetadataNames;
            Assertion.AssertEquals(2, itemTo.CustomMetadataCount);
            Assertion.AssertEquals(true, list.Contains("MainMeta"));
            Assertion.AssertEquals(true, list.Contains("ImportedMeta"));
        }

        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo A BuildItem that is Imported.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void CopyCustomMetadataToImportedBuildItem()
        {
            string importProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nImported Include='iImported'>
                                        <ImportedMeta>ImportedMetaValue</ImportedMeta>
                                    </nImported>
                                </ItemGroup>
                            </Project>
                        ";

            string parentProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nMain Include='iMain'>
                                        <MainMeta>MainMetaValue</MainMeta>
                                    </nMain>
                                </ItemGroup>
                                <Import Project='import.proj' />
                            </Project>
                        ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, parentProjectContents);
            BuildItem itemFrom = GetSpecificBuildItem(p, "nMain");
            BuildItem itemTo = GetSpecificBuildItem(p, "nImported");

            itemFrom.CopyCustomMetadataTo(itemTo);
        }

        /// <summary>
        /// Tests BuildItem.CopyCustomMetadataTo 
        /// </summary>
        [Test]
        public void CopyCustomMetadataToAnotherBuildItemThenSaveToDisk()
        {
            string projectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1 Include='i1'>
                                <a_Meta>a</a_Meta>
                                <b_Meta>b</b_Meta>
                            </n1>
                            <n2 Include='i2'>
                                <c_Meta>c</c_Meta>
                            </n2>
                        </ItemGroup>
                    </Project>
                ";

            project.LoadXml(projectContents);
            BuildItem itemFrom = GetSpecificBuildItem(project, "n1");
            BuildItem itemTo = GetSpecificBuildItem(project, "n2");
            itemFrom.CopyCustomMetadataTo(itemTo);

            string expectedProjectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1 Include='i1'>
                                <a_Meta>a</a_Meta>
                                <b_Meta>b</b_Meta>
                            </n1>
                            <n2 Include='i2'>
                                <c_Meta>c</c_Meta>
                                <a_Meta>a</a_Meta>
                                <b_Meta>b</b_Meta>
                            </n2>
                        </ItemGroup>
                    </Project>
                    ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }
        #endregion

        #region GetEvaluatedMetadata Tests
        /// <summary>
        /// Tests BuildItem.GetEvaluatedMetadata basic cases
        /// </summary>
        [Test]
        public void GetEvaluatedMetadataSimple()
        {
            string projectContents = @" 
                        <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup>
                                <n Include='i' >
                                    <m1>v</m1>
                                    <m2>$(p)</m2>
                                    <m3></m3>
                                </n>
                            </ItemGroup>
                            <PropertyGroup>
                                <p>p1</p>
                            </PropertyGroup>
                        </Project>
                    ";

            project.LoadXml(projectContents);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals("v", item.GetEvaluatedMetadata("m1"));
            Assertion.AssertEquals("$(p)", item.GetMetadata("m2"));
            Assertion.AssertEquals("p1", item.GetEvaluatedMetadata("m2"));
            Assertion.AssertEquals(String.Empty, item.GetEvaluatedMetadata("m3"));
        }

        /// <summary>
        /// Tests BuildItem.GetEvaluatedMetadata when BuildItem comes from an imported project
        /// </summary>
        [Test]
        public void GetEvaluatedMetadataFromImportedProject()
        {
            string importProjectContents = @" 
                        <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup>
                                <nImported Include='iImported'>
                                    <m1>v</m1>
                                    <m2>$(p)</m2>
                                </nImported>
                            </ItemGroup>
                        </Project>
                    ";

            string parentProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <nMain Include='iMain' />
                        </ItemGroup>
                        <Import Project='import.proj' />
                        <PropertyGroup>
                            <p>p1</p>
                        </PropertyGroup>
                    </Project>
                ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, parentProjectContents);
            BuildItem item = GetSpecificBuildItem(p, "nImported");

            Assertion.AssertEquals("$(p)", item.GetMetadata("m2"));
            Assertion.AssertEquals("p1", item.GetEvaluatedMetadata("m2"));
        }

        /// <summary>
        /// Tests BuildItem.GetEvaluatedMetadata when no property group exists to actually evaluate the Metadata against
        /// </summary>
        [Test]
        public void GetEvaluatedMetadataNoPropertyToEvaluateAgainst()
        {
            string projectContents = @" 
                        <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup>
                                <n Include='i' >
                                    <m>$(p)</m>
                                </n>
                            </ItemGroup>
                        </Project>
                    ";

            project.LoadXml(projectContents);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(String.Empty, item.GetEvaluatedMetadata("m"));
        }
        #endregion

        #region GetMetadata Tests
        /// <summary>
        /// Tests BuildItem.GetMetadata simple/basic case
        /// </summary>
        [Test]
        public void GetMetadataSimple()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals("value", item.GetMetadata("Meta"));
        }

        /// <summary>
        /// Tests BuildItem.GetMetadata when Metadata Value is an empty string
        /// </summary>
        [Test]
        public void GetMetadataWhenValueIsEmptyString()
        {
            string projectContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                                <Meta></Meta>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";

            project.LoadXml(projectContent);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals("", item.GetMetadata("Meta"));
        }

        /// <summary>
        /// Tests BuildItem.GetMetadata when Metadate Value contains Special Characters
        /// </summary>
        [Test]
        public void GetMetadataWhenValueContainsSpecialCharacters()
        {
            string projectContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                                <Meta>%24%40%3b%5c%25</Meta>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";

            project.LoadXml(projectContent);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(SpecialCharacters, item.GetMetadata("Meta"));
        }

        /// <summary>
        /// Tests BuildItem.GetMetadata when Metadata Value contains Escape Characters
        /// </summary>
        [Test]
        public void GetMetadataWhenValueContainsEscapableCharacters()
        {
            string projectContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                                <Meta>%*?@$();\</Meta>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";

            project.LoadXml(projectContent);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(EscapableCharacters, item.GetMetadata("Meta"));
        }

        /// <summary>
        /// Tests BuildItem.GetMetadata when Metadata comes from an imported Project
        /// </summary>
        [Test]
        public void GetMetadataWhenMetadataImportedProject()
        {
            string importProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nImported Include='iImported'>
                                        <ImportedMeta>ImportedMetaValue</ImportedMeta>
                                    </nImported>
                                </ItemGroup>
                            </Project>
                        ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");

            Assertion.AssertEquals("ImportedMetaValue", item.GetMetadata("ImportedMeta"));
        }

        /// <summary>
        /// Tests BuildItem.GetMetadata of a non-existing Metadata
        /// </summary>
        [Test]
        public void GetMetadataOfMetadataThatDoesNotExist()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(String.Empty, item.GetMetadata("Not"));
        }

        /// <summary>
        /// Tests BuildItem.GetMetadata of a Built-In Metadata
        /// </summary>
        [Test]
        public void GetMetadataFromBuiltInMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals("i", item.GetMetadata(builtInMetadataNames[2]));
        }
        #endregion

        #region HasMetadata Tests
        /// <summary>
        /// Tests BuildItem.HasMetadata when BuildItem has custom Metadata
        /// </summary>
        [Test]
        public void HasMetadataOnCustomMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemWithCustomMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(true, item.HasMetadata("a_Meta"));
            VerifyHasMetaDataOnBuiltInMetadata(item);
        }

        /// <summary>
        /// Tests BuildItem.HasMetadata when BuildItem only has Built in Metadata item (no custom)
        /// </summary>
        [Test]
        public void HasMetadataOnBuiltInMetadataWithNoCustomMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");
            VerifyHasMetaDataOnBuiltInMetadata(item);
        }

        /// <summary>
        /// Tests BuildItem.HasMetadata on Metadata that doesn't actually exist
        /// </summary>
        [Test]
        public void HasMetadataOnNonExistingMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupWithOneBuildItem);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(false, item.HasMetadata("not"));
        }

        /// <summary>
        /// Tests BuildItem.HasMetadata on custom Metadata that has no value
        /// </summary>
        [Test]
        public void HasMetadataOnCustomMetadataWithNoValue()
        {
            string projectContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                                <Meta></Meta>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";

            project.LoadXml(projectContent);
            BuildItem item = GetSpecificBuildItem(project, "n");

            Assertion.AssertEquals(true, item.HasMetadata("Meta"));
        }

        /// <summary>
        /// Tests BuildItem.HasMetadata on built in Metadata from an imported project
        /// </summary>
        [Test]
        public void HasMetadataFromImportedProject()
        {
            string importProjectContent = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                                <Meta></Meta>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";
            Project p = GetProjectThatImportsAnotherProject(importProjectContent, null);
            BuildItem item = GetSpecificBuildItem(p, "n");

            Assertion.AssertEquals(true, item.HasMetadata("Meta"));
            VerifyHasMetaDataOnBuiltInMetadata(item);
        }
        #endregion

        #region RemoveMetadata Tests
        /// <summary>
        /// Tests BuildItem.RemoveMetadata for simple/basic case
        /// </summary>
        [Test]
        public void RemoveMetadataSimple()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.RemoveMetadata("Meta");

            Assertion.AssertEquals(false, item.HasMetadata("Meta"));
        }

        /// <summary>
        /// Tests BuildItem.RemoveMetadata of a non existing Metadata, verify no exceptions are thrown.
        /// </summary>
        [Test]
        public void RemoveMetadataOfNonExistingMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.RemoveMetadata("Not");

            Assertion.AssertEquals(false, item.HasMetadata("Not"));
            Assertion.AssertEquals("value", item.GetMetadata("Meta"));
        }

        /// <summary>
        /// Tests BuildItem.RemoveMetadata of all built in Metadata, expected to fail
        /// </summary>
        [Test]
        public void RemoveMetadataOfBuiltInMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");

            foreach (string s in builtInMetadataNames)
            {
                try
                {
                    item.RemoveMetadata(s);
                    Assertion.Fail(String.Format("Built In Metadata '{0}' didn't throw the expected ArgumentException", s));
                }
                catch (ArgumentException expected)
                {
                    Assertion.AssertEquals(true, expected.Message.Contains(s));
                }
            }
        }

        /// <summary>
        /// Tests BuildItem.RemoveMetadata of an imported Metadata, expected to fail
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveMetadataFromImportedMetadata()
        {
            string importProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nImported Include='iImported'>
                                        <ImportedMeta>ImportedMetaValue</ImportedMeta>
                                    </nImported>
                                </ItemGroup>
                            </Project>
                        ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");

            item.RemoveMetadata("ImportedMeta");
        }

        /// <summary>
        /// Tests BuildItem.RemoveMetadata after removal of metadata, save to disk and verify
        /// </summary>
        [Test]
        public void RemoveMetadataSaveToDiskAndVerify()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.RemoveMetadata("Meta");

            string expectedProjectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }
        #endregion

        #region SetMetadata Tests
        /// <summary>
        /// Tests BuildItem.SetMetadata simple/basic case
        /// </summary>
        [Test]
        public void SetMetadataSimple()
        {
            BuildItem item = new BuildItem("n", "i");
            item.SetMetadata("m", "v");

            Assertion.AssertEquals(true, item.HasMetadata("m"));
            Assertion.AssertEquals("v", item.GetMetadata("m"));
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata on non-existing metadata (create new metadata)
        /// </summary>
        [Test]
        public void SetMetadataOnNonExistingMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata("m", "v");

            Assertion.AssertEquals(true, item.HasMetadata("m"));
            Assertion.AssertEquals("v", item.GetMetadata("m"));
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata on an existing metadata (change metadata)
        /// </summary>
        [Test]
        public void SetMetadataOnExistingMetadata()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata("Meta", "v");

            Assertion.AssertEquals("v", item.GetMetadata("Meta"));
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata setting the metadata name to an empty string
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetMetadataNameToEmptyString()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata(String.Empty, "v");
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata setting the metadata value to an empty string
        /// </summary>
        [Test]
        public void SetMetadataValueToEmptyString()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata("m", String.Empty);

            Assertion.AssertEquals(true, item.HasMetadata("m"));
            Assertion.AssertEquals(String.Empty, item.GetMetadata("m"));
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata setting the metadata name to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetMetadataNameToNull()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata(null, "v");
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata setting the metadata value to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetMetadataValueToNull()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata("m", null);
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata on an Imported Project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetMetadataOnImportedProject()
        {
            string importProjectContents = @" 
                            <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                                <ItemGroup>
                                    <nImported Include='iImported'>
                                        <ImportedMeta>ImportedMetaValue</ImportedMeta>
                                    </nImported>
                                </ItemGroup>
                            </Project>
                        ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");
            item.SetMetadata("m", "v");
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata Name with special characters
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void SetMetadataNameWithSpecialCharacters()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata(SpecialCharacters, "v");
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata Name with Reserved Name
        /// </summary>
        [Test]
        public void SetMetadataNameWithReservedName()
        {
            VerifyReservedNamesForBuildItemNameThrowExpectedException(TypeOfTest.SetMetadataTest);
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata Name with Escape Characters
        /// </summary>
        [Test]
        public void SetMetadataNameWithEscapableCharacters()
        {
            VerifyEscapeCharactersThrowExpectedException(TypeOfTest.SetMetadataTest);
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata Value with Escape Characters
        /// </summary>
        [Test]
        public void SetMetadataValueWithEscapableCharacters()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata("m", EscapableCharacters);

            Assertion.AssertEquals(true, item.HasMetadata("m"));
            Assertion.AssertEquals(EscapableCharacters, item.GetMetadata("m"));
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata, Save to disk and verify
        /// </summary>
        [Test]
        public void SetMetadataSaveToDisk()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata("Meta", "new");
            item.SetMetadata("m", "v");

            string expectedProjectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n Include='i'>
                                <Meta>new</Meta>
                                <m>v</m>
                            </n>
                        </ItemGroup>
                    </Project>
                    ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }

        /// <summary>
        /// Tests BuildItem.SetMetadata TreatMetadataValueAsLiteral True/False
        /// </summary>
        [Test]
        public void SetMetadataTreatMetadataValueAsLiteral()
        {
            project.LoadXml(ProjectContentOneItemGroupOneBuildItemOneMetadata);
            BuildItem item = GetSpecificBuildItem(project, "n");
            item.SetMetadata("m1", @"%*?@$();\", true);
            item.SetMetadata("m2", @"%*?@$();\", false);

            Assertion.AssertEquals(@"%25%2a%3f%40%24%28%29%3b\", item.GetMetadata("m1"));
            Assertion.AssertEquals(@"%*?@$();\", item.GetMetadata("m2"));
        }
        #endregion

        #region Clone Tests
        /// <summary>
        /// Tests BuildItem.Clone for a BuildItem that's backed by XML
        /// </summary>
        [Test]
        public void CloneBackedByXml()
        {
            string projectContents = @" 
                        <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup>
                                <n Include='i' Exclude='e' Condition=""'a' == 'b'"" >
                                    <meta>value</meta>
                                </n>
                            </ItemGroup>
                        </Project>
                    ";

            project.LoadXml(projectContents);
            BuildItem item = GetSpecificBuildItem(project, "n");
            BuildItem clone = item.Clone();

            Assertion.AssertEquals("n", clone.Name);
            Assertion.AssertEquals("i", clone.Include);
            Assertion.AssertEquals("e", clone.Exclude);
            Assertion.AssertEquals("i", clone.FinalItemSpec);
            Assertion.AssertEquals("'a' == 'b'", clone.Condition);
            Assertion.AssertEquals("value", clone.GetMetadata("meta"));
            Assertion.AssertEquals(1, clone.CustomMetadataCount);
            Assertion.AssertEquals(builtInMetadataNames.Length + 1, clone.MetadataCount);

            clone.SetMetadata("newMeta", "newValue");

            Assertion.AssertEquals(true, clone.HasMetadata("newMeta"));
            Assertion.AssertEquals(true, item.HasMetadata("newMeta"));
        }

        /// <summary>
        /// Tests BuildItem.Clone for a Virtual BuildItem
        /// </summary>
        [Test]
        public void CloneVirtual()
        {
            BuildItem item = new BuildItem("n", "i");
            item.SetMetadata("m1", "v1");
            item.SetMetadata("m2", "v2");
            BuildItem clone = item.Clone();

            Assertion.AssertEquals("v1", clone.GetMetadata("m1"));
            Assertion.AssertEquals("v2", clone.GetMetadata("m2"));

            clone.SetMetadata("m2", "newValue");

            Assertion.AssertEquals("v2", item.GetMetadata("m2"));
            Assertion.AssertEquals("newValue", clone.GetMetadata("m2"));
        }

        /// <summary>
        /// Tests BuildItem.Clone of an Imported BuildItem
        /// </summary>
        [Test]
        public void CloneImportedBuildItem()
        {
            string importProjectContents = @" 
                        <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                            <ItemGroup>
                                <nImported Include='iImported'>
                                    <ImportedMeta>ImportedMetaValue</ImportedMeta>
                                </nImported>
                            </ItemGroup>
                        </Project>
                    ";

            Project p = GetProjectThatImportsAnotherProject(importProjectContents, null);
            BuildItem item = GetSpecificBuildItem(p, "nImported");
            BuildItem clone = item.Clone();

            Assertion.AssertEquals("ImportedMetaValue", clone.GetMetadata("ImportedMeta"));
            Assertion.AssertEquals(true, clone.IsImported);
        }

        /// <summary>
        /// Tests BuildItem.Clone of a null BuildItem
        /// </summary>
        [Test]
        [ExpectedException(typeof(NullReferenceException))]
        public void CloneNull()
        {
            BuildItem item = null;
            BuildItem clone = item.Clone();
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Verifies HasMetadata for all Built In Metadata for a specific BuildItem
        /// </summary>
        /// <param name="item">The BuildItem you want to verify against</param>
        private void VerifyHasMetaDataOnBuiltInMetadata(BuildItem item)
        {
            foreach (string s in builtInMetadataNames)
            {
                Assertion.AssertEquals(true, item.HasMetadata(s));
            }
        }

        /// <summary>
        /// Verifies within a BuildItem for Metadata, if the Built in MetadataNames exist or not
        /// </summary>
        /// <param name="list">List of strings of either BuildItem.MetadataNames or .CustomMetadataNames</param>
        /// <param name="expectedToExist">true if you expect the Built in MetadataNames to exist, false if not</param>
        private void VerifyBuiltInMetadataNamesExistOrNot(List<string> list, bool expectedToExist)
        {
            foreach (string s in builtInMetadataNames)
            {
                Assertion.AssertEquals(expectedToExist, list.Contains(s));
            }
        }

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
                            <nImported Include='iImported' />
                        </ItemGroup>
                    </Project>
                ";
            }

            if (String.IsNullOrEmpty(parentProjectContents))
            {
                parentProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <nMain Include='iMain' />
                        </ItemGroup>
                        <Import Project='import.proj' />
                    </Project>
                ";
            }

            ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", importProjectContents);
            ObjectModelHelpers.CreateFileInTempProjectDirectory("main.proj", parentProjectContents);
            return ObjectModelHelpers.LoadProjectFileInTempProjectDirectory("main.proj", null);
        }

        /// <summary>
        /// Gets a BuildItem based on the BuildItem.Name you specify
        /// </summary>
        /// <param name="p">Project p</param>
        /// <param name="buildItemName">BuildItem.Name that you want</param>
        /// <returns>A BuildItem</returns>
        private BuildItem GetSpecificBuildItem(Project p, string buildItemName)
        {
            foreach (BuildItemGroup group in p.ItemGroups)
            {
                foreach (BuildItem item in group)
                {
                    if (String.Equals(item.Name, buildItemName, StringComparison.OrdinalIgnoreCase))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Saves a given Project to disk and compares what's saved to disk with expected contents.  Assertion handled within
        ///     ObjectModelHelpers.CompareProjectContents.
        /// </summary>
        /// <param name="p">Project to save</param>
        /// <param name="expectedProjectContents">The Project content that you expect</param>
        private void SaveProjectToDiskAndCompareAgainstExpectedContents(Project p, string expectedProjectContents)
        {
            string savePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "p.proj");
            p.Save(savePath);

            Engine e = new Engine();
            Project savedProject = new Project(e);
            savedProject.Load(savePath);

            ObjectModelHelpers.CompareProjectContents(savedProject, expectedProjectContents);
            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Call to set your BuildItem.Name to something valid after inializing it to null to verify
        ///     expected Microsoft.Build.BuildEngine.Shared.InternalErrorException is thrown.
        /// </summary>
        /// <param name="item">Your initialized BuiltItem</param>
        /// <param name="setting">true if your doing a Set, false if your doing a Get</param>
        private void BuildItemNameGetSetNullExpectedToThrow(BuildItem item, bool setting)
        {
            try
            {
                if (setting)
                {
                    item.Name = null;
                }
                else
                {
                    string s = item.Name;
                }
            }
            catch (Exception e) //// can't directly catch InternalErrorException because it's not publicly available
            {
                if (setting)
                {
                    Assertion.AssertEquals(true, e.Message.Contains("Internal MSBuild Error: Set Name: Item has not been initialized."));
                }
                else
                {
                    Assertion.AssertEquals(true, e.Message.Contains("Internal MSBuild Error: Get Name: Item has not been initialized."));
                }

                return;
            }

            Assertion.Fail("Expected Microsoft.Build.BuildEngine.Shared.InternalErrorException not thrown");
        }

        /// <summary>
        /// Loops through all of the Reserved itemNames to ensure the expected 'InvalidOperationException'
        ///     is thrown and that the exception message contains the reserved itemName name.  If any
        ///     unexpected exceptions are thrown, they are not directly handled, thus will fail the calling
        ///     unit test with the exception information.  And, if no exception is thrown, Assertion.Fail
        ///     is called, because the InvalidOperationException should have been thrown.
        /// </summary>
        /// <param name="testType">Type of test that you are calling from</param>
        private void VerifyReservedNamesForBuildItemNameThrowExpectedException(TypeOfTest testType)
        {
            foreach (string s in reservedNames)
            {
                try
                {
                    if (testType == TypeOfTest.ConstructorTest)
                    {
                        BuildItem item = new BuildItem(s, "i");
                    }
                    else if (testType == TypeOfTest.SetNameTest)
                    {
                        BuildItem item = new BuildItem("n", "i");
                        item.Name = s;
                    }
                    else if (testType == TypeOfTest.SetMetadataTest)
                    {
                        BuildItem item = new BuildItem("n", "i");
                        item.SetMetadata(s, "v");
                    }

                    Assertion.Fail(String.Format("Reserved itemName '{0}' didn't throw the expected InvalidOperationException", s));
                }
                catch (InvalidOperationException expected)
                {
                    Assertion.AssertEquals(true, expected.Message.Contains(s));
                }
            }
        }

        /// <summary>
        /// Loops through all of the Escape Characters to ensure the expected 'ArgumentException'
        ///     is thrown and that the exception message contains the Escape Character.  If any
        ///     unexpected exceptions are thrown, they are not directly handled, thus will fail the calling
        ///     unit test with the exception information.  And, if no exception is thrown, Assertion.Fail
        ///     is called, because the ArgumentException should have been thrown.
        /// </summary>
        /// <param name="testType">Type of test that you are calling from</param>
        private void VerifyEscapeCharactersThrowExpectedException(TypeOfTest testType)
        {
            foreach (char c in EscapableCharacters)
            {
                try
                {
                    if (testType == TypeOfTest.ConstructorTest)
                    {
                        BuildItem item = new BuildItem(c.ToString(), "i");
                    }
                    else if (testType == TypeOfTest.SetNameTest)
                    {
                        BuildItem item = new BuildItem("n", "i");
                        item.Name = c.ToString();
                    }
                    else if (testType == TypeOfTest.SetMetadataTest)
                    {
                        BuildItem item = new BuildItem("n", "i");
                        item.SetMetadata(c.ToString(), "v");
                    }

                    Assertion.Fail(String.Format("Escape Character '{0}' didn't throw the expected ArgumentException", c.ToString()));
                }
                catch (ArgumentException expected)
                {
                    Assertion.AssertEquals(true, expected.Message.Contains(c.ToString()));
                }
            }
        }

        /// <summary>
        /// Un-registers the existing logger and registers a new copy.
        /// We will use this when we do multiple builds so that we can safely 
        /// assert on log messages for that particular build.
        /// </summary>
        private void ResetLogger()
        {
            engine.UnregisterAllLoggers();
            logger = new MockLogger();
            project.ParentEngine.RegisterLogger(logger);
        }

        /// <summary>
        /// Custom implementation of ITaskItem for unit testing
        /// </summary>
        internal class MockTaskItem : ITaskItem
        {
            #region ITaskItem Members

            /// <summary>
            /// String ItemSpec
            /// </summary>
            public string ItemSpec
            {
                get
                {
                    return "i";
                }

                set
                {
                    // do nothing
                }
            }

            /// <summary>
            /// Implementation of ICollection
            /// </summary>
            public ICollection MetadataNames
            {
                get
                {
                    return new ArrayList();
                }
            }

            /// <summary>
            /// Implementation of MetadataCount
            /// </summary>
            public int MetadataCount
            {
                get { return 1; }
            }

            /// <summary>
            /// Implementation of GetMetaData
            /// </summary>
            /// <param name="attributeName">attribute name</param>
            /// <returns>always just returns 'foo'</returns>
            public string GetMetadata(string attributeName)
            {
                return "foo";
            }

            /// <summary>
            /// Implementation of SetMetadata
            /// </summary>
            /// <param name="attributeName">The attribute name</param>
            /// <param name="attributeValue">The attribute value</param>
            public void SetMetadata(string attributeName, string attributeValue)
            {
                // do nothing
            }

            /// <summary>
            /// Implementation of RemoveMetadata
            /// </summary>
            /// <param name="attributeName">does nothing</param>
            public void RemoveMetadata(string attributeName)
            {
                // do nothing
            }

            /// <summary>
            /// Implementation of CopyMetadataTo
            /// </summary>
            /// <param name="destinationItem">does nothing</param>
            public void CopyMetadataTo(ITaskItem destinationItem)
            {
                // do nothing
            }

            /// <summary>
            /// Implementation of IDictionary
            /// </summary>
            /// <returns>An IDictionary</returns>
            public IDictionary CloneCustomMetadata()
            {
                return new Hashtable();
            }

            #endregion
        }
        #endregion
    }
}
