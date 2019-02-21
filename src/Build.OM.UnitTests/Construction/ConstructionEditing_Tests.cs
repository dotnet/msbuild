// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Construction
{
    /// <summary>
    /// Tests for editing through the construction model
    /// </summary>
    public class ConstructionEditing_Tests
    {
        /// <summary>
        /// Add a target through the convenience method
        /// </summary>
        [Fact]
        public void AddTargetConvenience()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" />
</Project>");

            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(1, project.Count);
            Assert.Equal(0, target.Count);
            Assert.Equal(1, Helpers.Count(project.Children));
            Assert.Equal(0, Helpers.Count(target.Children));
            Assert.Null(project.Parent);
            Assert.Equal(project, target.Parent);
        }

        /// <summary>
        /// Simple add a target
        /// </summary>
        [Fact]
        public void AppendTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            Helpers.ClearDirtyFlag(project);
            ProjectTargetElement target = project.CreateTargetElement("t");
            Assert.False(project.HasUnsavedChanges);

            project.AppendChild(target);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(1, project.Count);
        }

        /// <summary>
        /// Append two targets
        /// </summary>
        [Fact]
        public void AppendTargetTwice()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.CreateTargetElement("t");
            ProjectTargetElement target2 = project.CreateTargetElement("t2");

            project.AppendChild(target1);
            project.AppendChild(target2);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" />
  <Target Name=""t2"" />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);

            Assert.Equal(2, project.Count);
            var targets = Helpers.MakeList(project.Targets);
            Assert.Equal(2, targets.Count);
            Assert.Equal(target1, targets[0]);
            Assert.Equal(target2, targets[1]);
        }

        /// <summary>
        /// Add node created from different project with AppendChild
        /// </summary>
        [Fact]
        public void InvalidAddFromDifferentProject_AppendChild()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project1 = ProjectRootElement.Create();
                ProjectRootElement project2 = ProjectRootElement.Create();
                ProjectTargetElement target = project1.CreateTargetElement("t");
                project2.AppendChild(target);
            }
           );
        }
        /// <summary>
        /// Add node created from different project with PrependChild
        /// </summary>
        [Fact]
        public void InvalidAddFromDifferentProject_PrependChild()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project1 = ProjectRootElement.Create();
                ProjectRootElement project2 = ProjectRootElement.Create();
                ProjectTargetElement target = project1.CreateTargetElement("t");
                project2.PrependChild(target);
            }
           );
        }
        /// <summary>
        /// Add node created from different project with InsertBeforeChild
        /// </summary>
        [Fact]
        public void InvalidAddFromDifferentProject_InsertBefore()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project1 = ProjectRootElement.Create();
                ProjectRootElement project2 = ProjectRootElement.Create();
                ProjectTargetElement target1 = project1.CreateTargetElement("t");
                ProjectTargetElement target2 = project2.AddTarget("t2");
                project2.InsertBeforeChild(target2, target1);
            }
           );
        }
        /// <summary>
        /// Add node created from different project with InsertAfterChild
        /// </summary>
        [Fact]
        public void InvalidAddFromDifferentProject_InsertAfter()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project1 = ProjectRootElement.Create();
                ProjectRootElement project2 = ProjectRootElement.Create();
                ProjectTargetElement target1 = project1.CreateTargetElement("t");
                ProjectTargetElement target2 = project2.AddTarget("t2");
                project2.InsertAfterChild(target2, target1);
            }
           );
        }
        /// <summary>
        /// Become direct child of self with AppendChild
        /// (This is prevented anyway because the parent is an invalid type.)
        /// </summary>
        [Fact]
        public void InvalidBecomeChildOfSelf_AppendChild()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectChooseElement choose = project.CreateChooseElement();

                choose.AppendChild(choose);
            }
           );
        }
        /// <summary>
        /// Become grandchild of self with AppendChild
        /// </summary>
        [Fact]
        public void InvalidBecomeGrandChildOfSelf_AppendChild()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectChooseElement choose = project.CreateChooseElement();
                ProjectWhenElement when = project.CreateWhenElement("c");
                project.AppendChild(choose);
                choose.AppendChild(when);
                when.AppendChild(choose);
            }
           );
        }
        /// <summary>
        /// Become grandchild of self with PrependChild
        /// </summary>
        [Fact]
        public void InvalidBecomeGrandChildOfSelf_PrependChild()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectChooseElement choose = project.CreateChooseElement();
                ProjectWhenElement when = project.CreateWhenElement("c");
                project.AppendChild(choose);
                choose.AppendChild(when);
                when.PrependChild(choose);
            }
           );
        }
        /// <summary>
        /// Become grandchild of self with InsertBeforeChild
        /// </summary>
        [Fact]
        public void InvalidBecomeGrandChildOfSelf_InsertBefore()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectChooseElement choose1 = project.CreateChooseElement();
                ProjectWhenElement when = project.CreateWhenElement("c");
                ProjectChooseElement choose2 = project.CreateChooseElement();
                project.AppendChild(choose1);
                choose1.AppendChild(when);
                when.PrependChild(choose2);
                when.InsertBeforeChild(choose1, choose2);
            }
           );
        }
        /// <summary>
        /// Become grandchild of self with InsertAfterChild
        /// </summary>
        [Fact]
        public void InvalidBecomeGrandChildOfSelf_InsertAfter()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectChooseElement choose1 = project.CreateChooseElement();
                ProjectWhenElement when = project.CreateWhenElement("c");
                ProjectChooseElement choose2 = project.CreateChooseElement();
                project.AppendChild(choose1);
                choose1.AppendChild(when);
                when.PrependChild(choose2);
                when.InsertAfterChild(choose1, choose2);
            }
           );
        }
        /// <summary>
        /// Attempt to reparent with AppendChild
        /// </summary>
        [Fact]
        public void InvalidAlreadyParented_AppendChild()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target = project.AddTarget("t");

                project.AppendChild(target);
            }
           );
        }
        /// <summary>
        /// Attempt to reparent with PrependChild
        /// </summary>
        [Fact]
        public void InvalidAlreadyParented_PrependChild()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target = project.AddTarget("t");

                project.PrependChild(target);
            }
           );
        }
        /// <summary>
        /// Attempt to reparent with InsertBeforeChild
        /// </summary>
        [Fact]
        public void InvalidAlreadyParented_InsertBefore()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target1 = project.AddTarget("t");
                ProjectTargetElement target2 = project.AddTarget("t2");

                project.InsertBeforeChild(target1, target2);
            }
           );
        }
        /// <summary>
        /// Attempt to reparent with InsertAfterChild
        /// </summary>
        [Fact]
        public void InvalidAlreadyParented_InsertAfter()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target1 = project.AddTarget("t");
                ProjectTargetElement target2 = project.AddTarget("t2");

                project.InsertAfterChild(target1, target2);
            }
           );
        }
        /// <summary>
        /// Attempt to add to unparented parent with AppendChild
        /// </summary>
        [Fact]
        public void InvalidParentNotParented_AppendChild()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target = project.CreateTargetElement("t");
                ProjectTaskElement task = project.CreateTaskElement("tt");

                target.AppendChild(task);
            }
           );
        }
        /// <summary>
        /// Attempt to add to unparented parent with PrependChild
        /// </summary>
        [Fact]
        public void InvalidParentNotParented_PrependChild()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target = project.CreateTargetElement("t");
                ProjectTaskElement task = project.CreateTaskElement("tt");

                target.PrependChild(task);
            }
           );
        }
        /// <summary>
        /// Attempt to add to unparented parent with InsertBeforeChild
        /// </summary>
        [Fact]
        public void InvalidParentNotParented_InsertBefore()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target = project.CreateTargetElement("t");
                ProjectTaskElement task1 = project.CreateTaskElement("tt");
                ProjectTaskElement task2 = project.CreateTaskElement("tt");

                target.InsertBeforeChild(task2, task1);
            }
           );
        }
        /// <summary>
        /// Attempt to add to unparented parent with InsertAfterChild
        /// </summary>
        [Fact]
        public void InvalidParentNotParented_InsertAfter()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target = project.CreateTargetElement("t");
                ProjectTaskElement task1 = project.CreateTaskElement("tt");
                ProjectTaskElement task2 = project.CreateTaskElement("tt");

                target.InsertAfterChild(task2, task1);
            }
           );
        }
        /// <summary>
        /// Setting attributes on a target should be reflected in the XML
        /// </summary>
        [Fact]
        public void AppendTargetSetAllAttributes()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.CreateTargetElement("t");

            project.AppendChild(target);
            target.Inputs = "i";
            target.Outputs = "o";
            target.DependsOnTargets = "d";
            target.Condition = "c";

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" Inputs=""i"" Outputs=""o"" DependsOnTargets=""d"" Condition=""c"" />
</Project>");

            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Clearing attributes on a target should be reflected in the XML
        /// </summary>
        [Fact]
        public void AppendTargetClearAttributes()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.CreateTargetElement("t");

            project.AppendChild(target);
            target.Inputs = "i";
            target.Outputs = "o";
            target.Inputs = String.Empty;

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" Outputs=""o"" />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Prepend item group
        /// </summary>
        [Fact]
        public void PrependItemGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemGroupElement itemGroup = project.CreateItemGroupElement();

            project.PrependChild(itemGroup);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup />
