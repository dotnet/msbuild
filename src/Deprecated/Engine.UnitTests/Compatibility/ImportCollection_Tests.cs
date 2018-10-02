// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using NUnit.Framework;

using Microsoft.Build;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    ///   Fixture Class for the v9 OM Public Interface Compatibility Tests. Import Class.
    ///   Also see Toolset tests in the Project test class.
    /// </summary>
    [TestFixture]
    public class ImportCollection_Tests : AddNewImportTests
    {
        /// <summary>
        /// Set the indirection for AddNewImport Tests
        /// </summary>
        public ImportCollection_Tests()
        {
            InvokeAddNewImportMethod = new AddNewImportDelegate(AddNewImportOverload);
        } 

        /// <summary>
        /// Count Test. Increment Count on Import Add
        /// </summary>
        [Test]
        public void Count_IncrementOnAdd()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                Assertion.AssertEquals(0, p.Imports.Count);
                p.Imports.AddNewImport(importPath, "true");
                Assertion.AssertEquals(0, p.Imports.Count);
                object o = p.EvaluatedItems;
                Assertion.AssertEquals(1, p.Imports.Count);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// Count Test. Decrement\Reset Count to 0 on clear import list.
        /// </summary>
        [Test]
        public void Count_DecrementOnRemove()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Imports.AddNewImport(importPath, "true");
                object o = p.EvaluatedItems;
                Assertion.AssertEquals(1, p.Imports.Count);
                p.Imports.RemoveImport(CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath));
                Assertion.AssertEquals(0, p.Imports.Count);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// CopyTo Test, copy into array at index zero
        /// </summary>
        [Test]
        public void CopyToStrong_IndexZero()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Imports.AddNewImport(importPath, "true");
                object o = p.EvaluatedItems;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                Import[] importArray = new Import[p.Imports.Count];
                p.Imports.CopyTo(importArray, 0);
                Assertion.AssertEquals(p.Imports.Count, importArray.Length);
                Assertion.AssertEquals(0, Array.IndexOf(importArray, import));
                Assertion.AssertEquals(true, object.ReferenceEquals(importArray[Array.IndexOf(importArray, import)], import));
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// CopyTo Test, copy into array at an offset Index
        /// </summary>
        [Test]
        public void CopyToStrong_OffsetIndex()
        {
            string importPath = String.Empty;
            try
            {
                const int OffsetIndex = 2;
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Imports.AddNewImport(importPath, "true");
                object o = p.EvaluatedItems;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                Import[] importArray = new Import[p.Imports.Count + OffsetIndex];
                p.Imports.CopyTo(importArray, OffsetIndex);
                Assertion.AssertEquals(p.Imports.Count, importArray.Length - OffsetIndex);
                Assertion.AssertNull(importArray[OffsetIndex - 1]);
                Assertion.AssertEquals(true, 0 < Array.IndexOf(importArray, import));
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// CopyTo Test, copy into array that is initialized too small to contain all imports
        /// </summary>
        [Test]
        [ExpectedException(typeof(OverflowException))]
        public void CopyToStrong_ArrayTooSmallImportsNotEvaludated()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Imports.AddNewImport(importPath, "true");
                p.Imports.CopyTo(new Toolset[p.Imports.Count - 1], 0);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// CopyTo Test, copy into array that is initialized too small to contain all imports
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void CopyToStrong_ArrayTooSmall()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Imports.AddNewImport(importPath, "true");
                object o = p.EvaluatedItems;
                p.Imports.CopyTo(new Toolset[p.Imports.Count - 1], 0);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// CopyTo Test, weak array, cast and narrow the return to import type
        /// </summary>
        [Test]
        public void CopyToWeak_CastNarrowReturns()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Imports.AddNewImport(importPath, "true");
                object o = p.EvaluatedItems;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                Import[] importArray = new Import[p.Imports.Count];
                p.Imports.CopyTo(importArray, 0);
                Assertion.AssertEquals(p.Imports.Count, importArray.Length);
                Assertion.AssertEquals(0, Array.IndexOf(importArray, import));
                Assertion.AssertEquals(true, object.ReferenceEquals(importArray[Array.IndexOf(importArray, import)], import));
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// CopyTo Test, store the return in weakly typed array
        /// </summary>
        [Test]
        public void CopyToWeak_Simple()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project(new Engine());
                p.Imports.AddNewImport(importPath, "true");
                object o = p.EvaluatedItems;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                Array importArray = Array.CreateInstance(typeof(Import), p.Imports.Count);
                p.Imports.CopyTo(importArray, 0);
                Assertion.AssertEquals(p.Imports.Count, importArray.Length);
                Assertion.AssertEquals(0, Array.IndexOf(importArray, import));
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// RemoveImport Test, null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RemoveImport_Null()
        {
                Project p = new Project(new Engine());
                p.Imports.RemoveImport(null);
        }

        /// <summary>
        /// RemoveImport Test, remove a import that belongs to a differnet project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveImport_Empty()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                Project p2 = new Project();
                p.Imports.AddNewImport(importPath, "true");
                p2.Imports.AddNewImport(importPath, "true");
                object o = p.EvaluatedItems;
                o = p2.EvaluatedItems;
                Import import2 = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p2.Imports, importPath);
                p.Imports.RemoveImport(import2); // does not exist in this project
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// RemoveImport Test, remove a import and check dirty
        /// </summary>
        [Test]
        public void RemoveImport_SimpleDirtyAfterRemove()
        {
            string importPath = String.Empty;
            string projectPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("project.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project(); 
                p.Imports.AddNewImport(importPath, "true");
                object o = p.EvaluatedItems;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                p.Save(projectPath);
                Assertion.AssertEquals(false, p.IsDirty);
                p.Imports.RemoveImport(import);
                Assertion.AssertNull(CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath));
                Assertion.AssertEquals(true, p.IsDirty);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
                CompatibilityTestHelpers.RemoveFile(projectPath); 
            }
        }

        /// <summary>
        /// RemoveImport Test, try to remove an import that is not first order
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveImport_ImportOfImport()
        {
            string project1 = String.Empty;
            string importPathA = String.Empty;
            string importPathB = String.Empty;
            string importPathBFull = String.Empty;
            try
            {
                project1 = ObjectModelHelpers.CreateFileInTempProjectDirectory("project.proj", TestData.ContentImportA);
                importPathA = ObjectModelHelpers.CreateFileInTempProjectDirectory("importA.proj", TestData.ContentImportB);
                importPathB = "importB.proj"; // as specified in TestData.ContentImportB xml
                importPathBFull = ObjectModelHelpers.CreateFileInTempProjectDirectory(importPathB, TestData.ContentA);
                Project p = new Project();
                p.Load(project1);
                object o = p.EvaluatedProperties;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPathB);
                p.Imports.RemoveImport(import);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(project1);
                CompatibilityTestHelpers.RemoveFile(importPathA);
                CompatibilityTestHelpers.RemoveFile(importPathBFull);
            }
        }

        /// <summary>
        /// Enumeration Test, manual iteration over ImportCollection using GetEnumerator();
        /// </summary>
        [Test]
        public void GetEnumerator()
        {
            string importPath = String.Empty;
            string importPath2 = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                importPath2 = ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Imports.AddNewImport(importPath, "true");
                p.Imports.AddNewImport(importPath2, "true");
                object o = p.EvaluatedItems;
                Import[] importArray = new Import[p.Imports.Count];
                p.Imports.CopyTo(importArray, 0);
                System.Collections.IEnumerator importEnum = p.Imports.GetEnumerator();
                int enumerationCounter = 0;
                while (importEnum.MoveNext())
                {
                    Assertion.AssertEquals(true, object.ReferenceEquals(importArray[enumerationCounter], importEnum.Current));
                    Assertion.AssertEquals(importArray[enumerationCounter].ProjectPath, ((Import)importEnum.Current).ProjectPath);
                    enumerationCounter++;
                }
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
                CompatibilityTestHelpers.RemoveFile(importPath2);
            }
        }

        /// <summary>
        /// SyncRoot Test, Take a lock on SyncRoot then iterate over it. 
        /// </summary>
        [Test]
        public void SyncRoot()
        {
            string importPath = String.Empty;
            string importPath2 = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                importPath2 = ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Imports.AddNewImport(importPath, "true");
                p.Imports.AddNewImport(importPath2, "true");
                object o = p.EvaluatedItems;
                Import[] importArray = new Import[p.Imports.Count];
                p.Imports.CopyTo(importArray, 0);
                lock (p.Imports.SyncRoot) 
                {
                    int i = 0;
                    foreach (Import import in p.Imports) 
                    {
                        Assertion.AssertEquals(importArray[i].ProjectPath, import.ProjectPath);
                       i++;
                    }
                }
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
                CompatibilityTestHelpers.RemoveFile(importPath2);
            }
        }

         /// <summary>
        /// isSynchronized, is false : returned collection is not threadsafe.  
        /// </summary>
        [Test]
        public void IsSynchronized()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.Imports.AddNewImport(importPath, "true");
                object o = p.EvaluatedItems;
                Assertion.AssertEquals(false, p.Imports.IsSynchronized);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// Indirection for common tests to p.AddNewImport
        /// </summary>
        private void AddNewImportOverload(Project p, string path, string condition)
        {
            p.Imports.AddNewImport(path, condition);
        } 
    }
}
