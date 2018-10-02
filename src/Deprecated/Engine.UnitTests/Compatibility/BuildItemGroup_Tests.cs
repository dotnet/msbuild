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
    /// Test Fixture Class for the v9 Object Model Public Interface Compatibility Tests for the BuildItemGroup Class.
    /// </summary>
    [TestFixture]
    public sealed class BuildItemGroup_Tests 
    {
        #region Common Helpers
        /// <summary>
        /// Basic Project XML Content
        /// </summary>
        private const string ProjectContentWithOneBuildItemGroupThreeBuildItems = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup Condition=""'A'=='B'"">
                            <n1 Include='i1' Exclude='e1'>
                                <n1Meta1>n1value1</n1Meta1>
                                <n1Meta2>n1value2</n1Meta2>
                            </n1>
                            <n2 Include='i2' Condition=""'a2' == 'b2'"" />
                            <n3 Include='i3'>
                                <n3Meta1>n3value1</n3Meta1>
                            </n3>
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

        /// <summary>
        /// Example test for BuildItemGroup 
        /// ****Don't keep this test once you're done automating****
        /// </summary>
        [Test]
        public void ExampleTest()
        {
            ////    Part 1 of Example test - working with BuildItemGroup where
            ////        you have an XML project
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup groupOne = GetBuildItemGroupFromProject(project, false);

            Assertion.AssertEquals("'A'=='B'", groupOne.Condition);
            Assertion.AssertEquals(3, groupOne.Count);
            Assertion.AssertEquals(false, groupOne.IsImported);

            BuildItem itemOne = GetSpecificBuildItemFromBuildItemGroup(groupOne, "n2");
            Assertion.AssertEquals("'a2' == 'b2'", itemOne.Condition);
            groupOne.RemoveItem(itemOne);
            Assertion.AssertEquals(2, groupOne.Count);

            BuildItem itemOther = groupOne.AddNewItem("n4", "i4");
            Assertion.AssertEquals(3, groupOne.Count);

            ////    Part 2 of Example test - working with BuildItemGroup where
            ////        you have an in-memory buildItemGroup
            BuildItemGroup groupTwo = new BuildItemGroup();
            groupTwo.AddNewItem("name", "include");
            Assertion.AssertEquals(1, groupTwo.Count);

            ////    Part 3 of Example test - working with BuildItemGroup where
            ////        you have an XML project that contains an Imported Project
            Project p = GetProjectThatImportsAnotherProject(null, null);
            BuildItemGroup groupThree = GetBuildItemGroupFromProject(p, true);

            Assertion.AssertEquals(true, groupThree.IsImported);

            BuildItem itemThree = GetSpecificBuildItemFromBuildItemGroup(groupThree, "n3Imported");
            Assertion.AssertEquals("n3Importedvalue1", itemThree.GetMetadata("n3ImportedMeta1"));
        }

        #region Constructor Tests
        /// <summary>
        /// Tests the Default (and only) BuildItemGroup Contructor, which takes no parameters
        /// </summary>
        [Test]
        public void ConstructSimple()
        {
            BuildItemGroup group = new BuildItemGroup();
            Assertion.AssertEquals(0, group.Count);
        }
        #endregion

        #region Condition Tests
        /// <summary>
        /// Tests BuildItemGroup.Condition Get simple/basic case
        /// </summary>
        [Test]
        public void ConditionGetSimple()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            Assertion.AssertEquals("'A'=='B'", group.Condition);
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Get when no condition exists
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ConditionGetWhenNoCondition()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Get when Condition is an empty string
        /// </summary>
        [Test]
        public void ConditionGetEmptyString()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Get from an imported Project
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ConditionGetFromImportedProject()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Set on in-memory BuildItemGroup
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ConditionSetOnInMemoryGroup()
        {
            BuildItemGroup group = new BuildItemGroup();
            group.Condition = "'t' == 'TRUE'";
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Set when no provious exists
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ConditionSetWhenNoPreviousConditionExists()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Set when an existing condition exists, changing the condition
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ConditionSetOverExistingCondition()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Set To Empty string
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ConditionSetToEmptyString()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Set to null
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ConditionSetToNull()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Set to Special Characters
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ConditionSetToSpecialCharacters()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Set to Escape Characters
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ConditionSetToEscapableCharacters()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Attempt Set on an Imported Project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        [Ignore("not yet implemented")]
        public void ConditionSetOnImportedProject()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Condition Set, save to disk and verify
        /// </summary>
        [Test]
        public void ConditionSaveProjectAfterSet()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);
            group.Condition = "'t' == 'true'";

            string expectedProjectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup Condition=""'t' == 'true'"">
                            <n1 Include='i1' Exclude='e1'>
                                <n1Meta1>n1value1</n1Meta1>
                                <n1Meta2>n1value2</n1Meta2>
                            </n1>
                            <n2 Include='i2' Condition=""'a2' == 'b2'"" />
                            <n3 Include='i3'>
                                <n3Meta1>n3value1</n3Meta1>
                            </n3>
                        </ItemGroup>
                    </Project>
                    ";

            SaveProjectToDiskAndCompareAgainstExpectedContents(project, expectedProjectContents);
        }
        #endregion

        #region Count Tests
        /// <summary>
        /// Tests BuildItemGroup.Count when Several BuildItem Exist within a BuildItemGroup
        /// </summary>
        [Test]
        public void CountGetWhenSeveralExist()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            Assertion.AssertEquals(3, group.Count);
        }

        /// <summary>
        /// Tests BuildItemGroup.Count when no BuildItems exist within a BuildItemGroup
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CountGetWhenNoneExist()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Count from an Imported BuildItemGroup
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CountGetFromImportedGroup()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Count from an In-memory BuildItemGroup
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CountGetFromInMemoryGroup()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Count after clearing it, expecting to be Zero
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CountAfterClear()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Count after removing 1 of the BuildItems
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CountAfterRemovingSomeItems()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Count after removing all of the BuildItems
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CountAfterRemovingAllItems()
        {
        }
        #endregion

        #region IsImported Tests
        /// <summary>
        /// Tests BuildItemGroup.IsImported when BuildItemGroup does come from an imported project
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void IsImportedExpectedTrue()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.IsImported when BuildItemGroup does not come from an imported project (comes from main/parent project)
        ///     and no projects are imported.
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void IsImportedExpectedFalseNoImportsExist()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.IsImported when specific BuildItemGroup does not come from an imported project
        ///     but others are imported.
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void IsImportedExpectedFalseImportsDoExist()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.IsImported when all we have is an in memory BuildItemGroup
        /// </summary>
        [Test]
        public void IsImportedInMemoryBuildItemGroup()
        {
            BuildItemGroup group = new BuildItemGroup();
            group.AddNewItem("n", "i");

            Assertion.AssertEquals(false, group.IsImported);
        }
        #endregion

        #region Clone Tests
        ////See BuildPropertyGroup Clone tests for specific examples for BuildItemGroup

        /// <summary>
        /// Tests BuildItemGroup.Clone Deep
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CloneDeep()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Clone Deep with Clear on the Cloned group
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CloneDeepClear()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Clone Shallow
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CloneShallow()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Clone Shallow with Add new BuildItem
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void CloneShallowAddItem()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.Clone Shallow when you attempt a shallow clone of an XMLProject
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        [Ignore("not yet implemented")]
        public void CloneShallowWithXMLProject()
        {
        }
        #endregion

        #region AddNewItem Tests
        /// <summary>
        /// Tests BuildItemGroup.AddNewItem for simple/basic case
        /// </summary>
        [Test]
        public void AddNewItemNameSimple()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group.AddNewItem("n4", "i4");

            Dictionary<string, string> items = GetDictionaryOfBuildItemsInProject(project, false);
            Assertion.AssertEquals("i1", items["n1"]);
            Assertion.AssertEquals("i2", items["n2"]);
            Assertion.AssertEquals("i3", items["n3"]);
            Assertion.AssertEquals("i4", items["n4"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem of same name as existing item (which creates another item of the same name)
        /// </summary>
        [Test]
        public void AddNewItemNameOfExistingItem()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group.AddNewItem("n3", "iNew");

            Dictionary<string, string> items = GetDictionaryOfBuildItemsInProject(project, true);
            Assertion.AssertEquals("i1", items["n1"]);
            Assertion.AssertEquals("i2", items["n2"]);
            Assertion.AssertEquals("i3", items["n3"]);
            Assertion.AssertEquals("iNew", items["_n3"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with setting Name to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddNewItemNameToNull()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group.AddNewItem(null, "i");
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with setting Name to null on an in memory group
        /// </summary>
        [Test]
        public void AddNewItemNameToNullMemoryGroup()
        {
            BuildItemGroup group = new BuildItemGroup();
            group.AddNewItem(null, "i");

            Assertion.AssertEquals(1, group.Count);
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with setting Name to an empty string
        /// </summary>
        [Test]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void AddNewItemNameToEmptyString()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group.AddNewItem(String.Empty, "i");
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with setting itemInclude to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        [Ignore("not yet implemented")]
        public void AddNewItemItemIncludeToNull()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with setting itemInclude to an empty string
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void AddNewItemItemIncludeToEmptyString()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with simple Name and itemInclude
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void AddNewItemSimpleNameItemInclude()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with simple Name and itemInclude as well as setting Treat Property Value as Literal to true
        /// </summary>
        [Test]
        public void AddNewItemSimpleNameItemIncludeWithTreatPropertyValueAsLiteralTrue()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group.AddNewItem("n4", EscapableCharacters, true);

            Dictionary<string, string> items = GetDictionaryOfBuildItemsInProject(project, false);
            Console.WriteLine("'{0}'", items["n4"].ToString());
            Assertion.AssertEquals(@"%25%2a%3f%40%24%28%29%3b\", items["n4"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with simple Name and itemInclude as well as setting Treat Property Value as Literal to false
        /// </summary>
        [Test]
        public void AddNewItemSimpleNameItemIncludeWithTreatPropertyValueAsLiteralFalse()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group.AddNewItem("n4", EscapableCharacters, false);

            Dictionary<string, string> items = GetDictionaryOfBuildItemsInProject(project, false);
            Console.WriteLine("'{0}'", items["n4"].ToString());
            Assertion.AssertEquals(EscapableCharacters, items["n4"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with special characters for the Name
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void AddNewItemWithSpecialCharactersInName()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group.AddNewItem(SpecialCharacters, "i");
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem with special characters for the itemInclude
        /// </summary>
        [Test]
        public void AddNewItemWithSpecialCharactersInValue()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group.AddNewItem("n4", SpecialCharacters);

            Dictionary<string, string> items = GetDictionaryOfBuildItemsInProject(project, false);
            Assertion.AssertEquals(SpecialCharacters, items["n4"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem within an in memory build item group
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void AddNewItemToInMemoryGroup()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem to an Imported Group, expected to fail
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void AddNewItemToGroupWithinAnImportedProject()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.AddNewItem to a Group, save to disk and verify
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void AddNewItemSaveToDiskVerify()
        {
        }
        #endregion

        #region RemoveItem Tests
        /// <summary>
        /// Tests BuildItemGroup.RemoveItem for simple/basic case
        /// </summary>
        [Test]
        public void RemoveItemSimple()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            BuildItem item = GetSpecificBuildItemFromBuildItemGroup(group, "n2");
            group.RemoveItem(item);

            Assertion.AssertEquals(2, group.Count);
            Dictionary<string, string> items = GetDictionaryOfBuildItemsInProject(project, false);
            Assertion.AssertEquals("i1", items["n1"]);
            Assertion.AssertEquals("i3", items["n3"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItem by removing 1 of many items
        /// </summary>
        [Test]
        public void RemoveItemOneOfSeveral()
        {
            BuildItemGroup group = new BuildItemGroup();
            BuildItem item = group.AddNewItem("n1", "i1");
            group.AddNewItem("n2", "i2");
            group.AddNewItem("n3", "i3");

            group.RemoveItem(item);

            Assertion.AssertEquals(2, group.Count);

            Dictionary<string, string> items = GetDictionaryOfBuildItemsInBuildItemsGroup(group);
            Assertion.AssertEquals("i2", items["n2"]);
            Assertion.AssertEquals("i3", items["n3"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItem by removing all of many
        /// </summary>
        [Test]
        public void RemoveItemAllOfSeveral()
        {
            BuildItemGroup group = new BuildItemGroup();
            BuildItem[] item = new BuildItem[3];
            item[0] = group.AddNewItem("n1", "i1");
            item[1] = group.AddNewItem("n2", "i2");
            item[2] = group.AddNewItem("n3", "i3");

            group.RemoveItem(item[0]);
            group.RemoveItem(item[1]);
            group.RemoveItem(item[2]);

            Assertion.AssertEquals(0, group.Count);
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItem by attempting to remove an item that doesn't actually exist
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void RemoveItemOfANonExistingItem()
        {
        }

        /// <summary>
        ///  Tests BuildItemGroup.RemoveItem by attempting to remove a property name that is null
        /// </summary>
        [Test]
        public void RemoveItemWhenItemIsNull()
        {
            BuildItemGroup group = new BuildItemGroup();
            group.RemoveItem(null);

            Assertion.AssertEquals(0, group.Count);
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItem by attempting to remove an item that is an empty string
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void RemoveItemThatIsAnEmptyString()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItem from an Imported Group, expected to fail
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void RemoveItemToGroupWithinAnImportedProject()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItem from a Group, save to disk and verify
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void RemoveItemSaveToDiskVerify()
        {
        }

        #endregion

        #region RemoveItemAt Tests
        //// RemoveItemAt(int index): index is zero based

        /// <summary>
        /// Tests BuildItemGroup.RemoveItemAt simple/base case
        /// </summary>
        [Test]
        public void RemoveItemAtSimple()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group.RemoveItemAt(1);

            Assertion.AssertEquals(2, group.Count);
            Dictionary<string, string> items = GetDictionaryOfBuildItemsInProject(project, false);
            Assertion.AssertEquals("i1", items["n1"]);
            Assertion.AssertEquals("i3", items["n3"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItemAt first item in group
        /// </summary>
        [Test]
        public void RemoveItemAtFirstItem()
        {
            BuildItemGroup group = new BuildItemGroup();
            group.AddNewItem("n1", "i1");
            group.AddNewItem("n2", "i2");
            group.AddNewItem("n3", "i3");

            group.RemoveItemAt(0);

            Assertion.AssertEquals(2, group.Count);

            Dictionary<string, string> items = GetDictionaryOfBuildItemsInBuildItemsGroup(group);
            Assertion.AssertEquals("i2", items["n2"]);
            Assertion.AssertEquals("i3", items["n3"]);
        }

        /// <summary>
        /// Tests the addition and removal of an evaluated item via its parent item group.
        /// </summary>
        /// <bug>Regression for bug: 170974</bug>
        /// <remarks>
        /// This method asserts broken behaviour
        /// Contrast with RemoveEvaluatedItem2 for expected behaviours.
        /// </remarks>
        [Test]
        public void RemoveEvaluatedItem1()
        {
            try
            {
                List<string> files = CompatibilityTestHelpers.CreateFiles(4, "foo", "foo", ObjectModelHelpers.TempProjectDir);
                Project p = new Project(new Engine());
                BuildItemGroup group = p.AddNewItemGroup();
                group.AddNewItem("foos", Path.Combine(ObjectModelHelpers.TempProjectDir, "*.foo"));
                object o = p.EvaluatedItems; // this causes the failure 
                group.RemoveItem(p.EvaluatedItems[0]); // Exception thrown here
                Assertion.Fail("success as failure"); // should not get here due to exception above
            }
            catch (Exception e)
            {
                if (!(e.GetType().ToString().Contains("InternalErrorException")))
                {
                    Assertion.Fail(e.Message + " was thrown");
                }
                else
                {
                    Assertion.Assert("InternalErrorException was thrown", true);
                }
            }
            finally
            {
                CompatibilityTestHelpers.CleanupDirectory(ObjectModelHelpers.TempProjectDir);
            }
        }

        /// <summary>
        /// Tests the addition of an evaluated item via its parent item group and removal via its project object.
        /// </summary>
        /// <bug>Regression of 170974</bug>
        [Test]
        public void RemoveEvaluatedItemSuccess()
        {
            try
            {
                string includePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "*.foo");
                List<string> files = CompatibilityTestHelpers.CreateFiles(4, "foo", "foo", ObjectModelHelpers.TempProjectDir);
                Project p = new Project(new Engine());
                BuildItemGroup group = p.AddNewItemGroup();
                group.AddNewItem("foos", includePath);
                object o = p.EvaluatedItems;
                files.RemoveAt(files.IndexOf(p.EvaluatedItems[0].FinalItemSpec));
                p.RemoveItem(p.EvaluatedItems[0]);
                int i = 0;
                foreach (string fileName in files)
                {
                    Assertion.AssertEquals(includePath, group[0].FinalItemSpec);
                    Assertion.AssertEquals(includePath, group[0].Include);
                    Assertion.AssertEquals(fileName, p.EvaluatedItems[i].Include);
                    Assertion.AssertEquals(fileName, p.EvaluatedItems[i].FinalItemSpec);
                    i++;
                }
            }
            finally
            {
                CompatibilityTestHelpers.CleanupDirectory(ObjectModelHelpers.TempProjectDir);
            }
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItemAt last item in group
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void RemoveItemAtLastItem()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItemAt middle item in group (of more then 3)
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void RemoveItemAtMiddleItem()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItemAt attempt to remove non-existing item
        ///     Example, you have 3 items in your group, attempt to remove item 5
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void RemoveItemAtNonExistingItem()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItemAt attempt to remove an item from an Imported project
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void RemoveItemAtFromImportedProject()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.RemoveItemAt remove, save to disk and verify
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void RemoveItemAtSaveToDiskAndVerify()
        {
        }
        #endregion

        #region ToArray[] Tests
        /// <summary>
        /// Tests BuildItemGroup.ToArray[] simple case.  Verify changing an item in the Array changes the actual item
        ///     in the group because the copies are NOT clones i.e. only the references are copied.
        /// </summary>
        [Test]
        public void ToArrayChangeMadeToArray()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            BuildItem[] items = group.ToArray();
            Assertion.AssertEquals(3, items.Length);

            items[0].Include = "New";
            Dictionary<string, string> groupItems = GetDictionaryOfBuildItemsInBuildItemsGroup(group);
            Assertion.AssertEquals("New", items[0].Include);
            Assertion.AssertEquals("New", groupItems["n1"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.ToArray[] case where change is made to the BuildItemGroup and verify that
        ///     change is not reflected in the BuildItem Array.
        /// </summary>
        [Test]
        public void ToArrayChangeMadeToGroup()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            BuildItem[] items = group.ToArray();
            Assertion.AssertEquals(3, items.Length);

            //// Change first item in group by removing it and re-adding it with a new itemInclude value
            group.RemoveItemAt(0);
            group.AddNewItem("n1", "New");

            Dictionary<string, string> groupItems = GetDictionaryOfBuildItemsInBuildItemsGroup(group);
            Assertion.AssertEquals("i1", items[0].Include);
            Assertion.AssertEquals("New", groupItems["n1"]);
        }

        /// <summary>
        /// Tests BuildItemGroup.ToArray[] attempt to make change to Array where group comes from an imported Project
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ToArrayOfImportedGroup()
        {
        }

        /// <summary>
        /// Tests BuildItemGroup.ToArray[] make change to Array, save Project to disk and verify
        ///     change is included
        /// </summary>
        [Test]
        [Ignore("not yet implemented")]
        public void ToArrayChangeMadeSaveToDiskAndVerify()
        {
        }
        #endregion

        #region BuildItem this[int index] Tests
        /// <summary>
        /// Tests BuildItem this[int index] { get }
        /// </summary>
        [Test]
        public void ThisGet()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            Assertion.AssertEquals("n1", group[0].Name);
            Assertion.AssertEquals("i1", group[0].Include);
            Assertion.AssertEquals("e1", group[0].Exclude);
            Assertion.AssertEquals("i1", group[0].FinalItemSpec);
            Assertion.AssertEquals(String.Empty, group[0].Condition);
            Assertion.AssertEquals(2, group[0].CustomMetadataCount);

            Assertion.AssertEquals("n2", group[1].Name);
            Assertion.AssertEquals("i2", group[1].Include);
            Assertion.AssertEquals(String.Empty, group[1].Exclude);
            Assertion.AssertEquals("i2", group[1].FinalItemSpec);
            Assertion.AssertEquals("'a2' == 'b2'", group[1].Condition);
            Assertion.AssertEquals(0, group[1].CustomMetadataCount);

            Assertion.AssertEquals("n3", group[2].Name);
            Assertion.AssertEquals("i3", group[2].Include);
            Assertion.AssertEquals(String.Empty, group[2].Exclude);
            Assertion.AssertEquals("i3", group[2].FinalItemSpec);
            Assertion.AssertEquals(String.Empty, group[2].Condition);
            Assertion.AssertEquals(1, group[2].CustomMetadataCount);
        }

        /// <summary>
        /// Tests BuildItem this[int index] { set }
        /// </summary>
        [Test]
        public void ThisSet()
        {
            project.LoadXml(ProjectContentWithOneBuildItemGroupThreeBuildItems);
            BuildItemGroup group = GetBuildItemGroupFromProject(project, false);

            group[0].Name = "n1New";
            group[1].Include = "i2New";
            group[2].Exclude = "e3New";
            group[0].Condition = "'true' == 'T'";
            group[1].SetMetadata("n2MetaNew", "n2valueNew");
            group[2].RemoveMetadata("n3Meta1");

            Assertion.AssertEquals("n1New", group[0].Name);
            Assertion.AssertEquals("i1", group[0].Include);
            Assertion.AssertEquals("e1", group[0].Exclude);
            Assertion.AssertEquals("i1", group[0].FinalItemSpec);
            Assertion.AssertEquals("'true' == 'T'", group[0].Condition);
            Assertion.AssertEquals(2, group[0].CustomMetadataCount);

            Assertion.AssertEquals("n2", group[1].Name);
            Assertion.AssertEquals("i2New", group[1].Include);
            Assertion.AssertEquals(String.Empty, group[1].Exclude);
            Assertion.AssertEquals("i2New", group[1].FinalItemSpec);
            Assertion.AssertEquals("'a2' == 'b2'", group[1].Condition);
            Assertion.AssertEquals(1, group[1].CustomMetadataCount);
            Assertion.AssertEquals("n2valueNew", group[1].GetEvaluatedMetadata("n2MetaNew"));

            Assertion.AssertEquals("n3", group[2].Name);
            Assertion.AssertEquals("i3", group[2].Include);
            Assertion.AssertEquals("e3New", group[2].Exclude);
            Assertion.AssertEquals("i3", group[2].FinalItemSpec);
            Assertion.AssertEquals(String.Empty, group[2].Condition);
            Assertion.AssertEquals(0, group[2].CustomMetadataCount);
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Gets a Dictionary List of BuildItems in your BuildItemGroup
        /// </summary>
        /// <param name="group">BuildItemGroup</param>
        /// <returns>A Dictionary List of BuildItems in your BuildItemGroup (Key = itemName, Value = itemInclude)</returns>
        private static Dictionary<string, string> GetDictionaryOfBuildItemsInBuildItemsGroup(BuildItemGroup group)
        {
            Dictionary<string, string> items = new Dictionary<string, string>();

            foreach (BuildItem item in group)
            {
                items.Add(item.Name, item.Include);
            }

            return items;
        }

        /// <summary>
        /// Gets a Dictionary List of BuildItems in your Project (Key = itemName, Value = itemInclude)
        /// </summary>
        /// <param name="p">Project</param>
        /// <returns>A Dictionary List of BuildItems in your project (Key = itemName, Value = itemInclude)</returns>
        private static Dictionary<string, string> GetDictionaryOfBuildItemsInProject(Project p, bool expectingDuplicateItem)
        {
            Dictionary<string, string> items = new Dictionary<string, string>();

            foreach (BuildItemGroup group in p.ItemGroups)
            {
                foreach (BuildItem item in group)
                {
                    try
                    {
                        items.Add(item.Name, item.Include);
                    }
                    catch (ArgumentException)
                    {
                        if (expectingDuplicateItem)
                        {
                            items.Add(String.Format("_{0}", item.Name), item.Include);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Gets a List of all BuildItems within your Project
        /// </summary>
        /// <param name="p">Project</param>
        /// <returns>A List of strings of all BuildItems</returns>
        private static List<string> GetListOfBuildItems(Project p)
        {
            List<string> items = new List<string>();
            foreach (BuildItemGroup group in p.ItemGroups)
            {
                foreach (BuildItem item in group)
                {
                    items.Add(item.Name);
                }
            }

            return items;
        }

        /// <summary>
        /// Gets you the specified BuildItem from a BuildItemGroup
        /// </summary>
        /// <param name="group">BuildItemGroup</param>
        /// <param name="buildItemName">The name of the BuildItem that you want</param>
        /// <returns>The specified BuildItem</returns>
        private BuildItem GetSpecificBuildItemFromBuildItemGroup(BuildItemGroup group, string buildItemName)
        {
            foreach (BuildItem item in group)
            {
                if (String.Equals(item.Name, buildItemName, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets you the BuildItemGroup from a Project
        /// </summary>
        /// <param name="p">Project that contains a BuildItemGroup</param>
        /// <param name="importedGroup">true if you want the BuildItemGroup that's Imported, false if not.  When
        ///     not dealing with imported projects, assume false</param>
        /// <returns>A BuildItemGroup</returns>
        private BuildItemGroup GetBuildItemGroupFromProject(Project p, bool importedGroup)
        {
            foreach (BuildItemGroup group in p.ItemGroups)
            {
                if (group.IsImported == importedGroup)
                {
                    return group;
                }
            }

            return null;
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
                            <n1Imported Include='i1Imported' Exclude='e1Imported' Condition=""'a2' == 'b2'"">
                                <n1ImportedMeta1>n1Importedvalue1</n1ImportedMeta1>
                                <n1ImportedMeta2>n1Importedvalue2</n1ImportedMeta2>
                            </n1Imported>
                            <n2Imported Include='i2Imported' />
                            <n3Imported Include='i3Imported'>
                                <n3ImportedMeta1>n3Importedvalue1</n3ImportedMeta1>
                            </n3Imported>
                        </ItemGroup>
                    </Project>
                ";
            }

            if (String.IsNullOrEmpty(parentProjectContents))
            {
                parentProjectContents = @" 
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                        <ItemGroup>
                            <n1Main Include='iMain' />
                            <n2Main Include='iMain' >
                                <n2MainMeta>n2Mainvalue</n2MainMeta>
                            </n2Main>
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

        #endregion
    }
}