</Project>");

            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);

            Assert.Equal(1, project.Count);
            var children = Helpers.MakeList(project.Children);
            Assert.Single(children);
            Assert.Equal(itemGroup, children[0]);
        }

        /// <summary>
        /// Insert target before
        /// </summary>
        [Fact]
        public void InsertTargetBefore()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemGroupElement itemGroup = project.CreateItemGroupElement();
            ProjectTargetElement target = project.CreateTargetElement("t");

            project.PrependChild(itemGroup);
            project.InsertBeforeChild(target, itemGroup);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" />
  <ItemGroup />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);

            Assert.Equal(2, project.Count);
            var children = Helpers.MakeList(project.Children);
            Assert.Equal(2, children.Count);
            Assert.Equal(target, children[0]);
            Assert.Equal(itemGroup, children[1]);
        }

        /// <summary>
        /// InsertBeforeChild with a null reference node should be the same as calling AppendChild.
        /// This matches XmlNode behavior.
        /// </summary>
        [Fact]
        public void InsertTargetBeforeNullEquivalentToAppendChild()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemGroupElement itemGroup = project.CreateItemGroupElement();
            ProjectTargetElement target = project.CreateTargetElement("t");

            project.PrependChild(itemGroup);
            project.InsertBeforeChild(target, null);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup />
  <Target Name=""t"" />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// InsertAfterChild with a null reference node should be the same as calling PrependChild.
        /// This matches XmlNode behavior.
        /// </summary>
        [Fact]
        public void InsertTargetAfterNullEquivalentToPrependChild()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemGroupElement itemGroup = project.CreateItemGroupElement();
            ProjectTargetElement target = project.CreateTargetElement("t");

            project.PrependChild(itemGroup);
            project.InsertAfterChild(target, null);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" />
  <ItemGroup />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Insert target before and after a reference
        /// </summary>
        [Fact]
        public void InsertTargetBeforeAndTargetAfter()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemGroupElement itemGroup = project.CreateItemGroupElement();
            ProjectTargetElement target1 = project.CreateTargetElement("t");
            ProjectTargetElement target2 = project.CreateTargetElement("t2");

            project.PrependChild(itemGroup);
            project.InsertBeforeChild(target1, itemGroup);
            project.InsertAfterChild(target2, itemGroup);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" />
  <ItemGroup />
  <Target Name=""t2"" />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);

            Assert.Equal(3, project.Count);
            var children = Helpers.MakeList(project.Children);
            Assert.Equal(3, children.Count);
            Assert.Equal(target1, children[0]);
            Assert.Equal(itemGroup, children[1]);
            Assert.Equal(target2, children[2]);
        }

        /// <summary>
        /// Insert before when no children
        /// </summary>
        [Fact]
        public void InsertTargetBeforeNothing()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.CreateTargetElement("t");

            project.InsertBeforeChild(target1, null);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" />
</Project>");

            Assert.Equal(1, project.Count);
            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Insert after when no children
        /// </summary>
        [Fact]
        public void InsertTargetAfterNothing()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.CreateTargetElement("t");

            project.InsertAfterChild(target, null);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" />
