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

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Fixture Class for the v9 OM Public Interface Compatibility Tests. UsingTaskCollection Class.
    /// Also see Toolset tests in the Project test class.
    /// </summary>
    [TestFixture]
    public class UsingTaskCollection_Tests
    {
        /// <summary>
        /// Imports Cache issue causes xml not to be loaded 
        /// This is a test case to reproduce some quirkiness  found when running tests out of order.
        /// </summary>
        [Test]
        public void ImportsUsingTask()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Save(importPath); // required to reproduce
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.ContentUsingTaskFile);
                Project p2 = new Project(); // new Engine() here fixes testcase
                p2.AddNewImport(importPath, "true");
                object o = p2.EvaluatedProperties; // evaluate the import
                Assertion.AssertNull(CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p2.UsingTasks)); // fails to find task
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// Count Test. Increment Count on Import Add in OM
        /// </summary>
        [Test]
        public void Count_IncrementOnAddFile()
        {
            Project p = new Project(new Engine());
            Assertion.AssertEquals(0, p.UsingTasks.Count);
            p.AddNewUsingTaskFromAssemblyFile("TaskName", "AssemblyFile.dll");
            Assertion.AssertEquals(0, p.UsingTasks.Count);
            object o = p.EvaluatedProperties;
            Assertion.AssertEquals(1, p.UsingTasks.Count);
        }

        /// <summary>
        /// Count Test. Increment Count on Import Add in OM
        /// </summary>
        [Test]
        public void Count_IncrementOnAddName()
        {
            Project p = new Project(new Engine());
            Assertion.AssertEquals(0, p.UsingTasks.Count);
            p.AddNewUsingTaskFromAssemblyName("TaskName", "AssemblyName");
            Assertion.AssertEquals(0, p.UsingTasks.Count);
            object o = p.EvaluatedProperties;
            Assertion.AssertEquals(1, p.UsingTasks.Count);
        }

        /// <summary>
        /// Count Test. Increment Count on Import Add in XML
        /// </summary>
        [Test]
        public void Count_IncrementOnAddFileXml()
        {
            string projectPath = String.Empty;
            try
            {
                projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.ContentUsingTaskFile);
                Project p = new Project(new Engine());
                Assertion.AssertEquals(0, p.UsingTasks.Count);
                p.Load(projectPath);
                Assertion.AssertEquals(1, p.UsingTasks.Count);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(projectPath);
            }
        }

        /// <summary>
        /// Count Test. Decrement\Reset Count to 0 on reload project xml
        /// </summary>
        [Test]
        public void Count_DecrementOnRemove()
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyFile("TaskName", "AssemblyFile.dll");
            object o = p.EvaluatedProperties;
            Assertion.AssertEquals(1, p.UsingTasks.Count);
            p.LoadXml(TestData.ContentSimpleTools35);
            Assertion.AssertEquals(0, p.UsingTasks.Count);
            o = p.EvaluatedProperties;
            Assertion.AssertEquals(0, p.UsingTasks.Count);
        }

        /// <summary>
        /// IsSynchronized Test           
        /// </summary>
        [Test]
        public void IsSynchronized() 
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyFile("TaskName", "AssemblyFile.dll");
            Assertion.AssertEquals(false, p.UsingTasks.IsSynchronized);
        }

        /// <summary>
        /// SyncRoot Test, ensure that SyncRoot returns and we can take a lock on it.           
        /// </summary>
        [Test]
        public void SyncRoot()
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyFile("TaskName1", "AssemblyFile1.dll");
            p.AddNewUsingTaskFromAssemblyFile("TaskName2", "AssemblyFile2.dll");
            UsingTask[] usingTasks = new UsingTask[p.UsingTasks.Count];
            p.UsingTasks.CopyTo(usingTasks, 0);
            lock (p.UsingTasks.SyncRoot)
            {
                int i = 0;
                foreach (UsingTask usingTask in p.UsingTasks)
                {
                    Assertion.AssertEquals(usingTasks[i].AssemblyFile, usingTask.AssemblyFile);
                    i++;
                }
            }
        }

        /// <summary>
        /// SyncRoot Test, copy into a strongly typed array and assert content against the source collection.        
        /// </summary>
        [Test]
        public void CopyTo_ZeroIndex()
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyFile("TaskName1", "AssemblyFile1.dll");
            p.AddNewUsingTaskFromAssemblyFile("TaskName2", "AssemblyFile2.dll");
            UsingTask[] usingTasks = new UsingTask[p.UsingTasks.Count];
            p.UsingTasks.CopyTo(usingTasks, 0);
            int i = 0;
            foreach (UsingTask usingTask in p.UsingTasks)
            {
                Assertion.AssertEquals(usingTasks[i].AssemblyFile, usingTask.AssemblyFile);
                i++;
            }
        }

        /// <summary>
        /// SyncRoot Test, copy into a strongly typed array and assert content against the source collection.        
        /// </summary>
        [Test]
        public void CopyTo_OffsetIndex()
        {
            int offSet = 3;
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyFile("TaskName1", "AssemblyFile1.dll");
            p.AddNewUsingTaskFromAssemblyFile("TaskName2", "AssemblyFile2.dll");
            UsingTask[] taskArray = new UsingTask[p.UsingTasks.Count + offSet];
            p.UsingTasks.CopyTo(taskArray, offSet);
            int i = offSet - 1;
            Assertion.AssertNull(taskArray[offSet - 1]);
            foreach (UsingTask usingTask in p.UsingTasks)
            {
                Assertion.AssertEquals(taskArray[i].AssemblyFile, usingTask.AssemblyFile);
                i++;
            }
        }

        /// <summary>
        /// SyncRoot Test, copy into a strongly typed array with an offset where the array is too small      
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void CopyTo_OffsetIndexArrayTooSmall()
        {
            int offSet = 3;
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyFile("TaskName1", "AssemblyFile1.dll");
            p.AddNewUsingTaskFromAssemblyFile("TaskName2", "AssemblyFile2.dll");
            UsingTask[] usingTasks = new UsingTask[p.UsingTasks.Count];
            p.UsingTasks.CopyTo(usingTasks, offSet);
        }

        /// <summary>
        /// Copy to a weakly typed array, no offset. Itterate over collection 
        /// </summary>
        [Test]
        public void CopyTo_WeakAndGetEnumerator()
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyFile("TaskName1", "AssemblyFile1.dll");
            p.AddNewUsingTaskFromAssemblyFile("TaskName2", "AssemblyFile2.dll");
            Array taskArray = Array.CreateInstance(typeof(UsingTask), p.UsingTasks.Count);
            p.UsingTasks.CopyTo(taskArray, 0);
            Assertion.AssertEquals(p.UsingTasks.Count, taskArray.Length);
            int i = 0;
            foreach (UsingTask usingTask in p.UsingTasks)
            {
                Assertion.AssertEquals(((UsingTask)taskArray.GetValue(i)), usingTask.AssemblyFile);
                i++;
            }
        }
    }
}
