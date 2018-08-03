// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Principal;
using System.Security.AccessControl;
using NUnit.Framework;

using Microsoft.Build;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Indirection delegate type for AddNewImport Method invocation
    /// </summary>
    public delegate void AddNewImportDelegate(Project p, string path, string condition); 

    /// <summary>
    /// indirection for  tests of Project.AddNewImport and ImportCollection.AddNewImport
    /// </summary>
    public abstract class AddNewImportTests
    {
        #region Indirection Delegates
     
        /// <summary>
        /// Indirection delegate for AddNewImport Method invocation
        /// </summary>
        protected AddNewImportDelegate InvokeAddNewImportMethod
        {
            get;
            set;
        } 

        #endregion

        /// <summary>
        ///  AddNewImport Test, Empty 
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void AddNewImportFileName_Empty()
        {
            Project p = new Project(new Engine());
            InvokeAddNewImportMethod(p, String.Empty, null);
        }

        /// <summary>
        ///  AddNewImport Test, Null 
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void AddNewImportFileName_Null()
        {
            Project p = new Project(new Engine());
            InvokeAddNewImportMethod(p, null, null);
        }

        /// <summary>
        ///  AddNewImport Test, Where imported file does not exist 
        /// </summary>
        [Test]
        public void AddNewImportFile_DoesNotExist()
        {
            Project p = new Project(new Engine());
            InvokeAddNewImportMethod(p, @"c:\doesnotexist.proj", null);
        }

        /// <summary>
        ///  AddNewImport Test, Import a File, where file is not permitted to be read.
        /// </summary>
        [Test]
        public void AddNewImportFile_NoReadPermissions()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentSimpleTools35InitialTargets);
                Project p = new Project(new Engine());
                CompatibilityTestHelpers.SetFileAccessPermissions(importPath, FileSystemRights.Read, AccessControlType.Deny);
                InvokeAddNewImportMethod(p, importPath, null);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        ///  AddNewImport Test, Import a file with an empty condition. 
        /// </summary>
        [Test]
        public void AddNewImportFile_EmptyCondition()
        {
            string importPath = String.Empty;
            try
            {
                Project p = new Project(new Engine());
                importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentSimpleTools35InitialTargets);
                InvokeAddNewImportMethod(p, importPath, String.Empty);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        ///  AddNewImport Test, import file with condiction that is true.
        /// </summary>
        [Test]
        public void AddNewImportFileCondition_True()
        {
            string importPath = String.Empty;
            try
            {
                Project p = new Project(new Engine());
                importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentSimpleTools35InitialTargets);
                InvokeAddNewImportMethod(p, importPath, "true");
                Assertion.AssertEquals(0, p.Imports.Count);
                object o = p.EvaluatedItems;  // force evaluation of imported projects.
                Assertion.AssertEquals(1, p.Imports.Count);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        ///  AddNewImport Test, import file with condiction that evaluates to true.
        /// </summary>
        [Test]
        public void AddNewImportFileCondition_EqualIsTrue()
        {
            string importPath = String.Empty;
            try
            {
                Project p = new Project(new Engine());
                importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentSimpleTools35InitialTargets);
                InvokeAddNewImportMethod(p, importPath, "1 == 1");
                Assertion.AssertEquals(0, p.Imports.Count);
                object o = p.EvaluatedItems;  // force evaluation of imported projects.
                Assertion.AssertEquals(1, p.Imports.Count);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        ///  AddNewImport Test, import file with Condition that is false
        /// </summary>
        [Test]
        public void AddNewImportFileCondition_False()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentSimpleTools35InitialTargets);
                Project p = new Project(new Engine());
                InvokeAddNewImportMethod(p, importPath, "false");
                Assertion.AssertEquals(0, p.Imports.Count);
                object o = p.EvaluatedItems;  // force evaluation of imported projects.
                Assertion.AssertEquals(0, p.Imports.Count);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        ///  AddNewImport Test, check project flagged as dirty after import
        /// </summary>
        [Test]
        public void AddNewImportsIsDirtyAfterImport()
        {
            Project p = new Project(new Engine());
            string importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentSimpleTools35InitialTargets);
            Assertion.AssertEquals(false, p.IsDirty);
            InvokeAddNewImportMethod(p, importPath, null);
            Assertion.AssertEquals(true, p.IsDirty);
        }

        /// <summary>
        ///  AddNewImport Test, Check precedence and import of InitialTargets
        /// </summary>
        [Test]
        public void AddNewImportAttributeprecedence_InitialTargets()
        {
            Project p = new Project(new Engine());
            p.InitialTargets = "localTargetFirst";
            string importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentSimpleTools35InitialTargets); // InitialTarget
            InvokeAddNewImportMethod(p, importPath, null);
            Assertion.AssertEquals("localTargetFirst", p.InitialTargets); // Assert non automatic evaluation.
            object o = p.EvaluatedItems;  // force evaluation of imported projects.
            Assertion.AssertEquals("localTargetFirst; InitialTarget", p.InitialTargets); // Assert the concat
        }

        /// <summary>
        ///  AddNewImport Test, Check precedence and import of DefaultTargets
        /// </summary>
        [Test]
        public void AddNewImportAttributeprecedence_DefaultTarget()
        {
            string importPath = String.Empty;
            try
            {
                Project p = new Project(new Engine());
                p.DefaultTargets = "localTargetDefault";
                importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified); // TestTargetDefault
                InvokeAddNewImportMethod(p, importPath, null);
                Assertion.AssertEquals("localTargetDefault", p.DefaultTargets); // Assert non automatic evaluation.
                object o = p.EvaluatedItems;  // force evaluation of imported projects.
                Assertion.AssertEquals("localTargetDefault", p.DefaultTargets);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        ///  AddNewImport Test, Import p1 into p2
        /// </summary>
        [Test]
        public void AddNewImportStandard()
        {
            string projectPath = String.Empty;
            string projectPathImport = String.Empty;
            try
            {
                Project p = new Project(new Engine());
                projectPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                projectPathImport = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                p.Load(projectPath);
                InvokeAddNewImportMethod(p, projectPathImport, "true");
                Assertion.AssertEquals(0, p.Imports.Count);
                object o = p.EvaluatedItems;  // force evaluation of imported projects.
                Assertion.AssertEquals(1, CompatibilityTestHelpers.CountNodesWithName(p.Xml, "Import"));
                Assertion.AssertEquals(1, p.Imports.Count);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(projectPath);
                CompatibilityTestHelpers.RemoveFile(projectPathImport);
            }
        }

        /// <summary>
        ///  AddNewImport Test, Import p1 into p twice
        /// </summary>
        [Test]
        public void AddNewImportStandardTwice()
        {
            string projectPath = String.Empty;
            string projectPathImport = String.Empty;
            try
            {
                Project p = new Project(new Engine());
                projectPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                projectPathImport = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                p.Load(projectPath);
                InvokeAddNewImportMethod(p, projectPathImport, "true");
                InvokeAddNewImportMethod(p, projectPathImport, "true");
                Assertion.AssertEquals(0, p.Imports.Count);
                object o = p.EvaluatedItems;  // force evaluation of imported projects.
                Assertion.AssertEquals(2, CompatibilityTestHelpers.CountNodesWithName(p.Xml, "Import"));  // 2 in xml
                Assertion.AssertEquals(1, p.Imports.Count); // 1 in OM. 
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(projectPath);
                CompatibilityTestHelpers.RemoveFile(projectPathImport);
            }
        }

        /// <summary>
        ///  AddNewImport Test, Import p1 and p2 into p
        /// </summary>
        [Test]
        public void AddTwoNewImportStandard()
        {
            string projectPath = String.Empty;
            string projectPathImport1 = String.Empty;
            string projectPathImport2 = String.Empty;

            try
            {
                Project p = new Project(new Engine());
                projectPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                projectPathImport1 = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                projectPathImport2 = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                p.Load(projectPath);
                InvokeAddNewImportMethod(p, projectPathImport1, "true");
                InvokeAddNewImportMethod(p, projectPathImport2, "true");
                Assertion.AssertEquals(0, p.Imports.Count);
                object o = p.EvaluatedItems;  // force evaluation of imported projects.
                Assertion.AssertEquals(2, CompatibilityTestHelpers.CountNodesWithName(p.Xml, "Import")); // 2 in the XML
                Assertion.AssertEquals(2, p.Imports.Count); // 2 in OM
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(projectPath);
                CompatibilityTestHelpers.RemoveFile(projectPathImport1);
                CompatibilityTestHelpers.RemoveFile(projectPathImport2);
            }
        }

        /// <summary>
        ///  AddNewImport Test, Import a project into itself
        /// </summary>
        [Test]
        public void AddNewImportToBecomeSelfReferential()
        {
            string projectPath = String.Empty;
            try
            {
                Project p = new Project(new Engine());
                projectPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                p.Load(projectPath);
                InvokeAddNewImportMethod(p, projectPath, "true");
                Assertion.AssertEquals(0, p.Imports.Count);
                object o = p.EvaluatedItems;  // force evaluation of imported projects.
                Assertion.AssertEquals(0, p.Imports.Count); // This is bonkers, should be 1 because the XML DOES contain the import node.
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(projectPath);
            }
        }

        /// <summary>
        ///  AddNewImport Test, Import a project into itself twice
        /// </summary>
        [Test]
        public void AddNewImportToBecomeSelfReferentialTwice()
        {
            string projectPath = String.Empty;
            try
            {
                Project p = new Project(new Engine());
                projectPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                InvokeAddNewImportMethod(p, projectPath, null);
                InvokeAddNewImportMethod(p, projectPath, null);
                object o = p.EvaluatedItems;  // force evaluation of imported projects.
                Assertion.AssertEquals(1, p.Imports.Count);
                Assertion.AssertEquals(2, CompatibilityTestHelpers.CountNodesWithName(p.Xml, "Import")); // 2 in the XML
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(projectPath);
            }
        }
    }
}