</Project>");

            Assert.Equal(1, project.Count);
            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Insert task in target
        /// </summary>
        [Fact]
        public void InsertTaskInTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.CreateTargetElement("t");
            ProjectTaskElement task = project.CreateTaskElement("tt");

            project.AppendChild(target);
            target.AppendChild(task);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"">
    <tt />
  </Target>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add a task through the convenience method
        /// </summary>
        [Fact]
        public void AddTaskConvenience()
        {
            ProjectRootElement project = ProjectRootElement.Create();

            ProjectTargetElement target = project.AddTarget("t");
            target.AddTask("tt");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"">
    <tt />
  </Target>
</Project>");

            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Attempt to insert project in target
        /// </summary>
        [Fact]
        public void InvalidAttemptToAddProjectToTarget()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target = project.CreateTargetElement("t");

                target.AppendChild(project);
            }
           );
        }
        /// <summary>
        /// Attempt to insert item in target
        /// </summary>
        [Fact]
        public void InvalidAttemptToAddItemToTarget()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target = project.CreateTargetElement("t");
                ProjectItemElement item = project.CreateItemElement("i");

                project.AppendChild(target);
                target.AppendChild(item);
            }
           );
        }
        /// <summary>
        /// Attempt to insert item without include in itemgroup in project
        /// </summary>
        [Fact]
        public void InvalidAttemptToAddEmptyItem()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectItemGroupElement itemGroup = project.CreateItemGroupElement();
                ProjectItemElement item = project.CreateItemElement("i");

                project.AppendChild(itemGroup);
                itemGroup.AppendChild(item);
            }
           );
        }
        /// <summary>
        /// Add item without include in itemgroup in target
        /// </summary>
        [Fact]
        public void AddItemWithoutIncludeToItemGroupInTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.CreateTargetElement("t");
            ProjectItemGroupElement itemGroup = project.CreateItemGroupElement();
            ProjectItemElement item = project.CreateItemElement("i");

            project.AppendChild(target);
            target.AppendChild(itemGroup);
            itemGroup.AppendChild(item);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"">
    <ItemGroup>
      <i />
    </ItemGroup>
  </Target>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add item with remove in itemgroup in target
        /// </summary>
        [Fact]
        public void AddItemWithRemoveToItemGroupInTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.CreateTargetElement("t");
            ProjectItemGroupElement itemGroup = project.CreateItemGroupElement();
            ProjectItemElement item = project.CreateItemElement("i");
            item.Remove = "r";

            project.AppendChild(target);
            target.AppendChild(itemGroup);
            itemGroup.AppendChild(item);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"">
    <ItemGroup>
      <i Remove=""r"" />
    </ItemGroup>
  </Target>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add item with remove in itemgroup in target
        /// </summary>
        [Fact]
        public void AddItemWithRemoveToItemGroupOutsideTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemGroupElement itemGroup = project.CreateItemGroupElement();
            ProjectItemElement itemRemoveFirst = project.CreateItemElement("i");
            ProjectItemElement itemInclude = project.CreateItemElement("i");
            ProjectItemElement itemRemoveSecond = project.CreateItemElement("i");
            ProjectItemElement itemUpdate = project.CreateItemElement("i");
            ProjectItemElement itemRemoveThird = project.CreateItemElement("i");

            itemRemoveFirst.Remove = "i";
            itemInclude.Include = "i";
            itemRemoveSecond.Remove = "i";
            itemUpdate.Update = "i";
            itemRemoveThird.Remove = "i";

            project.AppendChild(itemGroup);
            itemGroup.AppendChild(itemRemoveFirst);
            itemGroup.InsertAfterChild(itemInclude, itemRemoveFirst);
            itemGroup.InsertAfterChild(itemRemoveSecond, itemInclude);
            itemGroup.InsertAfterChild(itemUpdate, itemRemoveSecond);
            itemGroup.InsertAfterChild(itemRemoveThird, itemUpdate);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Remove=""i"" />
    <i Include=""i"" />
    <i Remove=""i"" />
    <i Update=""i"" />
    <i Remove=""i"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        public delegate void AddMetadata(ProjectItemElement element);

        public static IEnumerable<object[]> InsertMetadataElemenetAfterSiblingsTestData
        {
            get
            {
                yield return new object[]
                {
                    new AddMetadata((e) => { e.AddMetadata("a", "value_a", true); }), // operations on an ProjectItemElement
                    0, // insert metadata after the 1st metadata
                    @"<i Include=`a` a=`value_a`>
                        <m>v</m>
                      </i>" // expected item
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", false);
                    }),
                    0,
                    @"<i Include=`a`>
                        <a>value_a</a>
                        <m>v</m>
                      </i>"
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", true);
                        e.AddMetadata("b", "value_b", true);
                    }),
                    0,
                    @"<i Include=`a` a=`value_a` b=`value_b`>
                        <m>v</m>
                      </i>"
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", true);
                        e.AddMetadata("b", "value_b", true);
                        e.AddMetadata("c", "value_c", false);
                        e.AddMetadata("d", "value_d", true);
                        e.AddMetadata("e", "value_e", false);
                    }),
                    1,
                    @"<i Include=`a` a=`value_a` b=`value_b` d=`value_d`>
                        <m>v</m>
                        <c>value_c</c>
                        <e>value_e</e>
                      </i>"
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", false);
                        e.AddMetadata("b", "value_b", true);
                        e.AddMetadata("c", "value_c", true);
                    }),
                    0,
                    @"<i Include=`a` b=`value_b` c=`value_c`>
                        <a>value_a</a>
                        <m>v</m>
                      </i>"
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", false);
                        e.AddMetadata("b", "value_b", true);
                        e.AddMetadata("c", "value_c", true);
                    }),
                    1,
                    @"<i Include=`a` b=`value_b` c=`value_c`>
                        <a>value_a</a>
                        <m>v</m>
                      </i>"
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", false);
                        e.AddMetadata("b", "value_b", true);
                        e.AddMetadata("c", "value_c", true);
                    }),
                    2,
                    @"<i Include=`a` b=`value_b` c=`value_c`>
                        <a>value_a</a>
                        <m>v</m>
                      </i>"
                };

            }
        }

        [Theory]
        [MemberData(nameof(InsertMetadataElemenetAfterSiblingsTestData))]
        public void InsertMetadataElementAfterSiblings(AddMetadata addMetadata, int position, string expectedItem)
        {
            Action<ProjectItemElement, ProjectMetadataElement, ProjectMetadataElement> act = (i, c, r) => { i.InsertAfterChild(c, r); };

            AssertMetadataConstruction(addMetadata, position, expectedItem, act);
        }

        public static IEnumerable<object[]> InsertMetadataElemenetBeforeSiblingsTestData
        {
            get
            {
                yield return new object[]
                {
                    new AddMetadata((e) => { e.AddMetadata("a", "value_a", true); }), // operations on an ProjectItemElement
                    0, // insert metadata before the 1st metadata
                    @"<i Include=`a` a=`value_a`>
                        <m>v</m>
                      </i>" // expected item
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", true);
                        e.AddMetadata("b", "value_b", true);
                        e.AddMetadata("c", "value_c", false);
                        e.AddMetadata("d", "value_d", true);
                        e.AddMetadata("e", "value_e", false);
                    }),
                    0,
                    @"<i Include=`a` a=`value_a` b=`value_b` d=`value_d`>
                        <m>v</m>
                        <c>value_c</c>
                        <e>value_e</e>
                      </i>"
                };
            }
        }

        [Theory]
        [MemberData(nameof(InsertMetadataElemenetBeforeSiblingsTestData))]
        public void InsertMetadataElementBeforeSiblings(AddMetadata addMetadata, int position, string expectedItem)
        {
            Action<ProjectItemElement, ProjectMetadataElement, ProjectMetadataElement> act = (i, c, r) => { i.InsertBeforeChild(c, r);};

            AssertMetadataConstruction(addMetadata, position, expectedItem, act);
        }

        public static IEnumerable<object[]> InsertMetadataAttributeAfterSiblingsTestData
        {
            get
            {
                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", true);
                        e.AddMetadata("b", "value_b", true);
                    }), // operations on an ProjectItemElement
                    0, // insert metadata after the 1st metadata
                    @"<i Include=`a` a=`value_a` m=`v` b=`value_b` \>" // expected item
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", true);
                        e.AddMetadata("b", "value_b", true);
                    }),
                    1,
                    @"<i Include=`a` a=`value_a` b=`value_b` m=`v` \>"
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", true);
                        e.AddMetadata("b", "value_b", false);
                        e.AddMetadata("c", "value_c", false);
                    }),
                    2,
                    @"<i Include=`a` a=`value_a` m=`v`>
                        <b>value_b</b>
                        <c>value_c</c>
                      </i>"
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", false);
                        e.AddMetadata("b", "value_b", false);
                    }),
                    1,
                    @"<i Include=`a` m=`v`>
                        <a>value_a</a>
                        <b>value_b</b>
                      </i>"
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", false);
                        e.AddMetadata("b", "value_b", false);
                        e.AddMetadata("c", "value_c", true);
                    }),
                    1,
                    @"<i Include=`a` m=`v` c=`value_c`>
                        <a>value_a</a>
                        <b>value_b</b>
                      </i>"
                };

            }
        }

        [Theory(Skip= "https://github.com/Microsoft/msbuild/issues/1253")]
        [MemberData(nameof(InsertMetadataAttributeAfterSiblingsTestData))]
        public void InsertMetadataAttributeAfterSiblings(AddMetadata addMetadata, int position, string expectedItem)
        {
            Action<ProjectItemElement, ProjectMetadataElement, ProjectMetadataElement> act = (i, c, r) =>
            {
                c.ExpressedAsAttribute = true;
                i.InsertAfterChild(c, r);
            };

            AssertMetadataConstruction(addMetadata, position, expectedItem, act);
        }

        public static IEnumerable<object[]> InsertMetadataAttributeBeforeSiblingsTestData
        {
            get
            {
                yield return new object[]
                {
                    new AddMetadata((e) => { e.AddMetadata("a", "value_a", false); }), // operations on an ProjectItemElement
                    0, // insert metadata before the 1st metadata
                    @"<i Include=`a` m=`v`>
                        <a>value_a</a>
                      </i>" // expected item
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", false);
                        e.AddMetadata("b", "value_b", false);
                        e.AddMetadata("c", "value_c", true);
                        e.AddMetadata("d", "value_d", false);
                        e.AddMetadata("e", "value_e", true);
                    }),
                    0,
                    @"<i Include=`a` m=`v` c=`value_c` e=`value_e`>
                        <a>value_a</a>
                        <b>value_b</b>
                        <d>value_d</d>
                      </i>"
                };

                yield return new object[]
                {
                    new AddMetadata((e) =>
                    {
                        e.AddMetadata("a", "value_a", false);
                        e.AddMetadata("b", "value_b", false);
                        e.AddMetadata("c", "value_c", true);
                        e.AddMetadata("d", "value_d", false);
                        e.AddMetadata("e", "value_e", true);
                    }),
                    3,
                    @"<i Include=`a` c=`value_c` m=`v` e=`value_e`>
                        <a>value_a</a>
                        <b>value_b</b>
                        <d>value_d</d>
                      </i>"
                };
            }
        }

        [Theory(Skip= "https://github.com/Microsoft/msbuild/issues/1253")]
        [MemberData(nameof(InsertMetadataAttributeBeforeSiblingsTestData))]
        public void InsertMetadataAttributeBeforeSiblings(AddMetadata addMetadata, int position, string expectedItem)
        {
            Action<ProjectItemElement, ProjectMetadataElement, ProjectMetadataElement> act = (i, c, r) =>
            {
                c.ExpressedAsAttribute = true;
                i.InsertBeforeChild(c, r);
            };

            AssertMetadataConstruction(addMetadata, position, expectedItem, act);
        }

        private static void AssertMetadataConstruction(AddMetadata addMetadata, int position, string expectedItem, Action<ProjectItemElement, ProjectMetadataElement, ProjectMetadataElement> actOnTestData)
        {
            var project = ProjectRootElement.Create();
            var itemGroup = project.AddItemGroup();
            var item = itemGroup.AddItem("i", "a");

            addMetadata(item);

            var referenceSibling = item.Metadata.ElementAt(position);
            var m = project.CreateMetadataElement("m", "v");

            actOnTestData(item, m, referenceSibling);

            var expected = ComposeExpectedProjectString(expectedItem);

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        [Fact]
        public void AddItemWithUpdateAtSpecificLocation()
        {
            ProjectRootElement project = CreateProjectWithUpdates();

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""a"" />
    <i Update=""a"">
      <m1>metadata1</m1>
    </i>
    <i Include=""a"" />
    <i Update=""a"">
      <m1>metadata2</m1>
    </i>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        [Fact]
        public void DeleteItemWithUpdateFromSpecificLocations()
        {
            ProjectRootElement project = CreateProjectWithUpdates();

            var itemUpdateElements = project.Items.Where(i => i.UpdateLocation != null);

            foreach (var updateElement in itemUpdateElements)
            {
                updateElement.Parent.RemoveChild(updateElement);
            }

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""a"" />
    <i Include=""a"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        private static ProjectRootElement CreateProjectWithUpdates()
        {
            var project = ProjectRootElement.Create();
            var itemGroup = project.CreateItemGroupElement();
            var firstIncludeItem = project.CreateItemElement("i");
            var secondIncludeItem = project.CreateItemElement("i");
            var firstUpdateItem = project.CreateItemElement("i");
            var secondUpdateItem = project.CreateItemElement("i");
            var firstMetadata = project.CreateMetadataElement("m1");
            var secondMetadata = project.CreateMetadataElement("m1");

            firstIncludeItem.Include = "a";
            secondIncludeItem.Include = "a";
            firstUpdateItem.Update = "a";
            secondUpdateItem.Update = "a";
            firstMetadata.Value = "metadata1";
            secondMetadata.Value = "metadata2";

            project.AppendChild(itemGroup);
            itemGroup.AppendChild(firstIncludeItem);
            itemGroup.AppendChild(secondIncludeItem);

            // add update between two include items
            itemGroup.InsertAfterChild(firstUpdateItem, firstIncludeItem);
            firstUpdateItem.AppendChild(firstMetadata);

            // add update as the last child
            itemGroup.AppendChild(secondUpdateItem);
            secondUpdateItem.AppendChild(secondMetadata);
            return project;
        }

        /// <summary>
        /// Remove a target
        /// </summary>
        [Fact]
        public void RemoveSingleChildTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target = project.AddTarget("t");
            Helpers.ClearDirtyFlag(project);

            project.RemoveChild(target);

            string expected = ObjectModelHelpers.CleanupFileContents(@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"" />");

            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(0, Helpers.Count(project.Children));
        }

        /// <summary>
        /// Attempt to remove a child that is not parented
        /// </summary>
        [Fact]
        public void InvalidRemoveUnparentedChild()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectTargetElement target = project.CreateTargetElement("t");
                project.RemoveChild(target);
            }
           );
        }
        /// <summary>
        /// Attempt to remove a child that is parented by something in another project
        /// </summary>
        [Fact]
        public void InvalidRemoveChildFromOtherProject()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectRootElement project1 = ProjectRootElement.Create();
                ProjectTargetElement target = project1.CreateTargetElement("t");
                ProjectRootElement project2 = ProjectRootElement.Create();

                project2.RemoveChild(target);
            }
           );
        }
        /// <summary>
        /// Attempt to remove a child that is parented by something else in the same project
        /// </summary>
        [Fact]
        public void InvalidRemoveChildFromOtherParent()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectItemGroupElement itemGroup1 = project.CreateItemGroupElement();
                ProjectItemGroupElement itemGroup2 = project.CreateItemGroupElement();
                ProjectItemElement item = project.CreateItemElement("i");
                itemGroup1.AppendChild(item);

                itemGroup2.RemoveChild(item);
            }
           );
        }
        /// <summary>
        /// Attempt to add an Otherwise before a When
        /// </summary>
        [Fact]
        public void InvalidOtherwiseBeforeWhen()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectChooseElement choose = project.CreateChooseElement();
                ProjectWhenElement when = project.CreateWhenElement("c");
                ProjectOtherwiseElement otherwise = project.CreateOtherwiseElement();

                project.AppendChild(choose);
                choose.AppendChild(when);
                choose.InsertBeforeChild(otherwise, when);
            }
           );
        }
        /// <summary>
        /// Attempt to add an Otherwise after another
        /// </summary>
        [Fact]
        public void InvalidOtherwiseAfterOtherwise()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectChooseElement choose = project.CreateChooseElement();
                project.AppendChild(choose);
                choose.AppendChild(project.CreateWhenElement("c"));
                choose.AppendChild(project.CreateOtherwiseElement());
                choose.AppendChild(project.CreateOtherwiseElement());
            }
           );
        }
        /// <summary>
        /// Attempt to add an Otherwise before another
        /// </summary>
        [Fact]
        public void InvalidOtherwiseBeforeOtherwise()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectChooseElement choose = project.CreateChooseElement();
                project.AppendChild(choose);
                choose.AppendChild(project.CreateWhenElement("c"));
                choose.AppendChild(project.CreateOtherwiseElement());
                choose.InsertAfterChild(project.CreateOtherwiseElement(), choose.FirstChild);
            }
           );
        }
        /// <summary>
        /// Attempt to add a When after an Otherwise
        /// </summary>
        [Fact]
        public void InvalidWhenAfterOtherwise()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                ProjectChooseElement choose = project.CreateChooseElement();
                ProjectWhenElement when = project.CreateWhenElement("c");
                ProjectOtherwiseElement otherwise = project.CreateOtherwiseElement();

                project.AppendChild(choose);
                choose.AppendChild(otherwise);
                choose.InsertAfterChild(when, otherwise);
            }
           );
        }
        /// <summary>
        /// Add When before Otherwise
        /// </summary>
        [Fact]
        public void WhenBeforeOtherwise()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectChooseElement choose = project.CreateChooseElement();
            ProjectWhenElement when = project.CreateWhenElement("c");
            ProjectOtherwiseElement otherwise = project.CreateOtherwiseElement();

            project.AppendChild(choose);
            choose.AppendChild(when);
            choose.AppendChild(otherwise);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Choose>
    <When Condition=""c"" />
    <Otherwise />
  </Choose>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(1, Helpers.Count(project.Children));
            Assert.Equal(2, Helpers.Count(choose.Children));
        }

        /// <summary>
        /// Remove a target that is last in a list
        /// </summary>
        [Fact]
        public void RemoveLastInList()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");
            ProjectTargetElement target2 = project.AddTarget("t2");

            project.RemoveChild(target2);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t1"" />
