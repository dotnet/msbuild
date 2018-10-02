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
    /// Fixture Class for the v9 OM Public Interface Compatibility Tests. Import Class.
    /// Also see Toolset tests in the Project test class.
    /// </summary>
    [TestFixture]
    public sealed class Import_Tests 
    {
        /// <summary>
        /// Condition Test, Simple Condition, assert only accessible after evaluation.
        /// </summary>
        [Test]
        public void ConditionGet_Simple() 
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.AddNewImport(importPath, "true");
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                Assertion.AssertNull("true", import);
                object o = p.EvaluatedProperties;
                import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                Assertion.AssertEquals("true", import.Condition);
            }
            finally 
            {
                CompatibilityTestHelpers.RemoveFile(importPath);           
            }
        }

        /// <summary>
        /// Condition Test, Null condition.
        /// </summary>
        [Test]
        public void ConditionGet_Null()
        {
            string importPath = string.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.AddNewImport(importPath, null);
                object o = p.EvaluatedProperties;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                Assertion.AssertNull(import.Condition);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// Condition Test, Set Null condition, assert empty string back.
        /// </summary>
        [Test]
        public void ConditionSet_Null()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified); 
                Project p = new Project();
                p.AddNewImport(importPath, "true");
                object o = p.EvaluatedProperties;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                import.Condition = null;
                Assertion.AssertEquals(String.Empty, import.Condition);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// Condition Test, dirty wet set.
        /// </summary>
        [Test]
        public void ConditionSet_DirtyWhenSet()
        {
            string projectPath = String.Empty;
            string importPath = String.Empty;
            try
            {
                projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("project.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.AddNewImport(importPath, "true");
                object o = p.EvaluatedProperties;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                p.Save(projectPath);
                Assertion.AssertEquals(false, p.IsDirty);
                import.Condition = "condition";
                Assertion.AssertEquals(true, p.IsDirty);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
                CompatibilityTestHelpers.RemoveFile(projectPath);
            }
        }
       
        /// <summary>
        /// ProjectPath Test, get when set in the constructor. 
        /// </summary>
        [Test]
        public void ProjectPathGetWhenSetInCtor()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.AddNewImport(importPath, "true");
                object o = p.EvaluatedProperties;  
   
                // The verbosity of this assertion is to abstract the internal implentation of FindFirstMatchingImportByPath.
                Assertion.AssertEquals(importPath, CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath).ProjectPath); 
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// ProjectPath Test, get when set in ctor 
        /// </summary>
        [Test]
        public void EvaluatedProjectPathGetWhenSetInCtor()
        {
            string importPath = "importA.proj";
            string fullImportPath = ObjectModelHelpers.CreateFileInTempProjectDirectory(importPath, TestData.Content3SimpleTargetsDefaultSpecified);
            string projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("project.proj", TestData.ContentImportA);
            try
            {
                Project p = new Project();
                p.Load(projectPath);
 
                // The verbosity of this assertion is to abstract the internal implentation of FindFirstMatchingImportByPath.
                Assertion.AssertEquals(fullImportPath, CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath).EvaluatedProjectPath);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(projectPath);
                CompatibilityTestHelpers.RemoveFile(fullImportPath);
            }
        }

        /// <summary>
        /// ProjectPath Test, get when set in loaded xml. 
        /// </summary>
        [Test]
        public void ProjectPathGetWhenSetInXML()
        {
            string projectPath = String.Empty;
            string importPath = String.Empty;
            string fullImportPath = String.Empty;            
            try
            {
                projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("project.proj", TestData.ContentImportA);
                importPath = "importA.proj"; // as specified in xml
                fullImportPath = ObjectModelHelpers.CreateFileInTempProjectDirectory(importPath, TestData.ContentA);
                Project p = new Project();
                p.Load(projectPath);
                Assertion.AssertEquals(importPath, CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath).ProjectPath);           
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(projectPath);
                CompatibilityTestHelpers.RemoveFile(fullImportPath);
            }
        }

        /// <summary>
        /// ProjectPath Test, set overriding ctor value.
        /// </summary>
        [Test]
        public void ProjectPathSet()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.AddNewImport(importPath, "true");
                object o = p.EvaluatedProperties;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                import.ProjectPath = @"c:\anotherPath";
                Assertion.AssertEquals(@"c:\anotherPath", import.ProjectPath);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// ProjectPath Test, set overriding ctor value.
        /// </summary>
        [Test]
        public void ProjectPathSet_Escaped()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.AddNewImport(importPath, "true");
                object o = p.EvaluatedProperties;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                import.ProjectPath = @"%25%2a%3f%40%24%28%29%3b\";
                Assertion.AssertEquals(@"%25%2a%3f%40%24%28%29%3b\", import.ProjectPath);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// IsImported Test, false when project is an import
        /// </summary>
        [Test]
        public void IsImported_ProjectImport()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.AddNewImport(importPath, "true");
                object o = p.EvaluatedProperties;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                Assertion.AssertEquals(false, import.IsImported);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// IsImported Test, true when import is via an imported project. 
        /// </summary>
        [Test]
        public void IsImported_ProjectImportImport()
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
                Assertion.AssertEquals(true, import.IsImported);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(project1);
                CompatibilityTestHelpers.RemoveFile(importPathA);
                CompatibilityTestHelpers.RemoveFile(importPathBFull);
            }
        }

        /// <summary>
        /// ProjectPath Test, does not evaluate scalars
        /// </summary>
        [Test]
        public void ProjectPathSet_ScalarValue()
        {
            string importPath = String.Empty;
            string importPath2 = String.Empty;           
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                importPath2 = ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project();
                p.AddNewImport(importPath, "true");
                p.SetProperty("path", importPath2);
                object o = p.EvaluatedProperties;
                BuildProperty path = CompatibilityTestHelpers.FindBuildProperty(p, "path");
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                import.ProjectPath = "$(path)";
                Assertion.AssertEquals("$(path)", import.ProjectPath);
                o = p.EvaluatedProperties;
                Assertion.AssertEquals(false, object.Equals(importPath2, import.EvaluatedProjectPath)); // V9 OM does not evaluate imports
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
                CompatibilityTestHelpers.RemoveFile(importPath2);
            }
        }
    }
}
