// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for TaskItem internal members</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.UnitTests.BackEnd;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using System.Xml;
using Microsoft.Build.Framework;
using System.IO;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectPropertyInstance internal members
    /// </summary>
    [TestClass]
    public class TaskItem_Tests
    {
        /// <summary>
        /// Test serialization
        /// </summary>
        [TestMethod]
        public void Serialization()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            item.SetMetadata("a", "b");

            TranslationHelpers.GetWriteTranslator().Translate(ref item, TaskItem.FactoryForDeserialization);
            TaskItem deserializedItem = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedItem, TaskItem.FactoryForDeserialization);

            Assert.AreEqual(item.ItemSpec, deserializedItem.ItemSpec);
            Assert.AreEqual(item.MetadataCount, deserializedItem.MetadataCount);
            Assert.AreEqual(item.GetMetadata("a"), deserializedItem.GetMetadata("a"));
            Assert.AreEqual(item.GetMetadata(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath), deserializedItem.GetMetadata(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath));
        }

        /// <summary>
        /// Ensure an item is equivalent to itself.
        /// </summary>
        [TestMethod]
        public void TestEquivalenceIdentity()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");

            Assert.IsTrue(left.Equals(left));
        }

        /// <summary>
        /// Ensure two items with the same item spec and no metadata are equivalent
        /// </summary>
        [TestMethod]
        public void TestEquivalence()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            TaskItem right = new TaskItem("foo", "bar.proj");

            Assert.IsTrue(left == right);
            Assert.IsFalse(left != right);
        }

        /// <summary>
        /// Ensure two items with the same custom metadata are equivalent
        /// </summary>
        [TestMethod]
        public void TestEquivalenceWithCustomMetadata()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("a", "b");

            Assert.IsTrue(left == right);
            Assert.IsFalse(left != right);
        }

        /// <summary>
        /// Ensure two items with different custom metadata values are not equivalent
        /// </summary>
        [TestMethod]
        public void TestInequivalenceWithDifferentCustomMetadataValues()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("a", "c");

            Assert.IsFalse(left == right);
            Assert.IsTrue(left != right);
        }

        /// <summary>
        /// Ensure two items with different custom metadata keys are not equivalent
        /// </summary>
        [TestMethod]
        public void TestInequivalenceWithDifferentCustomMetadataKeys()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("b", "b");

            Assert.IsFalse(left == right);
            Assert.IsTrue(left != right);
        }

        /// <summary>
        /// Ensure two items with different numbers of custom metadata are not equivalent
        /// </summary>
        [TestMethod]
        public void TestInequivalenceWithDifferentCustomMetadataCount()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");

            Assert.IsFalse(left == right);
            Assert.IsTrue(left != right);
        }

        /// <summary>
        /// Ensure two items with different numbers of custom metadata are not equivalent
        /// </summary>
        [TestMethod]
        public void TestInequivalenceWithDifferentCustomMetadataCount2()
        {
            TaskItem left = new TaskItem("foo", "bar.proj");
            left.SetMetadata("a", "b");
            TaskItem right = new TaskItem("foo", "bar.proj");
            right.SetMetadata("a", "b");
            right.SetMetadata("c", "d");

            Assert.IsFalse(left == right);
            Assert.IsTrue(left != right);
        }

        /// <summary>
        /// Ensure when cloning an Item that the clone is equivilant to the parent item and that they are not the same object.
        /// </summary>
        [TestMethod]
        public void TestDeepClone()
        {
            TaskItem parent = new TaskItem("foo", "bar.proj");
            parent.SetMetadata("a", "b");
            parent.SetMetadata("c", "d");

            TaskItem clone = parent.DeepClone();
            Assert.IsTrue(parent.Equals(clone), "The parent and the clone should be equal");
            Assert.IsFalse(object.ReferenceEquals(parent, clone), "The parent and the child should not be the same object");
        }

        /// <summary>
        /// Flushing an item through a task should not mess up special characters on the metadata. 
        /// </summary>
        [TestMethod]
        public void Escaping1()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
           <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <ItemGroup>
                    <i Include='i1'>
                      <m>i1m1;i1m2</m>
                    </i>
                    <j Include='j1'>
                      <m>j1m1;j1m2</m>
                    </j>
                  </ItemGroup>

                  <Target Name='Build'>
                    <CallTarget Targets='%(i.m)'/>
                    <CreateItem Include='@(j)'>
                      <Output TaskParameter='Include' ItemName='j2'/>
                    </CreateItem>
                    <CallTarget Targets='%(j2.m)'/>
                  </Target>

                  <Target Name='i1m1'>
                    <Warning Text='[i1m1]'/>
                  </Target>
                  <Target Name='i1m2'>
                    <Warning Text='[i1m2]'/>
                  </Target>
                  <Target Name='j1m1'>
                    <Warning Text='[j1m1]'/>
                  </Target>
                  <Target Name='j1m2'>
                    <Warning Text='[j1m2]'/>
                  </Target>
                </Project>
                ");

            ProjectRootElement xml = ProjectRootElement.Create(XmlTextReader.Create(new StringReader(content)));

            Project project = new Project(xml);
            MockLogger logger = new MockLogger();
            project.Build("Build", new ILogger[] { logger });

            logger.AssertLogContains("[i1m1]");
            logger.AssertLogContains("[i1m2]");
            logger.AssertLogContains("[j1m1]");
            logger.AssertLogContains("[j1m2]");
        }

        /// <summary>
        /// Flushing an item through a task run in the task host also should not mess up special characters on the metadata. 
        /// </summary>
        [TestMethod]
        public void Escaping2()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
           <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <UsingTask TaskName='CreateItem' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' TaskFactory='TaskHostFactory' />
                  <ItemGroup>
                    <i Include='i1'>
                      <m>i1m1;i1m2</m>
                    </i>
                    <j Include='j1'>
                      <m>j1m1;j1m2</m>
                    </j>
                  </ItemGroup>

                  <Target Name='Build'>
                    <CallTarget Targets='%(i.m)'/>
                    <CreateItem Include='@(j)'>
                      <Output TaskParameter='Include' ItemName='j2'/>
                    </CreateItem>
                    <CallTarget Targets='%(j2.m)'/>
                  </Target>

                  <Target Name='i1m1'>
                    <Warning Text='[i1m1]'/>
                  </Target>
                  <Target Name='i1m2'>
                    <Warning Text='[i1m2]'/>
                  </Target>
                  <Target Name='j1m1'>
                    <Warning Text='[j1m1]'/>
                  </Target>
                  <Target Name='j1m2'>
                    <Warning Text='[j1m2]'/>
                  </Target>
                </Project>
                ");

            ProjectRootElement xml = ProjectRootElement.Create(XmlTextReader.Create(new StringReader(content)));

            Project project = new Project(xml);
            MockLogger logger = new MockLogger();
            project.Build("Build", new ILogger[] { logger });

            logger.AssertLogContains("[i1m1]");
            logger.AssertLogContains("[i1m2]");
            logger.AssertLogContains("[j1m1]");
            logger.AssertLogContains("[j1m2]");
        }

        /// <summary>
        /// Flushing an item through a task run in the task host also should not mess up the escaping of the itemspec either. 
        /// </summary>
        [TestMethod]
        public void Escaping3()
        {
            string content = ObjectModelHelpers.CleanupFileContents(@"
           <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                  <UsingTask TaskName='AssignCulture' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' TaskFactory='TaskHostFactory' />
                  <ItemGroup>
                    <i Include='i1%252ai2' />
                  </ItemGroup>

                  <Target Name='Build'>
                    <AssignCulture Files='@(i)'>
                      <Output TaskParameter='AssignedFiles' ItemName='i1'/>
                    </AssignCulture>
                    <Message Text='@(i1)'/>
                  </Target>
                </Project>
                ");

            ProjectRootElement xml = ProjectRootElement.Create(XmlTextReader.Create(new StringReader(content)));

            Project project = new Project(xml);
            MockLogger logger = new MockLogger();
            project.Build("Build", new ILogger[] { logger });

            logger.AssertLogContains("i1%2ai2");
        }
    }
}