</Project>");

            Assert.Equal(1, project.Count);
            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(1, Helpers.Count(project.Children));
            Assert.Equal(target1, Helpers.GetFirst(project.Children));
        }

        /// <summary>
        /// Remove a target that is first in a list
        /// </summary>
        [Fact]
        public void RemoveFirstInList()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");
            ProjectTargetElement target2 = project.AddTarget("t2");

            project.RemoveChild(target1);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t2"" />
</Project>");

            Assert.Equal(1, project.Count);
            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(1, Helpers.Count(project.Children));
            Assert.Equal(target2, Helpers.GetFirst(project.Children));
        }

        /// <summary>
        /// Remove all children when there are some
        /// </summary>
        [Fact]
        public void RemoveAllChildrenSome()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");
            ProjectTargetElement target2 = project.AddTarget("t2");

            project.RemoveAllChildren();

            Assert.Equal(0, project.Count);
            Assert.Null(target1.Parent);
            Assert.Null(target2.Parent);
        }

        /// <summary>
        /// Remove all children when there aren't any. Shouldn't fail.
        /// </summary>
        [Fact]
        public void RemoveAllChildrenNone()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");

            target1.RemoveAllChildren();

            Assert.Equal(0, target1.Count);
        }

        /// <summary>
        /// Remove and re-insert a node
        /// </summary>
        [Fact]
        public void RemoveReinsertHasSiblingAppend()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");
            ProjectTargetElement target2 = project.AddTarget("t2");

            project.RemoveChild(target1);
            project.AppendChild(target1);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t2"" />
  <Target Name=""t1"" />
</Project>");

            Assert.Equal(2, project.Count);
            Assert.True(project.HasUnsavedChanges);
            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(2, Helpers.Count(project.Children));
            Assert.Equal(target2, Helpers.GetFirst(project.Children));
        }

        /// <summary>
        /// Remove and re-insert a node
        /// </summary>
        [Fact]
        public void RemoveReinsertHasSiblingPrepend()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");
            project.AddTarget("t2");

            project.RemoveChild(target1);
            project.PrependChild(target1);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t1"" />
  <Target Name=""t2"" />
</Project>");

            Assert.Equal(2, project.Count);
            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Remove and re-insert a node
        /// </summary>
        [Fact]
        public void RemoveReinsertTwoChildrenAppend()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");
            project.AddTarget("t2");

            project.RemoveAllChildren();
            project.AppendChild(target1);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t1"" />
