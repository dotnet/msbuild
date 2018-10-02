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
    /// Test Fixture Class for the v9 Object Model Public Interface Compatibility Tests for the UsingTask Class.
    /// </summary>
    [TestFixture]
    public class UsingTask_Tests
    {
        #region AssemblyName
        /// <summary>
        /// AssemblyName test, set AssemblyName to a simple value in ctor then get.
        /// </summary>
        [Test]
        public void GetAssemblyName()
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyName("TaskName", "AssemblyName");
            object o = p.EvaluatedItems;
            Assertion.AssertEquals("AssemblyName", CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).AssemblyName);
        }

        /// <summary>
        /// AssemblyName test, set AssemblyName to special escaped characters in ctor then get.
        /// </summary>
        [Test]
        public void GetAssemblyNameSpecialCharsEscaped()
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyName("TaskName", @"%25%2a%3f%40%24%28%29%3b\");
            object o = p.EvaluatedItems;
            Assertion.AssertEquals(@"%25%2a%3f%40%24%28%29%3b\", CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).AssemblyName);
        }

        /// <summary>
        /// AssemblyName test, set AssemblyName to special non-escaped characters in ctor then get.
        /// </summary>
        [Test]
        public void GetAssemblyNameSpecialChars()
        {
            Project p = new Project();
            p.AddNewUsingTaskFromAssemblyName("TaskName", @"%*?@$();\");
            object o = p.EvaluatedItems;
            Assertion.AssertEquals(@"%*?@$();\", CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).AssemblyName);
        }

        /// <summary>
        /// AssemblyName test,, set AssemblyName to a scalar that has no property defined in the project, then get.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void GetAssemblyNameScalarThatIsNotSet()
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyName("TaskName", @"$(assemblyName)");
            object o = p.EvaluatedItems;
            Assertion.AssertEquals(@"$(assemblyName)", CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).AssemblyName);
        }

        /// <summary>
        /// AssemblyName test, set AssemblyName to scalar that is defined in the project, then get. Does not evaluate
        /// </summary>
        [Test]
        public void GetAssemblyNameScalarEvaluation()
        {
            string assemblyName = "$(assemblyName)";
            Project p = new Project();
            p.SetProperty("assemblyName", "aName");
            object o = p.EvaluatedItems;
            p.AddNewUsingTaskFromAssemblyName("TaskName", assemblyName);
            o = p.EvaluatedItems;
            Assertion.AssertEquals(assemblyName, CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).AssemblyName);
        }

        #endregion

        #region AssemblyFile
        /// <summary>
        /// AssemblyFile test, simple set in ctor then get.
        /// </summary>
        [Test]
        public void GetAssemblyFileName()
        {
            string assemblyFileName = "FileName.dll";
            Assertion.AssertNotNull(SetandGetAssemblyFileName(assemblyFileName));
        }

        /// <summary>
        /// AssemblyFile test, set special escaped characters in ctor then get.
        /// </summary>
        [Test]
        public void GetAssemblyFileNameSpecialCharsEscaped()
        {
            string assemblyFileName = @"%25%2a%3f%40%24%28%29%3b\";
            Assertion.AssertNotNull(SetandGetAssemblyFileName(assemblyFileName));
        }
        
        /// <summary>
        /// AssemblyFile test, set special non-escaped characters in ctor then get.
        /// </summary>
        [Test]
        public void GetAssemblyFileNameSpecialChars()
        {
            string assemblyFileName = @"%*?@$();\";
            Assertion.AssertNotNull(SetandGetAssemblyFileName(assemblyFileName));
        }

        /// <summary>
        /// AssemblyFile test, set to a scalar that has no property defined in the project, then get.
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void GetAssemblyFileNameScalarThatIsNotSet()
        {
            string assemblyFileName = "$(fileName)";
            Assertion.AssertNotNull(SetandGetAssemblyFileName(assemblyFileName));
        }

        /// <summary>
        /// AssemblyFile test, set to scalar that is defined in the project, then get. Does Not Evaluate
        /// </summary>
        [Test]
        public void GetAssemblyFileNameScalarEvaluation()
        {
                string assemblyFileName = "$(fileName)";
                Project p = new Project(new Engine());
                p.SetProperty("fileName", "aFileName");
                object o = p.EvaluatedItems;
                p.AddNewUsingTaskFromAssemblyFile("TaskName", assemblyFileName);
                o = p.EvaluatedItems;
                Assertion.AssertEquals(assemblyFileName, CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).AssemblyFile);
        }

        /// <summary>
        /// AssemblyFile Test, get assembly file name, where the path is is over windows max path
        /// </summary>
        [Test]
        public void GetAssemblyFileNamePathTooLong()
        {
            string assemblyFileName = Path.Combine(CompatibilityTestHelpers.GenerateLongPath(255), "assemblyFileName.dll");
            Assertion.AssertEquals(assemblyFileName, SetandGetAssemblyFileName(assemblyFileName));
        }

        /// <summary>
        /// Condition Test, get the usingtask conidtion when set in the xml
        /// </summary>
        [Test]
        public void GetUsingTaskAssemblyFile_SetInXml()
        {
            Project p = new Project(new Engine());
            p.LoadXml(TestData.ContentUsingTaskFile);
            object o = p.EvaluatedItems;
            Assertion.AssertEquals("AssemblyName.dll", CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).AssemblyFile);
        }

        #endregion 

        #region TaskName 

        /// <summary>
        /// TaskName Test, simple get
        /// </summary>
        [Test]
        public void GetTaskName_SetInXml()
        {
            Project p = new Project();
            p.LoadXml(TestData.ContentUsingTaskFile);
            object o = p.EvaluatedItems;
            Assertion.AssertNotNull(CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks));
        }

        /// <summary>
        /// TaskName Test, scalars are not evaluated
        /// </summary>
        [Test]
        public void GetTaskNameScalar()
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyName("$(name)", "assemblyName");
            p.SetProperty("name", "TaskName");
            object o = p.EvaluatedItems;
            Assertion.AssertNotNull(CompatibilityTestHelpers.FindUsingTaskByName("$(name)", p.UsingTasks));
        }

        /// <summary>
        /// TaskName Test, scalars are not evaluated
        /// </summary>
        [Test]
        public void GetTaskNameSpeicalChars()
        {
            Project p = new Project();
            p.AddNewUsingTaskFromAssemblyName(@"%*?@$();\", "assemblyName");
            object o = p.EvaluatedItems;
            Assertion.AssertNotNull(CompatibilityTestHelpers.FindUsingTaskByName(@"%*?@$();\", p.UsingTasks));
        }

        /// <summary>
        /// TaskName Test, scalars are not evaluated
        /// </summary>
        [Test]
        public void GetTaskNameSpeicalCharsEscaped()
        {
            Project p = new Project(new Engine());
            p.AddNewUsingTaskFromAssemblyName(@"%25%2a%3f%40%24%28%29%3b\", "assemblyName");
            object o = p.EvaluatedItems;
            Assertion.AssertNotNull(CompatibilityTestHelpers.FindUsingTaskByName(@"%25%2a%3f%40%24%28%29%3b\", p.UsingTasks));
        }

        #endregion

        #region IsImported

        /// <summary>
        /// IsImported Test, assert true for an improted file. 
        /// </summary>
        [Test]
        public void IsImported_true()
        {
            string importPath = String.Empty;
            try
            {
                importPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("import.proj", TestData.ContentUsingTaskName);
                Project p = new Project(new Engine());
                p.AddNewImport(importPath, "true");
                Object o = p.EvaluatedProperties;
                Import import = CompatibilityTestHelpers.FindFirstMatchingImportByPath(p.Imports, importPath);
                import.Condition = null;
                Assertion.AssertEquals(true, CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).IsImported);
            }
            finally
            {
                CompatibilityTestHelpers.RemoveFile(importPath);
            }
        }

        /// <summary>
        /// IsImported Test, assert true for an imported file. 
        /// </summary>
        [Test]
        public void IsImported_false()
        {
            Project p = new Project(new Engine());
            p.LoadXml(TestData.ContentUsingTaskFile);
            object o = p.EvaluatedProperties;
            Assertion.AssertEquals(false, CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).IsImported);
        }   

        #endregion

        #region Condition

        /// <summary>
        /// Condition Test, get condition when set in xml
        /// </summary>
        [Test]
        public void GetConditionSimple()
        {
            Project p = new Project();
            p.LoadXml(TestData.ContentUsingTaskName);
            object o = p.EvaluatedProperties;
            Assertion.AssertEquals("true", CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).Condition);
        }

        /// <summary>
        /// Condition Test, get when conditionis an expression
        /// </summary>
        [Test]
        public void GetConditionExpression()
        {
            Project p = new Project();
            p.LoadXml(TestData.ContentUsingTaskFile);
            Assertion.AssertEquals("$(value)==true", CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).Condition);
        }

        /// <summary>
        /// Condition Test, get condition in OM when set in xml
        /// </summary>
        [Test]
        public void GetUsingTaskCondition_SetInXml()
        {
            Project p = new Project();
            p.LoadXml(TestData.ContentUsingTaskName);
            Assertion.AssertEquals("true", CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks).Condition);
        }

        #endregion

        /// <summary>
        /// Set an assembly file name, then retrieve it. 
        /// </summary>
        private string SetandGetAssemblyFileName(string assemblyFileName)
        {
            Project p = new Project();
            p.AddNewUsingTaskFromAssemblyFile("TaskName", assemblyFileName);
            object o = p.EvaluatedItems;
            UsingTask usingTask = CompatibilityTestHelpers.FindUsingTaskByName("TaskName", p.UsingTasks);
            return usingTask.AssemblyFile;
        }   
    }
}