</Project>");

            Assert.Equal(1, project.Count);
            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Remove and re-insert a node with no siblings using PrependChild
        /// </summary>
        [Fact]
        public void RemoveLonelyReinsertPrepend()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");

            project.RemoveChild(target1);
            project.PrependChild(target1);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t1"" />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Remove and re-insert a node with no siblings using AppendChild
        /// </summary>
        [Fact]
        public void RemoveLonelyReinsertAppend()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");

            project.RemoveAllChildren();
            project.AppendChild(target1);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t1"" />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Test the AddPropertyGroup convenience method
        /// It adds after the last existing property group, if any; otherwise
        /// at the start of the project.
        /// </summary>
        [Fact]
        public void AddPropertyGroup_NoExistingPropertyGroups()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddTarget("t1");
            project.AddTarget("t2");

            ProjectPropertyGroupElement propertyGroup = project.AddPropertyGroup();

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup />
  <Target Name=""t1"" />
  <Target Name=""t2"" />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(3, Helpers.Count(project.Children));
            Assert.Equal(propertyGroup, Helpers.GetFirst(project.Children));
        }

        /// <summary>
        /// Test the AddPropertyGroup convenience method
        /// It adds after the last existing property group, if any; otherwise
        /// at the start of the project.
        /// </summary>
        [Fact]
        public void AddPropertyGroup_ExistingPropertyGroups()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectTargetElement target1 = project.AddTarget("t1");
            ProjectTargetElement target2 = project.AddTarget("t2");
            ProjectPropertyGroupElement propertyGroup1 = project.CreatePropertyGroupElement();
            ProjectPropertyGroupElement propertyGroup2 = project.CreatePropertyGroupElement();

            project.InsertAfterChild(propertyGroup1, target1);
            project.InsertAfterChild(propertyGroup2, target2);

            ProjectPropertyGroupElement propertyGroup3 = project.AddPropertyGroup();

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t1"" />
  <PropertyGroup />
  <Target Name=""t2"" />
  <PropertyGroup />
  <PropertyGroup />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(5, Helpers.Count(project.Children));
            Assert.Equal(propertyGroup3, Helpers.GetLast(project.Children));
        }

        /// <summary>
        /// Add an item group to an empty project
        /// </summary>
        [Fact]
        public void AddItemGroup_NoExistingElements()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemGroup();

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item group to a project with an existing item group; should add 2nd
        /// </summary>
        [Fact]
        public void AddItemGroup_OneExistingItemGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemGroup();
            ProjectItemGroupElement itemGroup2 = project.AddItemGroup();

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup />
  <ItemGroup />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(itemGroup2, Helpers.GetLast(project.ItemGroups));
        }

        /// <summary>
        /// Add an item group to a project with an existing property group; should add 2nd
        /// </summary>
        [Fact]
        public void AddItemGroup_OneExistingPropertyGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddPropertyGroup();
            project.AddItemGroup();

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup />
  <ItemGroup />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item group to a project with an existing property group and item group;
        /// should add after the item group
        /// </summary>
        [Fact]
        public void AddItemGroup_ExistingItemGroupAndPropertyGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemGroup();
            project.AppendChild(project.CreatePropertyGroupElement());
            ProjectItemGroupElement itemGroup2 = project.AddItemGroup();

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup />
  <ItemGroup />
  <PropertyGroup />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(itemGroup2, Helpers.GetLast(project.ItemGroups));
        }

        /// <summary>
        /// Add an item group to a project with an existing target;
        /// should add at the end
        /// </summary>
        [Fact]
        public void AddItemGroup_ExistingTarget()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddTarget("t");
            project.AddItemGroup();

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Target Name=""t"" />
  <ItemGroup />
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item to an empty project
        /// should add to new item group
        /// </summary>
        [Fact]
        public void AddItem_EmptyProject()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item to a project that only has an empty item group,
        /// should reuse that group
        /// </summary>
        [Fact]
        public void AddItem_ExistingEmptyItemGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemGroup();
            project.AddItem("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item to a project that only has an empty item group,
        /// should reuse that group, unless it has a condition
        /// </summary>
        [Fact]
        public void AddItem_ExistingEmptyItemGroupWithCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemGroupElement itemGroup = project.AddItemGroup();
            itemGroup.Condition = "c";
            project.AddItem("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup Condition=""c"" />
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item to a project that only has an item group with items of a different type,
        /// and an empty item group, should reuse that group
        /// </summary>
        [Fact]
        public void AddItem_ExistingEmptyItemGroupPlusItemGroupOfWrongType()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemGroupElement itemGroup = project.AddItemGroup();
            itemGroup.AddItem("h", "h1");
            project.AddItemGroup();
            project.AddItem("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <h Include=""h1"" />
  </ItemGroup>
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item to a project that only has an item group with items of a different type,
        /// and an empty item group above it, should reuse the empty group
        /// </summary>
        [Fact]
        public void AddItem_ExistingEmptyItemGroupPlusItemGroupOfWrongTypeBelow()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemGroup();
            ProjectItemGroupElement itemGroup = project.AddItemGroup();
            itemGroup.AddItem("h", "h1");
            ProjectItemElement item = project.AddItem("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
  <ItemGroup>
    <h Include=""h1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(item, Helpers.GetFirst(Helpers.GetFirst(project.ItemGroups).Items));
        }

        /// <summary>
        /// Add an item to a project with a single item group with existing items
        /// of a different item type; should add in alpha order of item type
        /// </summary>
        [Fact]
        public void AddItem_ExistingItemGroupWithItemsOfDifferentItemType()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "i1");
            project.AddItem("j", "j1");
            project.AddItem("h", "h1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
  <ItemGroup>
    <j Include=""j1"" />
  </ItemGroup>
  <ItemGroup>
    <h Include=""h1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item to a project with a single item group with existing items of
        /// same item type; should add in alpha order of itemspec
        /// </summary>
        [Fact]
        public void AddItem_ExistingItemGroupWithItemsOfSameItemType()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "i1");
            project.AddItem("i", "j1");
            project.AddItem("i", "h1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""h1"" />
    <i Include=""i1"" />
    <i Include=""j1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item to a project with an existing item group with items of a different
        /// type; should create a new item group
        /// </summary>
        [Fact]
        public void AddItem_ExistingItemGroupWithDifferentItemType()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "i1");
            project.AddItem("j", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
  <ItemGroup>
    <j Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item to a project with a single item group with existing items of
        /// various item types and item specs; should add in alpha order of item type,
        /// then item spec, keeping different item specs in different groups; different
        /// item groups are not mutually sorted
        /// </summary>
        [Fact]
        public void AddItem_ExistingItemGroupWithVariousItems()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "i1");
            project.AddItem("i", "j1");
            project.AddItem("j", "h1");
            project.AddItem("i", "h1");
            project.AddItem("h", "j1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""h1"" />
    <i Include=""i1"" />
    <i Include=""j1"" />
  </ItemGroup>
  <ItemGroup>
    <j Include=""h1"" />
  </ItemGroup>
  <ItemGroup>
    <h Include=""j1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Adding an item that's identical to an existing one should add it again and not skip
        /// </summary>
        [Fact]
        public void AddItem_Duplicate()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItem("i", "i1");
            project.AddItem("i", "i1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
    <i Include=""i1"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Adding items to when and Otherwise
        /// </summary>
        [Fact]
        public void AddItemToWhereOtherwise()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectChooseElement choose = project.CreateChooseElement();
            ProjectWhenElement when = project.CreateWhenElement("c");
            ProjectItemGroupElement ig1 = project.CreateItemGroupElement();
            project.AppendChild(choose);
            choose.AppendChild(when);
            when.AppendChild(ig1);
            ig1.AddItem("j", "j1");

            ProjectOtherwiseElement otherwise = project.CreateOtherwiseElement();
            ProjectItemGroupElement ig2 = project.CreateItemGroupElement();
            choose.AppendChild(otherwise);
            otherwise.AppendChild(ig2);
            ig2.AddItem("j", "j2");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <Choose>
    <When Condition=""c"">
      <ItemGroup>
        <j Include=""j1"" />
      </ItemGroup>
    </When>
    <Otherwise>
      <ItemGroup>
        <j Include=""j2"" />
      </ItemGroup>
    </Otherwise>
  </Choose>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Adding items to a specific item group should order them by item type and item spec
        /// </summary>
        [Fact]
        public void AddItemToItemGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemGroupElement itemGroup = project.AddItemGroup();
            itemGroup.AddItem("j", "j1");
            itemGroup.AddItem("i", "i1");
            itemGroup.AddItem("h", "h1");
            itemGroup.AddItem("j", "j2");
            itemGroup.AddItem("j", "j0");
            itemGroup.AddItem("h", "h0");
            itemGroup.AddItem("g", "zzz");
            itemGroup.AddItem("k", "aaa");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <g Include=""zzz"" />
    <h Include=""h0"" />
    <h Include=""h1"" />
    <i Include=""i1"" />
    <j Include=""j0"" />
    <j Include=""j1"" />
    <j Include=""j2"" />
    <k Include=""aaa"" />
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item definition to an empty project
        /// should add to new item definition group
        /// </summary>
        [Fact]
        public void AddItemDefinition_EmptyProject()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemDefinitionElement itemDefinition = project.AddItemDefinition("i");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i />
  </ItemDefinitionGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(itemDefinition, Helpers.GetFirst(Helpers.GetFirst(project.ItemDefinitionGroups).ItemDefinitions));
        }

        /// <summary>
        /// Add an item definition to a project with a single empty item definition group;
        /// should create another, because it doesn't have any items of the same type
        /// </summary>
        [Fact]
        public void AddItemDefinition_ExistingItemDefinitionGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemDefinitionGroup();
            project.AddItemDefinition("i");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup />
  <ItemDefinitionGroup>
    <i />
  </ItemDefinitionGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item definition to a project with a single empty item definition group with a condition;
        /// should create a new one after
        /// </summary>
        [Fact]
        public void AddItemDefinition_ExistingItemDefinitionGroupWithCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectItemDefinitionGroupElement itemGroup = project.AddItemDefinitionGroup();
            itemGroup.Condition = "c";
            project.AddItemDefinition("i");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup Condition=""c"" />
  <ItemDefinitionGroup>
    <i />
  </ItemDefinitionGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add an item definition to a project with a single item definitiongroup with existing items of
        /// same item type; should add in same one
        /// </summary>
        [Fact]
        public void AddItemDefinition_ExistingItemDefinitionGroupWithItemsOfSameItemType()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemDefinition("i");
            project.AddItemDefinition("i");
            ProjectItemDefinitionElement last = project.AddItemDefinition("i");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i />
    <i />
    <i />
  </ItemDefinitionGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(last, Helpers.GetLast(Helpers.GetFirst(project.ItemDefinitionGroups).ItemDefinitions));
        }

        /// <summary>
        /// Add an item definition to a project with an existing item definition group with items of a different
        /// type; should create a new item definition group
        /// </summary>
        [Fact]
        public void AddItemDefinition_ExistingItemDefinitionGroupWithDifferentItemType()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddItemDefinition("i");
            project.AddItemDefinition("j");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemDefinitionGroup>
    <i />
  </ItemDefinitionGroup>
  <ItemDefinitionGroup>
    <j />
  </ItemDefinitionGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add a property to an empty project
        /// should add to new property group
        /// </summary>
        [Fact]
        public void AddProperty_EmptyProject()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property = project.AddProperty("p", "v1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.Equal(property, Helpers.GetFirst(Helpers.GetFirst(project.PropertyGroups).Properties));
        }

        /// <summary>
        /// Add a property to a project with an existing property group
        /// should add to property group
        /// </summary>
        [Fact]
        public void AddProperty_ExistingPropertyGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            project.AddPropertyGroup();
            project.AddProperty("p", "v1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add a property to a project with an existing property group with condition
        /// should add to new property group
        /// </summary>
        [Fact]
        public void AddProperty_ExistingPropertyGroupWithCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyGroupElement propertyGroup = project.AddPropertyGroup();
            propertyGroup.Condition = "c";

            project.AddProperty("p", "v1");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup Condition=""c"" />
  <PropertyGroup>
    <p>v1</p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add a property to a project with an existing property with the same name
        /// should modify and return existing property
        /// </summary>
        [Fact]
        public void AddProperty_ExistingPropertySameName()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property1 = project.AddProperty("p", "v1");

            ProjectPropertyElement property2 = project.AddProperty("p", "v2");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p>v2</p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
            Assert.True(Object.ReferenceEquals(property1, property2));
        }

        /// <summary>
        /// Add a property to a project with an existing property with the same name but a condition;
        /// should add new property
        /// </summary>
        [Fact]
        public void AddProperty_ExistingPropertySameNameCondition()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property1 = project.AddProperty("p", "v1");
            property1.Condition = "c";

            project.AddProperty("p", "v2");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup>
    <p Condition=""c"">v1</p>
    <p>v2</p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Add a property to a project with an existing property with the same name but a condition;
        /// should add new property
        /// </summary>
        [Fact]
        public void AddProperty_ExistingPropertySameNameConditionOnGroup()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectPropertyElement property1 = project.AddProperty("p", "v1");
            property1.Parent.Condition = "c";

            project.AddProperty("p", "v2");

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <PropertyGroup Condition=""c"">
    <p>v1</p>
  </PropertyGroup>
  <PropertyGroup>
    <p>v2</p>
  </PropertyGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Attempt to add a property with a reserved name
        /// </summary>
        [Fact]
        public void InvalidAddPropertyReservedName()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                project.AddProperty("MSBuildToolsPATH", "v");
            }
           );
        }
        /// <summary>
        /// Attempt to add a property with an illegal name
        /// </summary>
        [Fact]
        public void InvalidAddPropertyIllegalName()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                project.AddProperty("ItemGroup", "v");
            }
           );
        }
        /// <summary>
        /// Attempt to add a property with an invalid XML name
        /// </summary>
        [Fact]
        public void InvalidAddPropertyInvalidXmlName()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ProjectRootElement project = ProjectRootElement.Create();
                project.AddProperty("@#$@#", "v");
            }
           );
        }
        /// <summary>
        /// Too much nesting should not cause stack overflow.
        /// </summary>
        [Fact]
        public void InvalidChooseOverflow()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectElementContainer current = project;

            Action infiniteChooseLoop = () =>
            {
                while (true)
                {
                    ProjectChooseElement choose = project.CreateChooseElement();
                    ProjectWhenElement when = project.CreateWhenElement("c");
                    current.AppendChild(choose);
                    choose.AppendChild(when);
                    current = when;
                }
            };

            Assert.Throws<InvalidProjectFileException>(infiniteChooseLoop);
        }
        /// <summary>
        /// Setting item condition should dirty project
        /// </summary>
        [Fact]
        public void Dirtying_ItemCondition()
        {
            XmlReader content = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"" />
  </ItemGroup>
</Project>")));

            Project project = new Project(content);
            ProjectItem item = Helpers.GetFirst(project.Items);

            item.Xml.Condition = "false";

            Assert.Equal(1, Helpers.Count(project.Items));

            project.ReevaluateIfNecessary();

            Assert.Equal(0, Helpers.Count(project.Items));
        }

        /// <summary>
        /// Setting metadata condition should dirty project
        /// </summary>
        [Fact]
        public void Dirtying_MetadataCondition()
        {
            XmlReader content = XmlReader.Create(new StringReader(ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i Include=""i1"">
      <m>m1</m>
    </i>
  </ItemGroup>
</Project>")));

            Project project = new Project(content);
            ProjectMetadata metadatum = Helpers.GetFirst(project.Items).GetMetadata("m");

            metadatum.Xml.Condition = "false";

            Assert.Equal("m1", metadatum.EvaluatedValue);

            project.ReevaluateIfNecessary();
            metadatum = Helpers.GetFirst(project.Items).GetMetadata("m");

            Assert.Null(metadatum);
        }

        /// <summary>
        /// Delete all the children of a container, then add them
        /// to a new one, and iterate. Should not go into infinite loop :-)
        /// </summary>
        [Fact]
        public void DeleteAllChildren()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            ProjectItemGroupElement group1 = xml.AddItemGroup();
            ProjectItemElement item1 = group1.AddItem("i", "i1");
            ProjectItemElement item2 = group1.AddItem("i", "i2");
            group1.RemoveChild(item1);
            group1.RemoveChild(item2);

            ProjectItemGroupElement group2 = xml.AddItemGroup();
            group2.AppendChild(item1);
            group2.AppendChild(item2);

            List<ProjectElement> allChildren = new List<ProjectElement>(group2.AllChildren);

            Helpers.AssertListsValueEqual(allChildren, new List<ProjectElement> { item1, item2 });
            Assert.Equal(0, group1.Count);
        }

        /// <summary>
        /// Same but with Prepend for the 2nd one
        /// </summary>
        [Fact]
        public void DeleteAllChildren2()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            ProjectItemGroupElement group1 = xml.AddItemGroup();
            ProjectItemElement item1 = group1.AddItem("i", "i1");
            ProjectItemElement item2 = group1.AddItem("i", "i2");
            group1.RemoveChild(item1);
            group1.RemoveChild(item2);

            ProjectItemGroupElement group2 = xml.AddItemGroup();
            group2.AppendChild(item1);
            group2.PrependChild(item2);

            List<ProjectElement> allChildren = new List<ProjectElement>(group2.AllChildren);

            Helpers.AssertListsValueEqual(allChildren, new List<ProjectElement> { item2, item1 });
            Assert.Equal(0, group1.Count);
        }

        /// <summary>
        /// Same but with InsertBefore for the 2nd one
        /// </summary>
        [Fact]
        public void DeleteAllChildren3()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            ProjectItemGroupElement group1 = xml.AddItemGroup();
            ProjectItemElement item1 = group1.AddItem("i", "i1");
            ProjectItemElement item2 = group1.AddItem("i", "i2");
            group1.RemoveChild(item1);
            group1.RemoveChild(item2);

            ProjectItemGroupElement group2 = xml.AddItemGroup();
            group2.AppendChild(item1);
            group2.InsertBeforeChild(item2, item1);

            List<ProjectElement> allChildren = new List<ProjectElement>(group2.AllChildren);

            Helpers.AssertListsValueEqual(allChildren, new List<ProjectElement> { item2, item1 });
            Assert.Equal(0, group1.Count);
        }

        /// <summary>
        /// Same but with InsertAfter for the 2nd one
        /// </summary>
        [Fact]
        public void DeleteAllChildren4()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            ProjectItemGroupElement group1 = xml.AddItemGroup();
            ProjectItemElement item1 = group1.AddItem("i", "i1");
            ProjectItemElement item2 = group1.AddItem("i", "i2");
            group1.RemoveChild(item1);
            group1.RemoveChild(item2);

            ProjectItemGroupElement group2 = xml.AddItemGroup();
            group2.AppendChild(item1);
            group2.InsertAfterChild(item2, item1);

            List<ProjectElement> allChildren = new List<ProjectElement>(group2.AllChildren);

            Helpers.AssertListsValueEqual(allChildren, new List<ProjectElement> { item1, item2 });
            Assert.Equal(0, group1.Count);
        }

        /// <summary>
        /// Same but with InsertAfter for the 2nd one
        /// </summary>
        [Fact]
        public void DeleteAllChildren5()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            ProjectItemGroupElement group1 = xml.AddItemGroup();
            ProjectItemElement item1 = group1.AddItem("i", "i1");
            ProjectItemElement item2 = group1.AddItem("i", "i2");
            group1.RemoveChild(item1);
            group1.RemoveChild(item2);

            ProjectItemGroupElement group2 = xml.AddItemGroup();
            group2.AppendChild(item1);
            group2.InsertAfterChild(item2, item1);

            List<ProjectElement> allChildren = new List<ProjectElement>(group2.AllChildren);

            Helpers.AssertListsValueEqual(allChildren, new List<ProjectElement> { item1, item2 });
            Assert.Equal(0, group1.Count);
        }

        /// <summary>
        /// Move some children
        /// </summary>
        [Fact]
        public void DeleteSomeChildren()
        {
            ProjectRootElement xml = ProjectRootElement.Create();
            ProjectItemGroupElement group1 = xml.AddItemGroup();
            ProjectItemElement item1 = group1.AddItem("i", "i1");
            ProjectItemElement item2 = group1.AddItem("i", "i2");
            ProjectItemElement item3 = group1.AddItem("i", "i3");
            group1.RemoveChild(item1);
            group1.RemoveChild(item2);

            ProjectItemGroupElement group2 = xml.AddItemGroup();
            group2.AppendChild(item1);
            group2.AppendChild(item2);

            List<ProjectElement> allChildren = new List<ProjectElement>(group2.AllChildren);

            Helpers.AssertListsValueEqual(allChildren, new List<ProjectElement> { item1, item2 });
            Assert.Equal(1, group1.Count);
            Assert.True(item3.PreviousSibling == null && item3.NextSibling == null);
            Assert.True(item2.PreviousSibling == item1 && item1.NextSibling == item2);
            Assert.True(item1.PreviousSibling == null && item2.NextSibling == null);
        }

        /// <summary>
        /// Attempt to modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_1()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectImportElement import = project.AddImport("p");
            import.Parent.RemoveAllChildren();
            import.Condition = "c";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_2()
        {
            ProjectRootElement project = ProjectRootElement.Create();
            ProjectImportElement import = project.AddImport("p");
            import.Parent.RemoveAllChildren();
            import.Project = "p";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_3()
        {
            ProjectRootElement.Create().CreateImportGroupElement().Condition = "c";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_4()
        {
            var element = ProjectRootElement.Create().AddItemDefinition("i").AddMetadata("m", "M1");
            element.Parent.RemoveAllChildren();
            element.Value = "v1";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_5()
        {
            var element = ProjectRootElement.Create().AddItem("i", "i1").AddMetadata("m", "M1");
            element.Parent.RemoveAllChildren();
            element.Value = "v1";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_5b()
        {
            var element = ProjectRootElement.Create().AddItem("i", "i1");
            element.Parent.RemoveAllChildren();
            element.ItemType = "j";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_6()
        {
            var element = ProjectRootElement.Create().AddItem("i", "i1");
            element.Parent.RemoveAllChildren();
            element.Include = "i2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_7()
        {
            var element = ProjectRootElement.Create().AddProperty("p", "v1");
            element.Parent.RemoveAllChildren();
            element.Value = "v2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_8()
        {
            var element = ProjectRootElement.Create().AddProperty("p", "v1");
            element.Parent.RemoveAllChildren();
            element.Condition = "c";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_9()
        {
            var element = ProjectRootElement.Create().AddUsingTask("n", "af", null);
            element.Parent.RemoveAllChildren();
            element.TaskName = "n2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_10()
        {
            var element = ProjectRootElement.Create().AddUsingTask("n", "af", null);
            element.Parent.RemoveAllChildren();
            element.AssemblyFile = "af2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_11()
        {
            var element = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            element.Parent.RemoveAllChildren();
            element.AssemblyName = "an2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_12()
        {
            var element = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            element.Parent.RemoveAllChildren();
            element.TaskFactory = "tf";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_15()
        {
            var usingTask = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            usingTask.TaskFactory = "f";
            var element = usingTask.AddParameterGroup().AddParameter("n", "o", "r", "pt");
            element.Parent.RemoveAllChildren();
            element.Name = "n2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_16()
        {
            var usingTask = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            usingTask.TaskFactory = "f";
            var element = usingTask.AddParameterGroup().AddParameter("n", "o", "r", "pt");
            element.Parent.RemoveAllChildren();
            element.Output = "o2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_17()
        {
            var usingTask = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            usingTask.TaskFactory = "f";
            var element = usingTask.AddParameterGroup().AddParameter("n", "o", "r", "pt");
            element.Parent.RemoveAllChildren();
            element.Required = "r2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_18()
        {
            var usingTask = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            usingTask.TaskFactory = "f";
            var element = usingTask.AddParameterGroup().AddParameter("n", "o", "r", "pt");
            element.Parent.RemoveAllChildren();
            element.ParameterType = "pt2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_19()
        {
            var element = ProjectRootElement.Create().AddTarget("t");
            element.Parent.RemoveAllChildren();
            element.Name = "t2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_20()
        {
            var element = ProjectRootElement.Create().AddTarget("t");
            element.Parent.RemoveAllChildren();
            element.Inputs = "i";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_21()
        {
            var element = ProjectRootElement.Create().AddTarget("t");
            element.Parent.RemoveAllChildren();
            element.Outputs = "o";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_22()
        {
            var element = ProjectRootElement.Create().AddTarget("t");
            element.Parent.RemoveAllChildren();
            element.DependsOnTargets = "d";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_23()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt");
            element.Parent.RemoveAllChildren();
            element.SetParameter("p", "v");
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_24()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt");
            element.Parent.RemoveAllChildren();
            element.ContinueOnError = "coe";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_25()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt").AddOutputItem("tp", "i");
            element.Parent.RemoveAllChildren();
            element.TaskParameter = "tp2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_26()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt").AddOutputItem("tp", "i");
            element.Parent.RemoveAllChildren();
            element.ItemType = "tp2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_27()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt").AddOutputProperty("tp", "p");
            element.Parent.RemoveAllChildren();
            element.TaskParameter = "tp2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_28()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt").AddOutputProperty("tp", "p");
            element.Parent.RemoveAllChildren();
            element.PropertyName = "tp2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_29()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddItemGroup().AddItem("i", "i1");
            element.Parent.RemoveAllChildren();
            element.ItemType = "j";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_30()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddItemGroup().AddItem("i", "i1");
            element.Parent.RemoveAllChildren();
            element.Include = "i2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_31()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddItemGroup().AddItem("i", "i1").AddMetadata("m", "m1");
            element.Parent.RemoveAllChildren();
            element.Value = "m2";
        }

        /// <summary>
        /// Legally modify a child that is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedChild_32()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddPropertyGroup().AddProperty("p", "v1");
            element.Parent.RemoveAllChildren();
            element.Value = "v2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_1()
        {
            var element = ProjectRootElement.Create().AddImportGroup().AddImport("p");
            element.Parent.Parent.RemoveAllChildren();
            element.Condition = "c";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_2()
        {
            var element = ProjectRootElement.Create().AddImportGroup().AddImport("p");
            element.Parent.Parent.RemoveAllChildren();
            element.Project = "p";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_3()
        {
            ProjectRootElement.Create().CreateImportGroupElement().Condition = "c";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_4()
        {
            var element = ProjectRootElement.Create().AddItemDefinition("i").AddMetadata("m", "M1");
            element.Parent.Parent.RemoveAllChildren();
            element.Value = "v1";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_5()
        {
            var element = ProjectRootElement.Create().AddItem("i", "i1").AddMetadata("m", "M1");
            element.Parent.Parent.RemoveAllChildren();
            element.Value = "v1";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_5b()
        {
            var element = ProjectRootElement.Create().AddItem("i", "i1");
            element.Parent.Parent.RemoveAllChildren();
            element.ItemType = "j";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_6()
        {
            var element = ProjectRootElement.Create().AddItem("i", "i1");
            element.Parent.Parent.RemoveAllChildren();
            element.Include = "i2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_7()
        {
            var element = ProjectRootElement.Create().AddProperty("p", "v1");
            element.Parent.Parent.RemoveAllChildren();
            element.Value = "v2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_8()
        {
            var element = ProjectRootElement.Create().AddProperty("p", "v1");
            element.Parent.Parent.RemoveAllChildren();
            element.Condition = "c";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_15()
        {
            var usingTask = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            usingTask.TaskFactory = "f";
            var element = usingTask.AddParameterGroup().AddParameter("n", "o", "r", "pt");
            element.Parent.Parent.RemoveAllChildren();
            element.Name = "n2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_16()
        {
            var usingTask = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            usingTask.TaskFactory = "f";
            var element = usingTask.AddParameterGroup().AddParameter("n", "o", "r", "pt");
            element.Parent.Parent.RemoveAllChildren();
            element.Output = "o2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_17()
        {
            var usingTask = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            usingTask.TaskFactory = "f";
            var element = usingTask.AddParameterGroup().AddParameter("n", "o", "r", "pt");
            element.Parent.Parent.RemoveAllChildren();
            element.Required = "r2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_18()
        {
            var usingTask = ProjectRootElement.Create().AddUsingTask("n", null, "an");
            usingTask.TaskFactory = "f";
            var element = usingTask.AddParameterGroup().AddParameter("n", "o", "r", "pt");
            element.Parent.Parent.RemoveAllChildren();
            element.ParameterType = "pt2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_23()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt");
            element.Parent.Parent.RemoveAllChildren();
            element.SetParameter("p", "v");
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_24()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt");
            element.Parent.Parent.RemoveAllChildren();
            element.ContinueOnError = "coe";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_25()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt").AddOutputItem("tp", "i");
            element.Parent.Parent.RemoveAllChildren();
            element.TaskParameter = "tp2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_26()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt").AddOutputItem("tp", "i");
            element.Parent.Parent.RemoveAllChildren();
            element.ItemType = "tp2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_27()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt").AddOutputProperty("tp", "p");
            element.Parent.Parent.RemoveAllChildren();
            element.TaskParameter = "tp2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_28()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddTask("tt").AddOutputProperty("tp", "p");
            element.Parent.Parent.RemoveAllChildren();
            element.PropertyName = "tp2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_29()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddItemGroup().AddItem("i", "i1");
            element.Parent.Parent.RemoveAllChildren();
            element.ItemType = "j";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_30()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddItemGroup().AddItem("i", "i1");
            element.Parent.Parent.RemoveAllChildren();
            element.Include = "i2";
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_31()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddItemGroup().AddItem("i", "i1").AddMetadata("m", "m1");
            element.Parent.Parent.RemoveAllChildren();
            element.Value = "m2";
        }

        [Fact]
        // Exposed https://github.com/Microsoft/msbuild/issues/1210
        public void AddMetadataAsAttributeAndAsElement()
        {
            var project = ProjectRootElement.Create();
            var itemGroup = project.AddItemGroup();

            var item = itemGroup.AddItem("i1", "i");
            item.AddMetadata("A", "value_a", expressAsAttribute: true);
            item.AddMetadata("B", "value_b", expressAsAttribute: true);

            item = itemGroup.AddItem("i2", "i");
            item.AddMetadata("A", "value_a", expressAsAttribute: false);
            item.AddMetadata("B", "value_b", expressAsAttribute: true);

            item = itemGroup.AddItem("i3", "i");
            item.AddMetadata("A", "value_a", expressAsAttribute: true);
            item.AddMetadata("B", "value_b", expressAsAttribute: false);

            string expected = ObjectModelHelpers.CleanupFileContents(
@"<Project ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
  <ItemGroup>
    <i1 Include=""i"" A=""value_a"" B=""value_b"" />
    <i2 Include=""i"" B=""value_b"">
      <A>value_a</A>
    </i2>
    <i3 Include=""i"" A=""value_a"">
      <B>value_b</B>
    </i3>
  </ItemGroup>
</Project>");

            Helpers.VerifyAssertProjectContent(expected, project);
        }

        /// <summary>
        /// Legally modify a child whose parent is not parented (should not throw)
        /// </summary>
        [Fact]
        public void ModifyUnparentedParentChild_32()
        {
            var element = ProjectRootElement.Create().AddTarget("t").AddPropertyGroup().AddProperty("p", "v1");
            element.Parent.Parent.RemoveAllChildren();
            element.Value = "v2";
        }

        private static string ComposeExpectedProjectString(string expectedItem)
        {
            var expected =
@"<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <ItemGroup>
{0}
  </ItemGroup>
</Project>";
            expectedItem = AdjustSpacesForItem(expectedItem);

            expected = ObjectModelHelpers.CleanupFileContents(string.Format(expected, expectedItem));

            return expected;
        }

        /// <summary>
        /// Add a property to an empty project
        /// should add to new property group
        /// </summary>
        [Fact]
        public void AddProperty_WithSdk_KeepsSdkAndImplicitImports()
        {
            using (var env = TestEnvironment.Create())
            {
                var testSdkRoot = env.CreateFolder().Path;
                var testSdkDirectory = Path.Combine(testSdkRoot, "MSBuildUnitTestSdk", "Sdk");
                Directory.CreateDirectory(testSdkDirectory);

                string sdkPropsPath = Path.Combine(testSdkDirectory, "Sdk.props");
                string sdkTargetsPath = Path.Combine(testSdkDirectory, "Sdk.targets");

                File.WriteAllText(sdkPropsPath, "<Project />");
                File.WriteAllText(sdkTargetsPath, "<Project />");

                var testProject = env.CreateTestProjectWithFiles(@"
                    <Project Sdk='MSBuildUnitTestSdk'>
                    </Project>");
                env.SetEnvironmentVariable("MSBuildSDKsPath", testSdkRoot);

                string content = @"
                    <Project Sdk='MSBuildUnitTestSdk' >
                    </Project>";

                File.WriteAllText(testProject.ProjectFile, content);

                var p = new Project(testProject.ProjectFile);

                p.Xml.AddProperty("propName", "propValue");

                var updated = Path.Combine(testProject.TestRoot, "updated.proj");

                p.Save(updated);

                var updatedContents = File.ReadAllText(updated);

                Assert.DoesNotContain("<Import", updatedContents);
            }
        }

        private static string AdjustSpacesForItem(string expectedItem)
        {
            Assert.False(string.IsNullOrEmpty(expectedItem));

            var itemSpace = "    ";
            var metadataSpace = itemSpace + "  ";

            var splits = expectedItem.Split(MSBuildConstants.NewlineChar);
            splits = splits.Select(s => s.Trim()).ToArray();

            Assert.True(splits.Length >= 1);

            var sb = new StringBuilder();

            if (splits.Length == 1)
            {
                splits[0] = itemSpace + expectedItem;
            }
            else
            {
                sb.AppendLine(itemSpace + splits[0]);

                for (var i = 1; i < splits.Length - 1; i++)
                {
                    sb.AppendLine(metadataSpace + splits[i]);
                }

                sb.Append(itemSpace + splits[splits.Length -1]);
            }

            expectedItem = sb.ToString();
            return expectedItem;
        }
    }
}
