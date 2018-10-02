// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Collections.Generic;
using System.Collections;
using System.Xml;
using System.Xml.XPath;
using System.Text;
using System.Threading;

using NUnit.Framework;

using Microsoft.Build;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Fixture Class for the v9 OM Public Interface Compatibility Tests. Project Class.
    /// </summary>
    public sealed class Project_Tests
    {
        /// <summary>
        /// Tests for the Project Class Constructor
        /// </summary>
        [TestFixture]
        public sealed class Constructor
        {
            /// <summary>
            /// Constructor Test, with Engine object.
            /// </summary>
            [Test]
            public void ConstructEngine()
            {
                Engine e = new Engine();
                Project p = new Project();
                Assertion.ReferenceEquals(e, p.ParentEngine);
            }

            /// <summary>
            /// Constructor Test, with Null Engine object.
            /// </summary>
            [Test]
            public void ConstructEngine_Null()
            {
                Engine e = null;
                Project p = new Project(e);
                Assertion.AssertEquals(Engine.GlobalEngine, p.ParentEngine);
            }

            /// <summary>
            /// Constructor Test, with Engine Object, ToolsVersion empty.
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void ConstructToolsVersion_Empty()
            {
                Project p = new Project(new Engine(), String.Empty);
            }

            /// <summary>
            /// Constructor Test, with Engine Object, ToolsVersion 2.0.
            /// </summary>
            [Test]
            public void ConstructToolsVersionKnown_2_0()
            {
                Project p = new Project(new Engine(), "2.0");
                Assertion.AssertEquals("2.0", p.ToolsVersion);
            }

            /// <summary>
            /// Constructor Test, with Engine Object, ToolsVersion 3.5.
            /// </summary>
            [Test]
            public void ConstructToolsVersionKnown_3_5()
            {
                if (FrameworkLocationHelper.PathToDotNetFrameworkV35 != null)
                {
                    Project p = new Project(new Engine(), "3.5");
                    Assertion.AssertEquals("3.5", p.ToolsVersion);
                }
                else
                {
                    Assert.Ignore(".NET Framework 3.5 is required for this test, but is not installed."); 
                }
            }

            /// <summary>
            /// Constructor Test, with Engine Object, ToolsVersion 4.0.
            /// </summary>
            [Test]
            public void ConstructToolsVersionKnown_4_0()
            {
                Project p = new Project(new Engine(), "4.0");
                Assertion.AssertEquals("4.0", p.ToolsVersion);
            }

            /// <summary>
            /// Constructor Test, Ensure IsValidate is false.
            /// </summary>
            [Test]
            public void ConstructDefault_IsValidate_False()
            {
                Project p = new Project(new Engine());
                Assertion.AssertEquals(false, p.IsValidated);
            }

            /// <summary>
            /// Constructor Test, ensure HasUnsavedChanges is false.
            /// </summary>
            [Test]
            public void ConstructDefault_IsDirty_False()
            {
                Project p = new Project(new Engine());
                Assertion.AssertEquals(false, p.IsDirty);
            }

            /// <summary>
            /// Constructor Test, Ensure BuildEnabled inherits from parent.
            /// </summary>
            [Test]
            public void ConstructDefault_BuildEnabled_InheritParent()
            {
                Engine e = new Engine();
                e.BuildEnabled = true;
                Project p = new Project(e);
                Assertion.AssertEquals(true, p.ParentEngine.BuildEnabled);
                Assertion.AssertEquals(p.ParentEngine.BuildEnabled, p.BuildEnabled);
            }

            /// <summary>
            /// Constructor Test, Ensure GloabalProperties inherits from parent engine
            /// </summary>
            [Test]
            public void ConstructDefault_GlobalProperties_InheritParent()
            {
                Engine e = new Engine();
                e.GlobalProperties.SetProperty("name", "value");
                Project p = new Project(e);
                Assertion.AssertEquals(p.ParentEngine.GlobalProperties.Count, p.GlobalProperties.Count);
                Assertion.AssertEquals("value", p.ParentEngine.GlobalProperties["name"].Value);
            }

            /// <summary>
            /// Constructor Test, With Engine Object, unknown ToolsVersion 999999.
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidOperationException))]
            public void ConstructToolsVersion_Unknown()
            {
                Project p = new Project(new Engine(), "999999");
            }

            /// <summary>
            /// Constructor Test, with Engine object, known ToolsVersion 999999.
            /// </summary>
            [Test]
            public void ConstructToolsVersion_Known()
            {
                Engine e = new Engine();
                e.Toolsets.Add(new Toolset("999999", @"c:\"));
                e.DefaultToolsVersion = "999999";
                Project p = new Project(e, "999999");
                Assertion.AssertNotNull(p);
                Assertion.AssertEquals("999999", p.ToolsVersion);
            }

            /// <summary>
            /// Constructor Test, with Engine object, Null ToolsVersion 
            /// </summary>
            [Test]
            public void ConstructToolsVersion_Null()
            {
                Project p = new Project(new Engine(), null);
                Assertion.AssertNotNull(p.DefaultToolsVersion);
                Assertion.AssertEquals(p.ParentEngine.DefaultToolsVersion, p.DefaultToolsVersion);
            }
        }

        /// <summary>
        /// Tests for DefaultTools property
        /// </summary>
        [TestFixture]
        public sealed class DefaultTools
        {
            // See Project constructor tests for additional interactions and tests.

            /// <summary>
            /// DefaultToolsVersion, set and get in OM with known value.
            /// </summary>
            [Test]
            public void DefaultToolsVersionSetGet_2_0()
            {
                Project p = new Project(new Engine());
                p.DefaultToolsVersion = "2.0";
                Assertion.AssertEquals("2.0", p.DefaultToolsVersion);
            }

            /// <summary>
            /// DefaultToolsVersion, assert is dirty after setting ToolsVersion.
            /// </summary>
            [Test]
            public void DefaultToolsVersionIsDirtyAfterSet()
            {
                Project p = new Project(new Engine());
                Assertion.AssertEquals(false, p.IsDirty);
                p.DefaultToolsVersion = "2.0";
                Assertion.AssertEquals(true, p.IsDirty);
            }

            /// <summary>
            /// DefaultToolsVersion, set and get in OM. Unknown value
            /// </summary>
            [Test]
            public void DefaultToolsVersionSetGet_Unknown()
            {
                Project p = new Project(new Engine());
                p.DefaultToolsVersion = "999999";

                // setting an unknown ToolsVersion will cause the DefaultToolsVersion (and ToolsVersion) 
                // to default to "4.0"
                Assertion.AssertEquals("4.0", p.DefaultToolsVersion);
            }

            /// <summary>
            /// DefaultToolsVersion, set in XML, get in OM. Value 4.0
            /// </summary>
            [Test]
            public void DefaultToolsVersionGetWhenSetInXML_4_0()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified);
                Assertion.AssertEquals("4.0", p.DefaultToolsVersion);
            }
        }

        /// <summary>
        /// Tests for HasToolsVersionAttribute property
        /// </summary>
        [TestFixture]
        public sealed class HasToolsVersionAttribute
        {
            /// <summary>
            /// HasToolsVersionAttribute, load with tools version, check flag true.
            /// </summary>
            [Test]
            public void HasToolsVersionAttributeFromDisk_True()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentSimpleTools35);
                Assertion.AssertEquals(true, p.HasToolsVersionAttribute);
            }

            /// <summary>
            /// HasToolsVersionAttribute, set in OM, check flag false.
            /// </summary>
            [Test]
            public void HasToolsVersionAttributeCreateInOM_False()
            {
                Engine e = new Engine();
                e.Toolsets.Add(new Toolset("999999", @"c:\"));
                e.DefaultToolsVersion = "999999";
                Project p = new Project(e);
                Assertion.AssertEquals("999999", p.ToolsVersion);
                Assertion.AssertEquals(false, p.HasToolsVersionAttribute);
            }

            /// <summary>
            /// HasToolsVersionAttribute, create without tools version, check flag false.
            /// </summary>
            [Test]
            public void HasToolsVersionAttribute_False()
            {
                Project p = new Project(new Engine());
                Assertion.AssertEquals(false, p.HasToolsVersionAttribute);
            }
        }

        /// <summary>
        /// Tests for the ToolsVersion property
        /// </summary>
        [TestFixture]
        public sealed class ToolsVersion
        {
            /// <summary>
            /// ToolsVersion, set and get in OM. Value 3.5
            /// </summary>
            [Test]
            public void ToolsVersionGetWhenSetInXml_3_5()
            {
                if (FrameworkLocationHelper.PathToDotNetFrameworkV35 != null)
                {
                    Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentSimpleTools35);
                    Assertion.AssertEquals("3.5", p.ToolsVersion);
                }
                else
                {
                    Assert.Ignore(".NET Framework 3.5 is required for this test, but is not installed."); 
                }
            }

            /// <summary>
            /// ToolsVersion, set and get in OM.
            /// </summary>
            [Test]
            public void ToolsVersionCompareWithDefault()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentSimpleTools35);
                Assertion.AssertEquals(p.DefaultToolsVersion, p.ToolsVersion);
            }

            /// <summary>
            /// ToolsVersion, set and get in OM with known Value.
            /// </summary>
            [Test]
            public void ToolsVersionGetWhenSetInXml_Custom_Unknown()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentSimpleCustomToolsVersion);

                // An unknown ToolsVersion was used, so we just sneakily reset it to 4.0
                Assertion.AssertEquals(p.ToolsVersion, "4.0");
            }

            /// <summary>
            /// ToolsVersion, get from OM when set in Project object constructor
            /// </summary>
            [Test]
            public void ToolsVersionGetWhenSetInProjectConstructor_3_5()
            {
                if (FrameworkLocationHelper.PathToDotNetFrameworkV35 != null)
                {
                    Project p = new Project(new Engine(), "3.5");
                    Assertion.AssertEquals("3.5", p.ToolsVersion);
                }
                else
                {
                    Assert.Ignore(".NET Framework 3.5 is required for this test, but is not installed."); 
                }
            }

            /// <summary>
            /// ToolsVersion, get from OM when set in Project object constructor
            /// </summary>
            [Test]
            public void ToolsVersionGetWhenSetInProjectConstructor_4_0()
            {
                Project p = new Project(new Engine(), "4.0");
                Assertion.AssertEquals("4.0", p.ToolsVersion);
            }
        }

        /// <summary>
        /// Tests for the TargetCollection accessor
        /// </summary>
        [TestFixture]
        public sealed class TargetsCollection
        {
            // More Tests in the target collection class. 

            /// <summary>
            /// Target Test, Get a list of targets
            /// </summary>
            [Test]
            public void TargetCollectionGet()
            {
                Project p = new Project(new Engine());
                p.Targets.AddNewTarget("TestTarget");
                Assertion.AssertEquals(true, p.IsDirty);
                Assertion.AssertEquals("TestTarget", p.Targets["TestTarget"].Name);
            }
        }

        /// <summary>
        /// Tests for AddNewUsingTaskFromAssemblyFile, AddNewUsingTaskFromAssemblyName and UsingTaskCollection
        /// </summary>
        [TestFixture]
        public sealed class UsingTask
        {
            /// <summary>
            /// UsingTasks Tests,
            /// </summary>
            [Test]
            public void AddNewUsingTaskFromAssemblyFileUsingTasksCollectionGet()
            {
                Project p = new Project(new Engine());
                p.AddNewUsingTaskFromAssemblyFile("UsingTaskName", @"c:\assembly.dll");
                object o = p.EvaluatedItems; // force evaluation of imported projects.
                Assertion.AssertEquals(true, p.IsDirty);
                XmlNodeList nl = CompatibilityTestHelpers.GetNodesWithName(p.Xml, "UsingTask");
                Assertion.AssertEquals(1, nl.Count);
                Assertion.AssertEquals("UsingTaskName", nl[0].Attributes["TaskName"].Value);
                Assertion.AssertEquals(@"c:\assembly.dll", nl[0].Attributes["AssemblyFile"].Value);
                Assertion.AssertEquals(2, nl[0].Attributes.Count); // no condition;
            }

            /// <summary>
            /// AddNewUsingTask Test, adding an invalid assembly is okay
            /// </summary>
            [Test]
            public void AddNewUsingTaskFromInvalidAssemblyNotEvaluated()
            {
                Project p = new Project(new Engine());
                p.AddNewUsingTaskFromAssemblyFile("taskName", @"invalid|.dll");
                Assertion.Assert("no exception", true);
            }

            /// <summary>
            /// AddNewUsingTask Test, adding an invalid assembly is okay, until you evaluate
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void AddNewUsingTaskFromInvalidAssemblyEvaluated()
            {
                Project p = new Project(new Engine());
                p.AddNewUsingTaskFromAssemblyFile("taskName", @"invalid|.dll");
                object o = p.EvaluatedItems; // force evaluation of imported projects.
            }

            /// <summary>
            /// AddNewUsingTask Test, adding an invalid assembly is okay, until you evaluate
            /// </summary>
            [Test]
            public void AddNewUsingTaskFromAssemblyName()
            {
                Project p = new Project(new Engine());
                p.AddNewUsingTaskFromAssemblyName("UsingTaskName", "UsingAssemblyName");
                object o = p.EvaluatedItems; // force evaluation of imported projects.
                XmlNodeList nl = CompatibilityTestHelpers.GetNodesWithName(p.Xml, "UsingTask");
                Assertion.AssertEquals(1, nl.Count);
                Assertion.AssertEquals("UsingTaskName", nl[0].Attributes["TaskName"].Value);
                Assertion.AssertEquals("UsingAssemblyName", nl[0].Attributes["AssemblyName"].Value);
                Assertion.AssertEquals(2, nl[0].Attributes.Count); // no condition;
            }
        }

        /// <summary>
        /// Tests for HasUnsavedChanges TimeOfLastChange 
        /// </summary>
        [TestFixture]
        public sealed class Dirty
        {
            /// <summary>
            /// MarkProjectAsDirty() set, HasUnsavedChanges get Test.
            /// </summary>
            [Test]
            public void MarkProjectAsDirty_True()
            {
                Project p = new Project(new Engine());
                Assertion.AssertEquals(false, p.IsDirty);
                p.MarkProjectAsDirty();
                Assertion.AssertEquals(true, p.IsDirty);
            }

            /// <summary>
            /// MarkProjectAsDirty() set, HasUnsavedChanges get Test.
            /// </summary>
            [Test]
            public void MarkProjectAsDirty_False()
            {
                Project p = new Project(new Engine());
                p.MarkProjectAsDirty();
                Assertion.AssertEquals(true, p.IsDirty);
                string projectPath = String.Empty;
                try
                {
                    projectPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    p.Load(projectPath);
                    Assertion.AssertEquals(false, p.IsDirty);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(projectPath);
                }
            }

            /// <summary>
            ///  TimeOfLastChange Test, check time is changed when isDirty is called.
            /// </summary>
            [Test]
            public void TimeOfLastDirtyMarkAsDirty()
            {
                Project p = new Project(new Engine());
                DateTime before = DateTime.Now;
                Thread.Sleep(100);
                p.MarkProjectAsDirty();
                Thread.Sleep(100);
                DateTime after = DateTime.Now;
                Assertion.AssertEquals(true, p.TimeOfLastDirty > before);
                Assertion.AssertEquals(true, p.TimeOfLastDirty < after);
            }

            /// <summary>
            ///  TimeOfLastChange Test, Check that dirty time is set at construction.
            /// </summary>
            [Test]
            public void TimeOfLastDirtyAtConstruction()
            {
                DateTime before = DateTime.Now;
                Thread.Sleep(100);
                Project p = new Project(new Engine());
                Thread.Sleep(100);
                DateTime after = DateTime.Now;
                Assertion.AssertEquals(true, p.TimeOfLastDirty > before);
                Assertion.AssertEquals(true, p.TimeOfLastDirty < after);
            }
        }

        /// <summary>
        /// Tests for IsValidated, SchemaFile
        /// </summary>
        [TestFixture]
        public sealed class Validation
        {
            /// <summary>
            /// IsValidated test, get set:True
            /// </summary>
            [Test]
            public void IsValidated_True()
            {
                Project p = new Project(new Engine());
                p.IsValidated = true;
                Assertion.AssertEquals(true, p.IsValidated);
            }

            /// <summary>
            /// IsValidated test, get set:False
            /// </summary>
            [Test]
            public void IsValidated_False()
            {
                Project p = new Project(new Engine());
                p.IsValidated = false;
                Assertion.AssertEquals(false, p.IsValidated);
            }

            /// <summary>
            /// IsValidated test, get set:True
            /// </summary>
            [Test]
            public void IsValidated_Default()
            {
                Project p = new Project(new Engine());
                Assertion.AssertEquals(false, p.IsValidated);
            }

            /// <summary>
            /// SchemaFile Test, get default
            /// </summary>
            [Test]
            public void SchemaFileGetDefault()
            {
                Project p = new Project(new Engine());
                Assertion.AssertEquals(null, p.SchemaFile);
            }

            /// <summary>
            /// SchemaFile Test, set to null.
            /// </summary>
            [Test]
            public void SchemaFileSet_Null()
            {
                Project p = new Project(new Engine());
                p.SchemaFile = null;
            }

            /// <summary>
            /// SchemaFile Test, set to url
            /// </summary>
            [Test]
            public void SchemaFileSet_Url()
            {
                Project p = new Project(new Engine());
                p.SchemaFile = CompatibilityTestHelpers.SchemaPathMSBuild;
                Assertion.AssertEquals(CompatibilityTestHelpers.SchemaPathMSBuild, p.SchemaFile);
            }

            /// <summary>
            ///  SchemaFileIsValidated Test, validate valid project xml against a valid schema
            /// </summary>
            [Test]
            public void SchemaFileIsValidatedDefaultSchema()
            {
                Project p = new Project(new Engine());
                p.IsValidated = true;
                p.LoadXml(TestData.Content3SimpleTargetsDefaultSpecified);
            }

            /// <summary>
            ///  SchemaFileIsValidated Test, validate valid project xml against valid schema
            /// </summary>
            [Test]
            public void SchemaFileIsValidatedSchemaSpecified()
            {
                Project p = new Project(new Engine());
                p.IsValidated = true;
                p.SchemaFile = CompatibilityTestHelpers.SchemaPathMSBuild;
                p.LoadXml(TestData.Content3SimpleTargetsDefaultSpecified);
            }

            /// <summary>
            ///  SchemaFileIsValidated Test, validate invalid project xml against valid schema
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void SchemaFileIsValidatedSchemaSpecifiedInvalidXml_InMemory()
            {
                Project p = new Project(new Engine());
                p.IsValidated = true;
                p.SchemaFile = CompatibilityTestHelpers.SchemaPathMSBuild;
                p.LoadXml(TestData.ContentSimpleInvalidMSBuildXml);
            }

            /// <summary>
            ///  SchemaFileIsValidated Test, Validate valid project xml against invalid schema
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void SchemaFileIsValidatedValidXmlInvalidSchema()
            {
                Project p = new Project(new Engine());
                p.IsValidated = true;
                p.SchemaFile = CompatibilityTestHelpers.SchemaPathBuildCore; // not the msbuild schema
                p.LoadXml(TestData.ContentSimpleInvalidMSBuildXml);
            }

            /// <summary>
            ///  SchemaFileIsValidated Test, setup valid project xml against invalid schema but turn 
            ///  validation off so it never runs.
            /// </summary>
            [Test]
            public void SchemaFileValidXmlInvalidSchema_IsValidatedFalse()
            {
                Project p = new Project(new Engine());
                p.IsValidated = false;
                p.SchemaFile = CompatibilityTestHelpers.SchemaPathBuildCore; // not the msbuild schema
                p.LoadXml(TestData.Content3SimpleTargetsDefaultSpecified);
            }

            /// <summary>
            ///  SchemaFileIsValidated Test, validate invalid project file against valid schema
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void SchemaFileIsValidatedSchemaSpecifiedInvalidXml_FromDisk()
            {
                string projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("file.proj", TestData.ContentSimpleInvalidMSBuildXml);
                try
                {
                    Project p = new Project(new Engine());
                    p.IsValidated = true;
                    p.SchemaFile = CompatibilityTestHelpers.SchemaPathMSBuild;
                    p.Load(projectPath);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(projectPath);
                }
            }

            /// <summary>
            /// SchemaFileIsValidated Test, validate a valid project file against valid schema
            /// </summary>
            [Test]
            public void SchemaFileIsValidatedInteraction()
            {
                string projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("file.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                try
                {
                    Project p = new Project(new Engine());
                    p.IsValidated = true;
                    p.SchemaFile = CompatibilityTestHelpers.SchemaPathMSBuild;
                    p.Load(projectPath);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(projectPath);
                }
            }
        }

        /// <summary>
        /// Tests for  Build ResetBuildStatus BuildFlags.
        /// </summary>
        [TestFixture]
        public sealed class Build
        {
            /// <summary>
            /// Build Test, Build the default target as specified in xml.
            /// </summary>
            [Test]
            public void Build_DefaultTarget()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                p.Build();
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target Default"));
            }

            /// <summary>
            /// Build Test, build the default target (ie the first in the file) when no default is specified in the xml
            /// </summary>
            [Test]
            public void Build_FirstTargetInFile()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified, logger);
                p.Build();
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target 1"));
            }

            /// <summary>
            /// Build Test, build a named target, overloading the default specified in the xml
            /// </summary>
            [Test]
            public void Build_SpecifiedInOM()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                p.Build("Target1");
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target 1"));
            }

            /// <summary>
            /// Build Test, build named targets as param list (not implemented), overloading default. Expect Error.
            /// </summary>
            [Test]
            public void Build_SpecifiedListAsParam()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                bool buildSuccessful = p.Build("Target1; Target2");
                Assertion.AssertEquals(false, buildSuccessful);
                Assertion.AssertEquals(true, logger.FullLog.Contains("MSB4057"));
            }

            /// <summary>
            /// Build Test,  overload the xml default target in the OM, then build
            /// </summary>
            [Test]
            public void BuildOverloadDefaultTargetOM()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                p.DefaultTargets = "Target1; Target2";
                bool buildSuccessful = p.Build();
                Assertion.AssertEquals(true, buildSuccessful);
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target 1"));
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target 2"));
            }

            /// <summary>
            /// Build Test, build an array of targets where one target is invalid
            /// </summary>
            [Test]
            public void BuildListInvalidTargets()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                bool buildSuccessful = p.Build(new string[] { "Target1", "TargetInvalid" });
                Assertion.AssertEquals(false, buildSuccessful);
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target 1"));
                Assertion.AssertEquals(true, logger.FullLog.Contains("error MSB4057"));
            }

            /// <summary>
            /// Build Test, build an array of valid targets
            /// </summary>
            [Test]
            public void BuildListValidTargets()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                p.Build(new string[] { "Target1", "Target2" });
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target 1"));
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target 2"));
            }

            /// <summary>
            /// Build Test, ensure initial and default targets get executed in the appropriate order
            /// </summary>
            [Test]
            public void BuildExecutionprecedence()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified, logger);
                p.InitialTargets = "Target3";
                p.DefaultTargets = "Target1";
                p.Build();
                bool delta = logger.FullLog.IndexOf("Executed Target 3") < logger.FullLog.IndexOf("Executed Target 1");
                Assertion.AssertEquals(true, delta);
            }

            /// <summary>
            /// Build Test, ensure the precedented execution of initial targets in imported projects
            /// </summary>
            [Test]
            public void BuildImportExecutionprecedence()
            {
                // Set up
                MockLogger logger = new MockLogger();
                string projImport1 = ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.proj", TestData.ContentImport1);
                string projImport2 = ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.proj", TestData.ContentImport2);
                try
                {
                    // Test
                    Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified, logger);
                    p.Imports.AddNewImport(projImport1, "true");
                    p.Imports.AddNewImport(projImport2, "true");
                    p.Build("ImportTarget1b");
                    bool delta1 = logger.FullLog.IndexOf("Executed ImportTarget 1a") < logger.FullLog.IndexOf("Executed ImportTarget 2a");
                    bool delta2 = logger.FullLog.IndexOf("Executed ImportTarget 2a") < logger.FullLog.IndexOf("Executed ImportTarget 1b");
                    Assertion.AssertEquals(true, delta1);
                    Assertion.AssertEquals(true, delta2);
                }
                finally
                {
                    // Tear down
                    CompatibilityTestHelpers.RemoveFile(projImport1);
                    CompatibilityTestHelpers.RemoveFile(projImport2);
                }
            }

            /// <summary>
            /// Build Test, ensure that removed imports are not executed when building
            /// </summary>
            [Test]
            public void BuildImportRemovedImport()
            {
                string projImport1 = String.Empty;
                string projImport2 = String.Empty;
                try
                {
                    MockLogger logger = new MockLogger();
                    projImport1 = ObjectModelHelpers.CreateFileInTempProjectDirectory("import1.proj", TestData.ContentImport1);
                    projImport2 = ObjectModelHelpers.CreateFileInTempProjectDirectory("import2.proj", TestData.ContentImport2);
                    Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified, logger);
                    p.Imports.AddNewImport(projImport1, "true");
                    p.Imports.AddNewImport(projImport2, "true");
                    object o = p.EvaluatedItems; // force evaluation of imported projects.               
                    p.Imports.RemoveImport(CompatibilityTestHelpers.GetImportByProjectPath(p.Imports, projImport2));
                    p.Build("ImportTarget1b");
                    Assertion.AssertEquals(false, logger.FullLog.Contains("Executed ImportTarget 2a"));
                }
                finally
                {
                    // Tear down
                    CompatibilityTestHelpers.RemoveFile(projImport1);
                    CompatibilityTestHelpers.RemoveFile(projImport2);
                }
            }

            /// <summary>
            /// Build Test, a failed build target should not return its outputs.
            /// </summary>
            [Test]
            public void BuildOutputInvalidTargets()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentInvalidTargetsWithOutput, logger);
                Hashtable outputs = new Hashtable();
                bool buildSuccessful = p.Build(new string[] { "Target1" }, outputs);
                Assertion.AssertEquals(false, buildSuccessful);
                Assertion.AssertEquals(0, outputs.Count);
            }

            /// <summary>
            /// Build Test, a successful build should return outputs
            /// </summary>
            [Test]
            public void BuildOutputValidTargets()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentValidTargetsWithOutput, logger);
                Hashtable outputs = new Hashtable();
                bool buildSuccessful = p.Build(new string[] { "Target1" }, outputs);
                Assertion.AssertEquals(true, buildSuccessful);
                Assertion.AssertEquals(1, outputs.Count);
            }

  /// <summary>
            /// BuildTest, Ensure that the build skips cached targets where flag permits.
            /// </summary>
            [Test]
            public void BuildIncrementalTargetExecutionFlag_On()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified, logger);
                p.Build("Target2");
                bool buildSuccessful = p.Build(new string[] { "Target1" }, null, BuildSettings.DoNotResetPreviouslyBuiltTargets);
                Assertion.AssertEquals(true, buildSuccessful);
                string skippedMessage = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "Target2");
                Assertion.AssertEquals(true, logger.FullLog.Contains(skippedMessage));
            }

            /// <summary>
            /// BuildTest, Ensure that .Build() rebuilds all targets, due to no permit flag.
            /// </summary>
            [Test]
            public void BuildIncrementalTargetExecutionFlag_Off()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified, logger);
                p.Build("Target2");
                bool buildSuccessful = p.Build(new string[] { "Target1" }, null, BuildSettings.None);
               
                Assertion.AssertEquals(true, buildSuccessful);
                string skippedMessage = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "Target1");
                string skippedMessage2 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "Target2");
                string skippedMessage3 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Target1");
                string skippedMessage4 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Target2");
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage));
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage2));
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage3));
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage4));
            }

            /// <summary>
            ///  ResetBuildStatus Test, Ensure that .Build() does not skip cached targets after a reset, where flags permit.
            /// </summary>
            [Test]
            public void BuildIncrementalTargetExecutionWithBuildResetFlag_On()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified, logger);
                p.Build("Target2");
                p.ResetBuildStatus(); // this is  called in build anyway!

                bool buildSuccessful = p.Build(new string[] { "Target1" }, null, BuildSettings.DoNotResetPreviouslyBuiltTargets);
                Assertion.AssertEquals(true, buildSuccessful);
                string skippedMessage = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "Target1");
                string skippedMessage2 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "Target2");
                string skippedMessage3 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Target1");
                string skippedMessage4 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Target2");
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage));
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage2));
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage3));
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage4));
            }

            /// <summary>
            ///  ResetBuildStatus Test, Ensure that .Build() automatically rebuilds all targets after a reset. No flags
            /// </summary>
            [Test]
            public void BuildIncrementalTargetExecutionWithBuildResetFlag_Off()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified, logger);
                p.Build("Target2");
                p.ResetBuildStatus(); // this is  called in build anyway!
                bool buildSuccessful = p.Build(new string[] { "Target1" }, null, BuildSettings.None);

                Assertion.AssertEquals(true, buildSuccessful);
                string skippedMessage = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "Target1");
                string skippedMessage2 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteSuccess", "Target2");
                string skippedMessage3 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Target1");
                string skippedMessage4 = ResourceUtilities.FormatResourceString("TargetAlreadyCompleteFailure", "Target2");
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage));
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage2));
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage3));
                Assertion.AssertEquals(false, logger.FullLog.Contains(skippedMessage4));
            }


            /// <summary>
            /// BuildTest, Check that all virtual items created by a target are removed on ResetBuildStatus();
            /// </summary>
            [Test]
            public void ResetBuildVirtualPropertyRemoval()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentCreatePropertyTarget);
                p.Build("CreatePropertyTarget");
                Assertion.AssertEquals("v", p.GetEvaluatedProperty("p"));
                p.ResetBuildStatus();
                Assertion.AssertEquals(null, p.GetEvaluatedProperty("p")); 
            }

            /// <summary>
            /// BuildTest, Check that all virtual properties changed by a target are reset on ResetBuildStatus();
            /// </summary>
            [Test]
            public void ResetBuildVirtualPropertyReset()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentCreatePropertyTarget);
                p.SetProperty("p", "v1");
                p.Build("CreatePropertyTarget");
                Assertion.AssertEquals("v", p.GetEvaluatedProperty("p"));
                p.ResetBuildStatus();
                Assertion.AssertEquals("v1", p.GetEvaluatedProperty("p"));
            }

            /// <summary>
            /// BuildTest, Check that all virtual items created by a target are removed on ResetBuildStatus();
            /// </summary>
            [Test]
            public void ResetBuildVirtualItemRemoved()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentCreateItemTarget);
                p.Build("CreateItemTarget");
                Assertion.AssertEquals("i", p.EvaluatedItems[0].Include);
                int preCount = p.EvaluatedItems.Count;
                p.ResetBuildStatus();
                Assertion.AssertEquals(preCount - 1, p.EvaluatedItems.Count);
            }

            /// <summary>
            /// BuildTest, Check that all virtual items changed by a target are reset on ResetBuildStatus();
            /// </summary>
            [Test]
            public void ResetBuildVirtualItemReset()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentCreateItemTarget);
                p.AddNewItem("BuildItem", "i1");
                p.Build("CreateItemTarget");
                Assertion.AssertEquals("i1", p.EvaluatedItems[0].Include);
                Assertion.AssertEquals("i", p.EvaluatedItems[1].Include);
                int preCount = p.EvaluatedItems.Count;
                p.ResetBuildStatus();
                Assertion.AssertEquals("i1", p.EvaluatedItems[0].Include);
                Assertion.AssertEquals(preCount - 1, p.EvaluatedItems.Count);
            }
        }
      
        /// <summary>
        /// Tests for BuildEnabled property.
        /// </summary>
        [TestFixture]
        public sealed class BuildEnabled
        {
            /// <summary>
            ///  BuildEnabled Test, does project inherit from global engine, where value is default
            /// </summary>
            [Test]
            public void BuildEnabledInheritGlobalEngine_Default()
            {
                Project p = new Project();
                Assertion.AssertEquals(true, p.BuildEnabled);
            }

            /// <summary>
            ///  BuildEnabled Test, does project inherit from global engine, where value is false;
            /// </summary>
            [Test]
            public void BuildEnabledInheritGlobalEngine_False()
            {
                Project p = new Project();
                Engine.GlobalEngine.BuildEnabled = false;
                Assertion.AssertEquals(false, p.BuildEnabled);
            }

            /// <summary>
            ///  BuildEnabled Test, does project inherit from global engine, where value is true;
            /// </summary>
            [Test]
            public void BuildEnabledInheritGlobalEngine_True()
            {
                Project p = new Project();
                Engine.GlobalEngine.BuildEnabled = true;
                Assertion.AssertEquals(true, p.BuildEnabled);
            }

            /// <summary>
            ///  BuildEnabled Test, does project inherit  from parent Engine, where value is default
            /// </summary>
            [Test]
            public void BuildEnabledInheritParentEngine_Default()
            {
                Engine e = new Engine();
                Project p = new Project(e);
                Assertion.AssertEquals(e.BuildEnabled, p.BuildEnabled);
            }

            /// <summary>
            ///  BuildEnabled Test, project inheritance from parent Engine, where value is true.
            /// </summary>
            [Test]
            public void BuildEnabledInheritParentEngine_True()
            {
                Engine e = new Engine();
                e.BuildEnabled = true;
                Project p = new Project(e);
                Assertion.AssertEquals(e.BuildEnabled, p.BuildEnabled);
            }

            /// <summary>
            ///  BuildEnabled Test, project inheritance from parent Engine, where value is false.
            /// </summary>
            [Test]
            public void BuildEnabledInheritParentEngine_False()
            {
                Engine e = new Engine();
                e.BuildEnabled = false;
                Project p = new Project(e);
                Assertion.AssertEquals(e.BuildEnabled, p.BuildEnabled);
            }

            /// <summary>
            /// BuildEnabled Test, getting and setting.
            /// </summary>
            [Test]
            public void BuildEnabledSetGet()
            {
                Project p = new Project(new Engine());
                bool oldState = p.BuildEnabled;
                p.BuildEnabled = !oldState;
                Assertion.AssertEquals(!oldState, p.BuildEnabled);
            }
        }

        /// <summary>
        /// ParentEngine Tests.
        /// </summary>
        [TestFixture]
        public sealed class ParentEngine
        {
            // See Engine Test class for more tests.

            /// <summary>
            /// IsValidated test, get set:True
            /// </summary>
            [Test]
            public void ParentEngine_Get()
            {
                Engine e = new Engine();
                e.BinPath = @"c:\somepath";
                Project p = new Project(e);
                Assertion.AssertEquals(@"c:\somepath", p.ParentEngine.BinPath);
            }
        }

        /// <summary>
        /// Tests for the Encoding property
        /// </summary>
        [TestFixture]
        public sealed class XmlEncoding
        {
            // More extensive tests of this property elsewhere in unit testing framework.

            /// <summary>
            /// Encoding Test, Get, when set to default (UTF8) 
            /// </summary>
            [Test]
            public void Encoding_Get_Default_UTF8()
            {
                Project p = new Project(new Engine());
                Assertion.AssertEquals(Encoding.UTF8, p.Encoding);
            }

            /// <summary>
            /// Encoding Test, get when set to an invalid encoding
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void Encoding_Get_Custom_Invalid()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentSimpleInvalidEncoding);
                Encoding e = p.Encoding; // This assignment throws the exception.
            }

            /// <summary>
            /// Encoding Test, get when set to invalid encoding 
            /// </summary>
            [Test]
            public void Encoding_Get_Custom_UTF16()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentSimpleUTF16);
                Assertion.AssertEquals(Encoding.Unicode, p.Encoding);
            }
        }

        /// <summary>
        /// Tests for Load, LoadXml, Load TextReader.
        /// </summary>
        [TestFixture]
        public sealed class Load
        {
            /// <summary>
            /// Load Test, with Project File Name: String.Empty
            /// </summary>
            /// <bugs>
            ///  <bug>
            ///   Project.cs
            ///   Before: ErrorUtilities.VerifyThrowArgument(projectFileName.Length > 0, "EmptyProjectFileName" +buildEventContext.ToString());
            ///   After: ErrorUtilities.VerifyThrowArgument(projectFileName.Length > 0, "EmptyProjectFileName"); 
            ///   Threw ArgumentException due to invalid keying into resources.
            ///  </bug>
            /// </bugs>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void LoadProjectFileName_EmptyString()
            {
                Project p = new Project(new Engine());
                p.Load(String.Empty);
            }

            /// <summary>
            /// Load Test, with Project File Name: Null
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void LoadProjectFileName_Null()
            {
                Project p = new Project(new Engine());
                p.Load((String)null);
            }

            /// <summary>
            ///   Load Test, load a project file twice. Unload Previous project, dump from cache. 
            /// </summary>
            /// <remarks>
            ///  This is a test for nametable colissions on shared engines. 
            ///  An Debug assertion was removed from Engine to prevent the assert from blocking suites.
            /// </remarks>
            [Test]
            public void LoadTwoProjectThatShareEngine()
            {
                string projectPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("project.proj", TestData.Content3SimpleTargetsDefaultSpecified);
                try
                {
                    Project p = new Project();
                    p.Load(projectPath);
                    Project p2 = new Project();
                    p2.Load(projectPath);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(projectPath);
                }
            }

            /// <summary>
            /// Load Test, with Project File Name: Null
            /// </summary>
            /// <remarks>
            ///    - This should throw ArgumentException, compare with AddNewImport()
            /// </remarks>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectFile_FileContentEmpty()
            {
                // Setup
                string tempProjectFilePath = null;
                try
                {
                    tempProjectFilePath = CompatibilityTestHelpers.CreateTempProjectFile(String.Empty);

                    // Execute
                    Project p = new Project(new Engine());
                    p.Load(tempProjectFilePath);
                }
                finally
                {
                    // Tear down
                    CompatibilityTestHelpers.RemoveFile(tempProjectFilePath);
                }
            }

            /// <summary>
            /// Load Test, with Valid Project File Name, which does not exist.
            /// </summary>
            /// <remarks> 
            /// FileNotFound throws to expose ArgumentException in version v9 and earlier
            /// versions post v9 should throw and expose FileNotFoundException
            /// </remarks>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void LoadProjectFileName_DoesNotExist()
            {
                Project p = new Project(new Engine());
                p.Load("doesNotExist.proj"); // doesNotExist.proj does not exist on disk
            }

            /// <summary>
            /// Load Test, With Valid Project File Name. Standard Path
            /// </summary>
            [Test]
            public void LoadProjectFileName_FileSystemPath()
            {
                string tempProjectFilePath = CompatibilityTestHelpers.CreateTempProjectFile();
                Project p = new Project(new Engine());
                p.Load(tempProjectFilePath);
                Assertion.AssertEquals(true, p.Targets.Exists("TestTarget"));
                CompatibilityTestHelpers.RemoveFile(tempProjectFilePath);
            }

            /// <summary>
            /// Load Test, Check FullFileName is set 
            /// </summary>
            /// <bugs>
            ///  <bug>
            ///    Regression Test for Bug VSWhidbey 415236
            ///  </bug>
            /// </bugs>
            [Test]
            public void LoadProjectFileName_isFullFileNameSet()
            {
                string tempProjectFilePath = CompatibilityTestHelpers.CreateTempProjectFile();
                Project p = new Project(new Engine());
                p.Load(tempProjectFilePath);
                Assertion.AssertEquals(tempProjectFilePath, p.FullFileName);
                CompatibilityTestHelpers.RemoveFile(tempProjectFilePath);
            }

            /// <summary>
            /// Load Test, Check FullFileName is set 
            /// </summary>
            /// <bugs>
            ///  <bug>
            ///    Regression Test for Bug VSWhidbey 415236
            ///  </bug>
            /// </bugs>
            [Test]
            public void LoadProjectFileName_DirtyIsFalse()
            {
                string tempProjectFilePath = CompatibilityTestHelpers.CreateTempProjectFile();
                Project p = new Project(new Engine());
                p.Load(tempProjectFilePath);
                Assertion.AssertEquals(false, p.IsDirty);
                CompatibilityTestHelpers.RemoveFile(tempProjectFilePath);
            }

            /// <summary>
            /// Load Test, With Valid solution file.
            /// </summary>
            [Test]
            [Ignore("not yet implemented")]
            public void LoadSolutionFile()
            {
                // NYI
            }

            /// <summary>
            /// Load Test, file contains invalid/corrupt XML
            /// </summary>
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectFile_ContainsInvalidXml()
            {
                string tempProjectFilePath = CompatibilityTestHelpers.CreateTempProjectFile(TestData.ContentSimpleInvalidXml);
                try
                {
                    Project p = new Project(new Engine());
                    p.Load(tempProjectFilePath);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(tempProjectFilePath);
                }
            }

            /// <summary>
            /// Load Test, With Valid Project File Name, which is locked for read access
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectFile_UnauthorizedAccessException()
            {
                string tempProjectFilePath = CompatibilityTestHelpers.CreateTempProjectFile(TestData.ContentSimpleTools35);
                CompatibilityTestHelpers.SetFileAccessPermissions(tempProjectFilePath, FileSystemRights.Read, AccessControlType.Deny);
                Project p = new Project(new Engine());
                try
                {
                    p.Load(tempProjectFilePath);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(tempProjectFilePath);
                }
            }

            /// <summary>
            /// Load Test, InvalidXML Exception
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectFile_XmlException()
            {
                string tempProjectFilePath = CompatibilityTestHelpers.CreateTempProjectFile(TestData.ContentSimpleInvalidXml);
                CompatibilityTestHelpers.SetFileAccessPermissions(tempProjectFilePath, FileSystemRights.Read, AccessControlType.Deny);
                Project p = new Project(new Engine());

                try
                {
                    p.Load(tempProjectFilePath);
                }
                finally
                {
                    // Tear Down
                    CompatibilityTestHelpers.RemoveFile(tempProjectFilePath);
                }
            }

            /// <summary>
            /// Load Test, Invalid Path
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void LoadProjectFile_InvalidPath()
            {
                new Project().Load(@"|invalidpath\project.proj");
            }

            /// <summary>
            /// Load Test, SecurityException
            /// </summary>
            [Test]
            [Ignore("NYI")]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectFile_SecurityException()
            {
                // NYI
            }

            /// <summary>
            /// Load Test, NotSupportedException
            /// </summary>
            [Test]
            [Ignore("NYI")]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectFile_NotSupportedException()
            {
                // NYI
            }

            /// <summary>
            /// Load Test, IOException
            /// </summary>
            [Test]
            [Ignore("NYI")]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectFile_IOException()
            {
                // NYI
            }

            /// <summary>
            /// Load Test, With Path length over 256 Characters
            /// </summary>
            /// <remarks>
            /// The file refereced in this test does not exist, as it cannot be created in
            /// the file system. 
            /// </remarks>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void LoadProjectFileName_OverMaxPath()
            {
                Project p = new Project(new Engine());
                p.Load(CompatibilityTestHelpers.GenerateLongPath(256) + "doesNotExist.proj"); // doesNotExist.proj does not exist
            }

            /// <summary>
            /// Load Test, Project Load with a file that has missing imports, and IgnoreMissingImports as true.
            /// </summary>
            [Test]
            public void LoadProjectFileName_ProjectLoadSettings_IgnoreMissingImports()
            {
                string tempProjectFilePath = CompatibilityTestHelpers.CreateTempProjectFile(TestData.ContentMissingImports);
                Project p = new Project(new Engine());
                p.Load(tempProjectFilePath, ProjectLoadSettings.IgnoreMissingImports);
                Assertion.AssertNotNull(p);
                CompatibilityTestHelpers.RemoveFile(tempProjectFilePath);
            }

            /// <summary>
            /// Load Test, Project Load with a file that has missing imports
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectFileName_ProjectLoadSettings_CatchMissingImports()
            {
                string tempProjectFilePath = CompatibilityTestHelpers.CreateTempProjectFile(TestData.ContentMissingImports);
                Project p = new Project(new Engine());
                try
                {
                    p.Load(tempProjectFilePath, ProjectLoadSettings.None);
                }
                finally
                {
                    // Tear Down
                    CompatibilityTestHelpers.RemoveFile(tempProjectFilePath);
                }
            }

            /// <summary>
            /// Load Test, valid xml via a text reader
            /// </summary>
            [Test]
            public void LoadTextReader_Valid()
            {
                Project p = new Project(new Engine());
                p.Load(new StringReader(TestData.ContentSimpleTools35));
                Assertion.AssertNotNull(p);
            }

            /// <summary>
            /// Load Test, valid xml via a text reader
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectTextReader_Invalid()
            {
                Project p = new Project(new Engine());
                p.Load(new StringReader(TestData.ContentSimpleInvalidXml));
            }

            /// <summary>
            /// Load Test, valid xml via a text reader, catch missing imports
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectTextReader_CatchMissingImports()
            {
                Project p = new Project(new Engine());
                p.Load(new StringReader(TestData.ContentMissingImports), ProjectLoadSettings.None);
            }

            /// <summary>
            /// Load Test, null text reader
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void LoadProjectTextReader_NullTextReader()
            {
                TextReader tr = null;
                Project p = new Project(new Engine());
                p.Load(tr, ProjectLoadSettings.None);
            }

            /// <summary>
            /// Load Test, valid xml via a text reader, ignore missing imports
            /// </summary>
            [Test]
            public void LoadProjectTextReader_IgnoreMissingImports()
            {
                Project p = new Project(new Engine());
                p.Load(new StringReader(TestData.ContentMissingImports), ProjectLoadSettings.IgnoreMissingImports);
            }

            /// <summary>
            /// LoadXml Test, from String of xml
            /// </summary>
            [Test]
            public void LoadProjectXMLString_Valid()
            {
                Project p = new Project();
                p.LoadXml(TestData.Content3SimpleTargetsDefaultSpecified);
                Assertion.AssertEquals(String.Empty, p.FullFileName);
            }

            /// <summary>
            /// Load Test, from a string of INVALID xml, 
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidProjectFileException))]
            public void LoadProjectXMLString_Invalid()
            {
                Project p = new Project();
                p.LoadXml(TestData.ContentSimpleInvalidXml);
            }

            /// <summary>
            /// Load Test, from a string of xml, catching missing imports
            /// </summary>
            [Test]
            public void LoadProjectXMLString_CatchMissingImports()
            {
                Project p = new Project();
                p.LoadXml(TestData.ContentMissingImports, ProjectLoadSettings.IgnoreMissingImports);
                Assertion.AssertNotNull(p);
            }

            /// <summary>
            /// Load Test, from a string of xml 
            /// </summary>
            [Test]
            public void LoadProjectXMLString_IsDirtyAfterLoad()
            {
                Project p = new Project();
                p.LoadXml(TestData.ContentMissingImports, ProjectLoadSettings.IgnoreMissingImports);
                Assertion.AssertEquals(true, p.IsDirty);
            }

            /// <summary>
            /// Load Test, from a string of invalid xml 
            /// </summary>
            [Test]
            public void LoadProjectXMLString_IgnoreMissingImports()
            {
                Project p = new Project();
                p.LoadXml(TestData.ContentMissingImports, ProjectLoadSettings.IgnoreMissingImports);
                Assertion.AssertNotNull(p);
            }
        }

         /// <summary>
        /// Tests for Save, Save TextReader.
        /// </summary>
        [TestFixture]
        public sealed class Save 
        {
            /// <summary>
            ///  Save Test, Path is empty String
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void SaveToFileFileName_EmptyString()
            {
                Project p = new Project(new Engine());
                p.Save(String.Empty);
            }

            /// <summary>
            ///  Save Test, Path is null string
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void SaveToFileFileName_Null()
            {
                Project p = new Project(new Engine());
                string s = null;
                p.Save(s);
            }

            /// <summary>
            ///  Save Test, simple save, verify on disk.
            /// </summary>
            [Test]
            public void SaveToFile()
            {
                string savePath = String.Empty;
                try
                {
                    Project p = new Project(new Engine());
                    savePath = ObjectModelHelpers.TempProjectDir + "\\" + "temp.proj";
                    p.Save(savePath);
                    Project savedProject = new Project();
                    savedProject.Load(savePath);
                    ObjectModelHelpers.CompareProjectContents(p, savedProject.Xml);
                    Assertion.AssertEquals(false, p.IsDirty);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(savePath);
                }
            }

            /// <summary>
            ///  Save Test, FullFileName picks up the new file path after save.
            /// </summary>
            [Test]
            public void SaveToFileProjectFullFileNameUpdates()
            {
                string savePath = String.Empty;
                try
                {
                    Project p = new Project(new Engine());
                    p.FullFileName = ObjectModelHelpers.TempProjectDir + "\\" + "temp.proj";
                    savePath = ObjectModelHelpers.TempProjectDir + "\\" + "temp2.proj";
                    p.Save(savePath);
                    Assertion.AssertEquals(true, File.Exists(p.FullFileName));
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(savePath);
                }
            }

            /// <summary>
            ///  Save Test, a dirty project is clean afer a save.
            /// </summary>
            [Test]
            public void SaveToFileNotDirtyAfterSave()
            {
                string savePath = String.Empty;
                try
                {
                    Project p = new Project(new Engine());
                    p.Targets.AddNewTarget("newTarget");
                    savePath = ObjectModelHelpers.TempProjectDir + "\\" + "temp2.proj";
                    p.Save(savePath);
                    Assertion.AssertEquals(false, p.IsDirty);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(savePath);
                }
            }

            /// <summary>
            ///  Save Test, using an invalid filename.
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void SaveInvalidFileName()
            {
                string savePath = String.Empty;
                try
                {
                    Project p = new Project(new Engine());
                    savePath = ObjectModelHelpers.TempProjectDir + "\\" + "invalid|.proj";
                    p.Save(savePath);
                    CompatibilityTestHelpers.RemoveFile(savePath);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(savePath);
                }
            }

            /// <summary>
            /// Save Test, With Path length over 256 Characters
            /// </summary>
            /// <remarks>
            /// The file refereced in this test does not exist, as it cannot be created in
            /// the file system. 
            /// </remarks>
            [Test]
            [ExpectedException(typeof(PathTooLongException))]
            public void SaveValidFileName_OverMaxPath()
            {
                Project p = new Project(new Engine());
                p.Save(CompatibilityTestHelpers.GenerateLongPath(256) + "doesNotExist.proj"); // doesNotExist.proj does not exist
            }

            /// <summary>
            ///  Save Test, using valid Encoding
            /// </summary>
            [Test]
            public void SaveWithEncoding()
            {
                string savePath = String.Empty;
                try
                {
                    Project p = new Project(new Engine());
                    p.LoadXml(TestData.ContentSimpleUTF16);
                    savePath = ObjectModelHelpers.TempProjectDir + "\\" + "temp.proj";
                    p.Save(savePath);
                    p.Load(savePath);
                    Assertion.AssertEquals(Encoding.Unicode, p.Encoding);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(savePath);
                }
            }

            /// <summary>
            ///  Save Test, overloading the encoding with new valid encoding
            /// </summary>
            [Test]
            public void SaveWithOverloadedEncoding()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentSimpleUTF16);
                string savePath = ObjectModelHelpers.TempProjectDir + "\\" + "temp.proj";
                p.Save(savePath, Encoding.UTF8);
                p.Load(savePath);
                Assertion.AssertEquals(Encoding.UTF8, p.Encoding);
                CompatibilityTestHelpers.RemoveFile(savePath);
            }

            /// <summary>
            ///  Save Test, using Invalid Encoding
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void SaveWithInvalidEncoding()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentSimpleInvalidEncoding);
                string savePath = ObjectModelHelpers.TempProjectDir + "\\" + "temp.proj";
                p.Save(savePath);
                CompatibilityTestHelpers.RemoveFile(savePath);
            }

            /// <summary>
            ///  Save Test, using valid XmlWriter
            /// </summary>
            [Test]
            public void SaveXmlWriter_Valid()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentSimpleTools35);
                StringBuilder stringBuilder = new StringBuilder();
                StringWriter stringReader = new StringWriter(stringBuilder);
                p.Save(stringReader);
                ObjectModelHelpers.CompareProjectContents(p, stringReader.ToString());
            }

            /// <summary>
            ///  Save Test, using invalid stringWriter
            /// </summary>
            [Test]
            [ExpectedException(typeof(NullReferenceException))]
            public void SaveXmlWriter_Null()
            {
                Project p = new Project(new Engine());
                p.LoadXml(TestData.ContentSimpleTools35);
                StringWriter stringWriter = null;
                p.Save(stringWriter);
            }
        }

        /// <summary>
        /// Tests for the xml property.
        /// </summary>
        [TestFixture]
        public sealed class Xml
        {
            /// <summary>
            ///  Xml Test, get Xml after load
            /// </summary>
            [Test]
            public void Xml_Get()
            {
                Project p = new Project();
                p.LoadXml(TestData.ContentSimpleTools35);
                ObjectModelHelpers.CompareProjectContents(p, TestData.ContentSimpleTools35); // Asserts in here
            }

            /// <summary>
            ///  Xml Test, get Xml after change to OM
            /// </summary>
            [Test]
            public void Xml_GetAfterOMChange()
            {
                Project p = new Project();
                p.LoadXml(TestData.ContentSimpleTools35);
                p.Targets.AddNewTarget("newTarget");
                Assertion.AssertEquals(true, p.Xml.Contains("newTarget"));
            }
        }

        /// <summary>
        /// Tests for the DefaultTargets Property. 
        /// </summary>
        [TestFixture]
        public sealed class DefaultTargets
        {
            /// <summary>
            /// DefaultTargets Test, Set and Get through OM
            /// </summary>
            [Test]
            public void DefaultTargets_SetGetTarget()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified);
                p.DefaultTargets = "TestTarget1";
                p.Build();
                Assertion.AssertEquals("TestTarget1", p.DefaultTargets);
            }

            /// <summary>
            /// DefaultTargets Test, Get from OM, where set from property on project xml element.
            /// </summary>
            [Test]
            public void DefaultTargetsGetWhenSetInXml()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified);
                Assertion.AssertEquals("TestTargetDefault", p.DefaultTargets);
            }

            /// <summary>
            ///  DefaultTargets Test, Get in XMl where set in OM
            /// </summary>
            [Test]
            public void DefaultTargetsSetInOMGetInXml()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified);
                p.DefaultTargets = "DefaultTargetName";
                XmlDocument xdoc = new XmlDocument();
                xdoc.LoadXml(p.Xml);
                Assertion.AssertEquals("DefaultTargetName", xdoc.DocumentElement.GetAttribute("DefaultTargets"));
            }

            /// <summary>
            /// DefaultTargets Test, Get from OM, where set from property on project xml element.
            /// </summary>
            [Test]
            public void DefaultTargetsGetWhenNotExplicitySetInXml()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified);
                Assertion.AssertEquals(String.Empty, p.DefaultTargets);
            }

            /// <summary>
            /// DefaultTargets Test, Set where defaultTargets contains a target that does not exist. 
            /// </summary>
            [Test]
            public void DefaultTargetsSetMissingTarget()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                p.DefaultTargets = "missingTarget";
                p.Build();
                Assertion.AssertEquals(true, logger.FullLog.Contains("MSB4057")); // error MSB4057, the target does not exist 
            }

            /// <summary>
            /// DefaultTargets Test, where defaultTargets list contain null targets. 
            /// </summary>
            [Test]
            public void DefaultTargetsSetNullItemsInTargetList()
            {
                MockLogger logger = new MockLogger();
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                p.DefaultTargets = ";;Target1";
                p.Build();
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target 1"));
            }

            /// <summary>
            /// DefaultTargets Test, where value contains escaped delimters
            /// </summary>
            [Test]
            public void DefaultTargetsSpecialCharacterDelimiter()
            {
                // Setup
                MockLogger logger = new MockLogger();

                // Execute
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                p.DefaultTargets = "TestTarget%3bTestTarget1";
                p.Build();

                // Test
                Assertion.AssertEquals(true, logger.FullLog.Contains("MSB4057")); // error MSB4057, the target "TestTarget;TestTarget1" does not exist 
            }

            /// <summary>
            /// DefaultTargets Test, set to an invalid target name
            /// </summary>
            [Test]
            public void DefaultTargetsSpecialCharactersInTargetName()
            {
                // Execute
                Project p = new Project(new Engine());
                p.DefaultTargets = "valid@target;$\\valid;%valid()";
                Assertion.AssertEquals("valid@target; $\\valid; %valid()", p.DefaultTargets); // note whitespace 
            }

            /// <summary>
            /// DefaultTargets Test, where value contains empty items and 
            /// excess whitespace around target names
            /// </summary>
            [Test]
            public void DefaultTargetsExcessWhitespace()
            {
                // Setup
                MockLogger logger = new MockLogger();

                // Execute
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified, logger);
                p.DefaultTargets = "; ; ; Target1  ";
                p.Build();

                // Test
                Assertion.AssertEquals(true, logger.FullLog.Contains("Executed Target 1"));
            }

            /// <summary>
            /// DefaultTargets Test, set to null
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void DefaultTargetsSetNullString()
            {
                // Execute
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentSimpleTools35);
                p.DefaultTargets = null;
            }

            /// <summary>
            /// DefaultTargets Test, project should be flagged as ditry when default targets are set
            /// </summary>
            [Test]
            public void DefaultTargets_IsDirtyWhenSet()
            {
                // Execute
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentSimpleTools35);
                p.DefaultTargets = "newTarget";
                Assertion.AssertEquals(true, p.IsDirty);
            }
        }
        
        /// <summary>
        /// Tests for the FullfileName Property.
        /// </summary>
        [TestFixture]
        public sealed class FullFileName
        {
            /// <summary>
            ///  FullFileName Test, set then get 
            /// </summary>
            [Test]
            public void FullFileNameSetGet()
            {
                Project p = new Project(new Engine());
                p.FullFileName = "newname.proj";
                Assertion.AssertEquals(p.FullFileName, "newname.proj");
            }

            /// <summary>
            ///  FullFileName Test, assert is empty when in memory
            /// </summary>
            [Test]
            public void FullFileNameEmptyWhenConstructed()
            {
                Project p = new Project(new Engine());
                Assertion.AssertEquals(String.Empty, p.FullFileName);
            }

            /// <summary>
            ///  FullFileName Test, get when set on load
            /// </summary>
            [Test]
            public void FullFileNameGetWhenLoaded()
            {
                string path = CompatibilityTestHelpers.CreateTempProjectFile(TestData.Content3SimpleTargetsDefaultSpecified);
                Project p = new Project(new Engine());
                p.Load(path);
                Assertion.AssertEquals(path, p.FullFileName);
            }

            /// <summary>
            ///  FullFileName Test, not dirty after set
            /// </summary>
            [Test]
            public void FullFileNameSetProjectIsNotDirtyAfter()
            {
                Project p = new Project(new Engine());
                p.FullFileName = "newname.proj";
                Assertion.AssertEquals(false, p.IsDirty);
            }
        }

        /// <summary>
        ///  Tests for the Intial Targets Property.
        /// </summary>
        [TestFixture]
        public sealed class InitialTargets
        {
            /// <summary>
            ///  InitialTargets Test, set then get 
            /// </summary>
            [Test]
            public void InitialTargetsSetGet()
            {
                Project p = new Project(new Engine());
                p.InitialTargets = "testTarget";
                Assertion.AssertEquals("testTarget", p.InitialTargets);
            }

            /// <summary>
            ///  InitialTargets Test, set then get 
            /// </summary>
            [Test]
            public void InitialTargetsSetGetMultiple()
            {
                Project p = new Project(new Engine());
                p.InitialTargets = "testTarget; testTarget2";
                Assertion.AssertEquals("testTarget; testTarget2", p.InitialTargets);
            }

            /// <summary>
            ///  InitialTargets Test, 
            /// </summary>
            [Test]
            public void InitialTargetsSetGetMultipleSpecialcharacters()
            {
                Project p = new Project(new Engine());
                p.InitialTargets = "@testTarget; %3btestTarget2";
                Assertion.AssertEquals("@testTarget; %3btestTarget2", p.InitialTargets);
            }

            /// <summary>
            ///  InitialTargets Test,
            /// </summary>
            [Test]
            public void InitialTargetsGetWhenSetInXml()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentSimpleTools35InitialTargets);
                Assertion.AssertEquals(true, p.InitialTargets.Contains("InitialTarget"));
            }

            /// <summary>
            ///  InitialTargets Test,
            /// </summary>
            [Test]
            public void InitialTargetsIsDirtyWhenSet()
            {
                Project p = new Project(new Engine());
                p.InitialTargets = "testTarget";
                Assertion.AssertEquals(true, p.IsDirty);
            }

            /// <summary>
            ///  InitialTargets Test,
            /// </summary>
            [Test]
            public void InitialTargetsSetInOMGetInXml()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentSimpleTools35);
                p.InitialTargets = "InitialTarget";
                XmlDocument xdoc = new XmlDocument();
                xdoc.LoadXml(p.Xml);
                Assertion.AssertEquals("InitialTarget", xdoc.DocumentElement.GetAttribute("InitialTargets"));
            }
        }

        /// <summary>
        /// Tests for SetBuildProperty, GetConditionedPropertyValues, Group manipulation
        /// </summary>
        [TestFixture]
        public sealed class Properties
        {
            /// <summary>
            ///  RemoveAllPropertyGroups Test, check removal of groups defined in xml, imports and the om.
            /// </summary>
            [Test]
            public void RemoveAllPropertyGroups()
            {
                string importedProjFilePath = String.Empty;
                try
                {
                    Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified);
                    importedProjFilePath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    p.AddNewImport(importedProjFilePath, "true");
                    p.AddNewPropertyGroup(true);
                    p.AddNewPropertyGroup(true);
                    object o = p.EvaluatedItems;
                    Assertion.AssertEquals(3, p.PropertyGroups.Count);
                    p.RemoveAllPropertyGroups();
                    Assertion.AssertEquals(1, p.PropertyGroups.Count); // import remains
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilePath);
                }
            }

            /// <summary>
            ///  RemoveImportedPropertyGroup Test, remove on wrong project
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidOperationException))]
            public void RemoveImportedPropertyGroupInvalidOp()
            {
                string importedProjFilename = String.Empty;
                string mainProjFilename = String.Empty;
                try
                {
                    importedProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    Project mainProject = new Project(new Engine());                                         
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.Load(mainProjFilename);
                    importedProject.Load(importedProjFilename);
                    BuildPropertyGroup removalGroup = importedProject.AddNewPropertyGroup(true);
                    mainProject.RemoveImportedPropertyGroup(removalGroup);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilename);
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            ///  RemoveImportedPropertyGroup Test, remove on correct project
            /// </summary>
            [Test]
            public void RemoveImportedPropertyGroup()
            {
                string importedProjFilename = String.Empty;
                string mainProjFilename = String.Empty;
                try
                {
                    importedProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    Project mainProject = new Project(new Engine());
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.SetImportedProperty("property", "value", "true", importedProject);
                    mainProject.Load(mainProjFilename);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilename);
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            ///  RemovePropertyGroup Test, typical remove
            /// </summary>
            [Test]
            public void RemovePropertyGroup()
            {
                string mainProjFilename = String.Empty;
                try
                {
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    Project mainProject = new Project(new Engine());
                    BuildPropertyGroup groupToRemove = mainProject.AddNewPropertyGroup(true);
                    Assertion.AssertEquals(1, mainProject.PropertyGroups.Count);
                    mainProject.RemovePropertyGroup(groupToRemove);
                    Assertion.AssertEquals(0, mainProject.PropertyGroups.Count);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            ///  RemovePropertyGroup Test, remove a null group
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void RemovePropertyGroup_null()
            {
               Project mainProject = new Project(new Engine());
               BuildPropertyGroup groupToRemove = null;
               mainProject.RemovePropertyGroup(groupToRemove);
            }

            /// <summary>
            /// GetConditionedPropertyValues Test, where key is null.
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void GetConditionedPropertyValues_Null()
            {
                Project p = new Project();
                string[] values = p.GetConditionedPropertyValues(null);
            }
        
            /// <summary>
            /// Test the RemoveAllPropertyGroups method.
            /// </summary>
            /// <owner>RGoel</owner>
            [Test]
            public void RemoveAllPropertyGroupsWithChoose()
            {
                // ************************************
                //               BEFORE
                // ************************************
                string original = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <PropertyGroup Condition=`'$(x)'=='y'`>
                        <ReferencePath>c:\foobar</ReferencePath>
                    </PropertyGroup>

                    <Choose>
                        <When Condition = `true`>
                            <PropertyGroup Condition=`'$(x)'=='z'`>
                                <ReferencePath>c:\foobar</ReferencePath>
                            </PropertyGroup>
                        </When>
                        <Otherwise>
                            <PropertyGroup Condition=`'$(x)'=='v'`>
                                <ReferencePath>c:\foobar</ReferencePath>
                            </PropertyGroup>
                        </Otherwise>
                    </Choose>

                </Project>
                ";

                // ************************************
                //               AFTER
                // ************************************
                string expected = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <Choose>
                        <When Condition=`true`>
                        </When>
                        <Otherwise>
                        </Otherwise>
                    </Choose>

                </Project>
                ";

                Project project = ObjectModelHelpers.CreateInMemoryProject(original);
                Assertion.AssertEquals(3, project.PropertyGroups.Count);

                project.RemoveAllPropertyGroups();

                Assertion.AssertEquals(0, project.PropertyGroups.Count);
                ObjectModelHelpers.CompareProjectContents(project, expected);
            }

            /// <summary>
            /// Test the RemoveAllPropertyGroupsByCondition method.
            /// </summary>
            /// <owner>RGoel</owner>
            [Test]
            public void RemoveAllPropertyGroupsByConditionWithChoose()
            {
                // ************************************
                //               BEFORE
                // ************************************
                string original = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <PropertyGroup Condition=`'$(x)'=='y'`>
                        <ReferencePath>c:\foobar</ReferencePath>
                    </PropertyGroup>

                    <PropertyGroup Condition=`'$(x)'=='z'`>
                        <ReferencePath>c:\foobar</ReferencePath>
                    </PropertyGroup>

                    <Choose>
                        <When Condition = `true`>
                            <PropertyGroup Condition=`'$(x)'=='y'`>
                                  <ReferencePath>c:\foobar</ReferencePath>
                            </PropertyGroup>
  
                            <PropertyGroup Condition=`'$(x)'=='z'`>
                                  <ReferencePath>c:\foobar</ReferencePath>
                            </PropertyGroup>
                        </When>
                        <Otherwise>
                            <PropertyGroup Condition=`'$(x)'=='y'`>
                                  <ReferencePath>c:\foobar</ReferencePath>
                            </PropertyGroup>
  
                            <PropertyGroup Condition=`'$(x)'=='z'`>
                                  <ReferencePath>c:\foobar</ReferencePath>
                            </PropertyGroup>
                        </Otherwise>
                    </Choose>

                </Project>
                ";

                // ************************************
                //               AFTER
                // ************************************
                string expected = @"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <PropertyGroup Condition=`'$(x)'=='z'`>
                        <ReferencePath>c:\foobar</ReferencePath>
                    </PropertyGroup>

                    <Choose>
                        <When Condition=`true`>
                            <PropertyGroup Condition=`'$(x)'=='z'`>
                                <ReferencePath>c:\foobar</ReferencePath>
                            </PropertyGroup>
                        </When>
                        <Otherwise>
                            <PropertyGroup Condition=`'$(x)'=='z'`>
                                <ReferencePath>c:\foobar</ReferencePath>
                            </PropertyGroup>
                        </Otherwise>
                    </Choose>

                </Project>
                ";

                Project project = ObjectModelHelpers.CreateInMemoryProject(original);
                Assertion.AssertEquals(6, project.PropertyGroups.Count);

                project.RemovePropertyGroupsWithMatchingCondition("'$(x)'=='y'");

                Assertion.AssertEquals(3, project.PropertyGroups.Count);
                ObjectModelHelpers.CompareProjectContents(project, expected);
            }

            /// <summary>
            ///  RemovePropertyGroupsWithMatchingCondition, where condition matches imported group
            ///  and is not removed.
            /// </summary>
            [Test]
            public void RemovePropertyGroupsWithMatchingConditionMatchInImported_False()
            {
                string importedProjFilename = String.Empty;
                string mainProjFilename = String.Empty;
                try
                {
                    importedProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    Project mainProject = new Project(new Engine());
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.Load(mainProjFilename);
                    importedProject.Load(importedProjFilename);
                    Assertion.AssertEquals(0, mainProject.PropertyGroups.Count);
                    mainProject.RemovePropertyGroupsWithMatchingCondition("true");
                    Assertion.AssertEquals(0, mainProject.PropertyGroups.Count);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilename);
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            ///  RemovePropertyGroupsWithMatchingCondition, where condition matches imported group
            ///  and is not removed.
            /// </summary>
            [Test]
            public void RemovePropertyGroupsWithMatchingConditionMatchInImported_True()
            {
                string importedProjFilename = String.Empty;
                string mainProjFilename = String.Empty;
                try
                {
                    importedProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    Project mainProject = new Project(new Engine());
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.Load(mainProjFilename);
                    importedProject.Load(importedProjFilename);
                    BuildPropertyGroup referenceGroup = mainProject.AddNewPropertyGroup(true);
                    referenceGroup.Condition = "true";
                    mainProject.SetImportedProperty("newp", "newv", "true", importedProject);
                    Assertion.AssertEquals(2, mainProject.PropertyGroups.Count);
                    mainProject.RemovePropertyGroupsWithMatchingCondition("true", true);
                    Assertion.AssertEquals(0, mainProject.PropertyGroups.Count);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilename);
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            ///  RemovePropertyGroupsWithMatchingCondition, where condition matches a lcoal
            ///  group and can be removed
            /// </summary>
            [Test]
            public void RemovePropertyGroupsWithMatchingCondition_MatchInProject()
            {
                string mainProjFilename = String.Empty;
                try
                {
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    Project mainProject = new Project(new Engine());
                    mainProject.Load(mainProjFilename);
                    Assertion.AssertEquals(1, mainProject.PropertyGroups.Count);
                    mainProject.RemovePropertyGroupsWithMatchingCondition("true");
                    Assertion.AssertEquals(0, mainProject.PropertyGroups.Count);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            /// GetConditionedPropertyValues Test, where key is empty string
            /// </summary>
            [Test]
            public void GetConditionedPropertyValues_EmptyString()
            {
                Project p = new Project();
                string[] values = p.GetConditionedPropertyValues(String.Empty);
                Assertion.AssertEquals(0, values.Length);
            }

            /// <summary>
            /// GetConditionedPropertyValues Test, where key does not exists
            /// </summary>
            [Test]
            public void GetConditionedPropertyValues_Missing()
            {
                Project p = new Project();
                string[] values = p.GetConditionedPropertyValues("doesnotExist");
                Assertion.AssertEquals(0, values.Length);
            }

            /// <summary>
            /// SetProperty Test, add a property that does not exist
            /// </summary>
            [Test]
            public void SetPropertyAddingNew()
            {
                Project p = new Project();
                p.SetProperty("property_name", "v");
                p.SetProperty("property_name2", "v2");
                object o = p.EvaluatedItems;
                Assertion.AssertEquals("v", p.GetEvaluatedProperty("property_name"));
                Assertion.AssertEquals("v2", p.GetEvaluatedProperty("property_name2"));
                Assertion.AssertEquals(true, p.Xml.Contains("property_name"));
                Assertion.AssertEquals(true, p.Xml.Contains("property_name2"));
            }

            /// <summary>
            /// SetProperty Test, test that the project is dirty after set
            /// </summary>
            [Test]
            public void SetPropertyDirtyAfterSet()
            {
                Project p = new Project();
                p.SetProperty("property_name", "v");
                Assertion.AssertEquals(true, p.IsDirty);
            }

            /// <summary>
            /// SetProperty Test, test that property is set in a new group before th import element
            /// </summary>
            [Test]
            public void SetPropertyBeforeImportDoesNotExists()
            {
                string importPath = String.Empty;
                try
                {
                    Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified);
                    importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentCreatePropertyTarget);
                    p.AddNewImport(importPath, "true");
                    p.SetProperty("n", "vNew", null, PropertyPosition.UseExistingOrCreateAfterLastPropertyGroup);
                    object o = p.EvaluatedItems;
                    Assertion.AssertEquals(true, p.Xml.IndexOf("vNew") < p.Xml.IndexOf("Import"));
                }
                finally 
                {
                    CompatibilityTestHelpers.RemoveFile(importPath);
                }
            }

            /// <summary>
            /// SetProperty Test, test that the project is dirty after set
            /// </summary>
            [Test]
            public void SetPropertyAfterImportDoesNotExists()
            {
                string importPath = String.Empty;
                try
                {
                    Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsNoDefaultSpecified);
                    importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentCreatePropertyTarget);
                    p.AddNewImport(importPath, "true");
                    p.SetProperty("n", "vNew", null, PropertyPosition.UseExistingOrCreateAfterLastImport);
                    object o = p.EvaluatedItems;
                    Assertion.AssertEquals(true, p.Xml.IndexOf("vNew") > p.Xml.IndexOf("Import"));
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importPath);
                }
            }

            /// <summary>
            /// SetProperty Test, test existing items before will be used ahead of createing a new one after an import
            /// </summary>
            [Test]
            public void SetPropertyAfterImportWherePropertyExistsBefore()
            {
                string importPath = String.Empty;
                try
                {
                    Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentCreatePropertyTarget);
                    importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentImport1);
                    p.AddNewImport(importPath, "true");
                    p.SetProperty("n", "vNew", null, PropertyPosition.UseExistingOrCreateAfterLastImport);
                    object o = p.EvaluatedItems;
                    Assertion.AssertEquals(true, p.Xml.IndexOf("vNew") < p.Xml.IndexOf("Import"));
                }
                finally 
                {
                    CompatibilityTestHelpers.RemoveFile(importPath);
                }
            }

            /// <summary>
            /// SetProperty Test, set when codition is null
            /// </summary>
            [Test]
            public void SetPropertyCondition_Null()
            {
                Project p = new Project();
                p.SetProperty("n", "v", null);
                Assertion.AssertEquals("v", p.GetEvaluatedProperty("n"));
            }

            /// <summary>
            /// SetProperty Test, set when codition is String.Empty
            /// </summary>
            [Test]
            public void SetPropertyCondition_Empty()
            {
                Project p = new Project();
                p.SetProperty("n", "v", String.Empty);
                Assertion.AssertEquals("v", p.GetEvaluatedProperty("n"));
            }

            /// <summary>
            /// SetProperty Test, set when codition evaluates to true
            /// </summary>
            [Test]
            public void SetPropertyCondition_True()
            {
                Project p = new Project();
                p.SetProperty("n", "v", "1 == 1");
                Assertion.AssertEquals("v", p.GetEvaluatedProperty("n"));
            }

            /// <summary>
            /// SetProperty Test, set when codition evaluates to false
            /// </summary>
            [Test]
            public void SetPropertyCondition_False()
            {
                Project p = new Project();
                p.SetProperty("n", "v", "false");
                Assertion.AssertNull(p.GetEvaluatedProperty("n"));
            }

            /// <summary>
            /// SetProperty Test, set to a scalar variable
            /// </summary>
            [Test]
            public void SetPropertyTreatAsScalar()
            {
                Project p = new Project();
                ////p.SetProperty("$n", "v", null, null, true);
            }

            /// <summary>
            /// SetProperty Test, set a property that exists
            /// </summary>
            [Test]
            public void SetPropertySetExisting()
            {
                Project p = new Project();
                p.SetProperty("property_name", "v");
                p.SetProperty("property_name", "v2");
                Assertion.AssertEquals("v2", p.GetEvaluatedProperty("property_name"));
                Assertion.AssertEquals(true, p.Xml.Contains("property_name"));
                Assertion.AssertEquals(true, p.IsDirty);
            }

            /// <summary>
            /// SetProperty Test, null name
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void SetPropertySetName_Null()
            {
                Project p = new Project();
                string name = null;
                p.SetProperty(name, "v");
            }

            /// <summary>
            /// SetProperty Test, null name
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void SetPropertySetName_Empty()
            {
                Project p = new Project();
                p.SetProperty(String.Empty, "v");
            }

            /// <summary>
            /// SetProperty Test, null value
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void SetPropertySet_ValueNull()
            {
                Project p = new Project();
                string value = null;
                p.SetProperty("n", value);
            }

            /// <summary>
            /// SetProperty Test, empty value
            /// </summary>
            [Test]
            public void SetPropertySetValue_Empty()
            {
                Project p = new Project();
                p.SetProperty("n", String.Empty);
            }

            /// <summary>
            /// SetProperty Test, with literal and escaped characters
            /// </summary>
            [Test]
            public void SetPropertySetValue_literals()
            {
                Project p = new Project();
                p.SetProperty("literalFalse", @"%25%2a%3f%40%24%28%29%3b\", "true", PropertyPosition.UseExistingOrCreateAfterLastImport, false);
                p.SetProperty("literalTrue", @"%25%2a%3f%40%24%28%29%3b\", "true", PropertyPosition.UseExistingOrCreateAfterLastImport, true);
                p.SetProperty("nonLiteralFalse", @"%*?@$();\", "true", PropertyPosition.UseExistingOrCreateAfterLastImport, false);
                p.SetProperty("nonLiteralTrue", @"%*?@$();\", "true", PropertyPosition.UseExistingOrCreateAfterLastImport, true);
                Assertion.AssertEquals(@"%*?@$();\", p.GetEvaluatedProperty("literalFalse"));
                Assertion.AssertEquals(@"%*?@$();\", p.GetEvaluatedProperty("nonLiteralTrue"));
                Assertion.AssertEquals(@"%25%2a%3f%40%24%28%29%3b\", p.GetEvaluatedProperty("literalTrue"));
                Assertion.AssertEquals(@"%*?@;\", p.GetEvaluatedProperty("nonLiteralFalse"));
            }

            /// <summary>
            /// AddNewPropertyGroup Test, check flags and addition.
            /// </summary>
            [Test]
            public void AddNewPropertyGroup()
            {
                Project p = new Project();
                BuildPropertyGroup buildPropertyGroup = p.AddNewPropertyGroup(true);
                buildPropertyGroup.AddNewProperty("n", "v");
                buildPropertyGroup.Condition = "true";
                Assertion.AssertEquals(true, p.Xml.Contains("<PropertyGroup Condition=\"true\">"));
                Assertion.AssertEquals(1, p.PropertyGroups.Count);
                Assertion.AssertEquals(true, p.IsDirty);
            }

            /// <summary>
            /// AddNewProperty, where group is not persisted.
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidOperationException))]
            public void AddNewProperty_InvalidOp()
            {
                BuildPropertyGroup buildPropertyGroup = new BuildPropertyGroup();
                buildPropertyGroup.AddNewProperty("n", "v");
            }

            /// <summary>
            /// AddNewPropertyGroup Test, before other groups
            /// 
            /// If "insertAtEnd", is inserted at the very end. Otherwise,
            /// we add the new property group just after the last property group in the
            /// main project file.  If there are currently no property groups in the main
            /// project file, we add this one to the very beginning of the project file.
            /// </summary>
            [Test]
            public void AddNewPropertyGroupBeforeWithOneOtherPropertyGroup()
            {
                Project p = new Project();
                BuildPropertyGroup buildPropertyGroup1 = p.AddNewPropertyGroup(true);
                buildPropertyGroup1.Condition = "true";
                BuildPropertyGroup buildPropertyGroup2 = p.AddNewPropertyGroup(false);
                buildPropertyGroup2.Condition = "false";
                Assertion.AssertEquals(true, p.Xml.IndexOf("<PropertyGroup Condition=\"true\" />") < p.Xml.IndexOf("<PropertyGroup Condition=\"false\" />"));
                Assertion.AssertEquals(2, p.PropertyGroups.Count);
            }

            /// <summary>
            /// AddNewPropertyGroup Test, before other groups
            /// 
            /// If "insertAtEnd", is inserted at the very end. Otherwise,
            /// we add the new property group just after the last property group in the
            /// main project file.  If there are currently no property groups in the main
            /// project file, we add this one to the very beginning of the project file.
            /// </summary>
            [Test]
            public void AddNewPropertyGroupBeforeOneOtherPropertyGroupAndAUsingTask()
            {
                Project p = new Project();
                BuildPropertyGroup buildPropertyGroup1 = p.AddNewPropertyGroup(true);
                buildPropertyGroup1.Condition = "true";
                p.AddNewImport("p", "c");
                BuildPropertyGroup buildPropertyGroup2 = p.AddNewPropertyGroup(false);
                buildPropertyGroup2.Condition = "false";
                Assertion.AssertEquals(true, p.Xml.IndexOf("<PropertyGroup Condition=\"true\" />") < p.Xml.IndexOf("<PropertyGroup Condition=\"false\" />"));
                Assertion.AssertEquals(true, p.Xml.IndexOf("<PropertyGroup Condition=\"true\" />") < p.Xml.IndexOf("<Import Condition=\"c\" Project=\"p\" />"));
                Assertion.AssertEquals(2, p.PropertyGroups.Count);
            }

            /// <summary>
            /// AddNewPropertyGroup Test, add after other groups
            /// </summary>
            [Test]
            public void AddNewPropertyGroupAfter()
            {
                Project p = new Project();
                BuildPropertyGroup buildPropertyGroup1 = p.AddNewPropertyGroup(false);
                buildPropertyGroup1.Condition = "true";
                BuildPropertyGroup buildPropertyGroup2 = p.AddNewPropertyGroup(true);
                buildPropertyGroup2.Condition = "false";
                Assertion.AssertEquals(true, p.Xml.IndexOf("<PropertyGroup Condition=\"true\" />") < p.Xml.IndexOf("<PropertyGroup Condition=\"false\" />"));
                Assertion.AssertEquals(2, p.PropertyGroups.Count);
                Assertion.AssertEquals(true, p.IsDirty);
            }

            /// <summary>
            /// AddNewPropertyGroup Test, not after, no existing property groups.
            /// Should go at very beginning.
            /// </summary>
            [Test]
            public void AddNewPropertyGroupNotAFter()
            {
                Project p = new Project();
                p.AddNewImport("p", "c");
                BuildPropertyGroup buildPropertyGroup1 = p.AddNewPropertyGroup(false);

                Assertion.AssertEquals(true, p.Xml.IndexOf("<PropertyGroup />") < p.Xml.IndexOf("<Import Condition=\"c\" Project=\"p\" />"));
            }

            /// <summary>
            /// GlobalProperties Test, Get collection
            /// </summary>
            [Test]
            public void GlobalPropertiesGet()
            {
                Project p = new Project();
                p.GlobalProperties.SetProperty("a", "b");

                Assertion.AssertEquals(1, p.GlobalProperties.Count);
            }

            /// <summary>
            /// GlobalProperties Test, set to null
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void GlobalPropertiesSet_null()
            {
                Project p = new Project();
                p.GlobalProperties = null;
            }

            /// <summary>
            /// AddNewPropertyGroup Test, ensure that object is cloned into the setter;
            /// </summary>
            [Test]
            public void GlobalPropertiesSet()
            {
                Project p = new Project();
                BuildPropertyGroup newBuildPropertyGroup = new BuildPropertyGroup();
                p.GlobalProperties = newBuildPropertyGroup;
                Assertion.AssertEquals(false, newBuildPropertyGroup.Equals(p.GlobalProperties));
            }

            /// <summary>
            /// PropertyGroups Test, get PropertyGroupsCollection
            /// </summary>
            [Test]
            public void PropertyGroupsGet()
            {
                Project p = new Project();
                p.LoadXml(TestData.PropertyGroup);
                p.SetProperty("n", "v");
                p.AddNewPropertyGroup(false);
                BuildPropertyGroupCollection buildPropertyGroups = p.PropertyGroups;
                Assertion.AssertEquals(3, buildPropertyGroups.Count);
                Assertion.AssertEquals(true, buildPropertyGroups.Equals(p.PropertyGroups));
            }
        }

        /// <summary>
        /// Tests for AddNewImport method, RemoveImport method, Imports property, SetImportedProperty
        /// </summary>
        [TestFixture]
        public sealed class Import : AddNewImportTests
        {
            /// <summary>
            /// Set the indirection for AddNewImport Tests
            /// </summary>
            public Import()
            {
                InvokeAddNewImportMethod = new AddNewImportDelegate(AddNewImportOverload);
            }
          
            /// <summary>
            ///  Import Test, Get Import 
            /// </summary>
            [Test]
            public void ImportsGet()
            {
                string importPath = String.Empty;
                try
                {
                    Project p = new Project(new Engine());
                    importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.ContentSimpleTools35InitialTargets);
                    p.AddNewImport(importPath, null);
                    object o = p.EvaluatedItems; // force evaluation of imported projects.
                    Assertion.AssertEquals(1, p.Imports.Count);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importPath);
                }
            }

            /// <summary>
            ///  SetImportedProperty Test
            /// </summary>
            [Test]
            public void SetImportedProperty()
            {
                string importPath = String.Empty;
                string mainProjectPath = String.Empty;
                try
                {
                    importPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    mainProjectPath = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);

                    Project mainProject = new Project(new Engine());
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.Load(mainProjectPath);
                    importedProject.Load(importPath);
                    mainProject.SetImportedProperty("p", "v", "1 == 1", importedProject);
                    Assertion.AssertEquals("v", mainProject.GetEvaluatedProperty("p"));
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importPath);
                    CompatibilityTestHelpers.RemoveFile(mainProjectPath);
                }
            }

            /// <summary>
            ///  SetImportedProperty set imported properties and track the
            ///  change through the import from the main project
            /// </summary>
            [Test]
            public void SetImportedPropertyThatExists()
            {
                string importedProjFilename = String.Empty;
                string mainProjFilename = String.Empty;
                try
                {
                    importedProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    Project mainProject = new Project(new Engine());
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.Load(mainProjFilename);
                    importedProject.Load(importedProjFilename);
                    Assertion.AssertEquals("v1", importedProject.GetEvaluatedProperty("n1"));
                    Assertion.AssertEquals(null, mainProject.GetEvaluatedProperty("n1"));
                    mainProject.SetImportedProperty("n1", "newV", "1 == 1", importedProject);
                    Assertion.AssertEquals("newV", mainProject.GetEvaluatedProperty("n1"));
                    Assertion.AssertEquals("newV", importedProject.GetEvaluatedProperty("n1"));
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilename);
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            /// SetProperty Test, with literal and escaped characters
            /// </summary>
            [Test]
            public void SetPropertySetValueLiteralFlag()
            { 
                string importedProjFilename = String.Empty;
                string mainProjFilename = String.Empty;
                try
                {
                    importedProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    Project mainProject = new Project(new Engine());
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.Load(mainProjFilename);
                    importedProject.Load(importedProjFilename);
                    mainProject.SetImportedProperty("n1", "newV", "true", importedProject, PropertyPosition.UseExistingOrCreateAfterLastImport, true);
                    mainProject.SetImportedProperty("literalFalse", @"%25%2a%3f%40%24%28%29%3b\", "true", importedProject, PropertyPosition.UseExistingOrCreateAfterLastImport, false);
                    mainProject.SetImportedProperty("literalTrue", @"%25%2a%3f%40%24%28%29%3b\", "true", importedProject, PropertyPosition.UseExistingOrCreateAfterLastImport, true);
                    mainProject.SetImportedProperty("nonLiteralFalse", @"%*?@$();\", "true", importedProject, PropertyPosition.UseExistingOrCreateAfterLastImport, false);
                    mainProject.SetImportedProperty("nonLiteralTrue", @"%*?@$();\", "true", importedProject, PropertyPosition.UseExistingOrCreateAfterLastImport, true);

                    Assertion.AssertEquals(@"%*?@$();\", mainProject.GetEvaluatedProperty("literalFalse"));
                    Assertion.AssertEquals(@"%*?@$();\", mainProject.GetEvaluatedProperty("nonLiteralTrue"));
                    Assertion.AssertEquals(@"%25%2a%3f%40%24%28%29%3b\", mainProject.GetEvaluatedProperty("literalTrue"));
                    Assertion.AssertEquals(@"%*?@;\", mainProject.GetEvaluatedProperty("nonLiteralFalse"));
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilename);
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            ///  SetImportedProperty Test,  null property name 
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void SetImportedPropertyName_Null()
            {
                string importedProjFilename = String.Empty;
                string mainProjFilename = String.Empty;
                try
                {
                    importedProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    Project mainProject = new Project(new Engine());
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.Load(mainProjFilename);
                    importedProject.Load(importedProjFilename);
                    mainProject.SetImportedProperty(null, "newV", "1 == 1", importedProject);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilename);
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            ///  SetImportedProperty Test, null property value
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void SetImportedPropertyValue_Null()
            {
                string importedProjFilename = String.Empty;
                string mainProjFilename = String.Empty;
                try
                {
                    importedProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    Project mainProject = new Project(new Engine());
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.Load(mainProjFilename);
                    importedProject.Load(importedProjFilename);
                    mainProject.SetImportedProperty("n1", null, "1 == 1", importedProject);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilename);
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            ///  SetImportedProperty Test, null condition
            /// </summary>
            [Test]
            public void SetImportedPropertyCondition_Null()
            {
                string importedProjFilename = String.Empty;
                string mainProjFilename = String.Empty;
                try
                {
                    importedProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.PropertyGroup);
                    mainProjFilename = ObjectModelHelpers.CreateTempFileOnDisk(TestData.Content3SimpleTargetsDefaultSpecified);
                    Project mainProject = new Project(new Engine());
                    Project importedProject = new Project(mainProject.ParentEngine);
                    mainProject.Load(mainProjFilename);
                    importedProject.Load(importedProjFilename);
                    mainProject.SetImportedProperty("n1", "newV", null, importedProject);
                    Assertion.AssertEquals("newV", importedProject.GetEvaluatedProperty("n1"));
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(importedProjFilename);
                    CompatibilityTestHelpers.RemoveFile(mainProjFilename);
                }
            }

            /// <summary>
            /// Indirection for common tests to p.AddNewImport
            /// </summary>
            private void AddNewImportOverload(Project p, string path, string condition)
            {
                p.AddNewImport(path, condition);
            }
        }

        /// <summary>
        /// Tests for ProjectExtensions
        /// </summary>
        [TestFixture]
        public sealed class ProjectExtensions 
        {
            /// <summary>
            /// GetProjectExtensions Test, where item id exists
            /// </summary>
            [Test]
            public void PGetProjectExtensionsExistingItem()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentExtensions);
                Assertion.AssertEquals("v1", p.GetProjectExtensions("id1"));
            }

            /// <summary>
            /// GetProjectExtensions where Extentions element is missing
            /// </summary>
            [Test]
            public void PGetProjectExtensionsWhenNoProjectExtensionsElement()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified);
                Assertion.AssertEquals(String.Empty, p.GetProjectExtensions("id1"));
            }

            /// <summary>
            /// GetProjectExtensions Test, where item id does not exist
            /// </summary>
            [Test]
            public void GetProjectExtensionsWhereElementofIdMissing()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.Content3SimpleTargetsDefaultSpecified);
                Assertion.AssertEquals(String.Empty, p.GetProjectExtensions("idMissing"));
            }

            /// <summary>
            /// GetProjectExtensions Test, check that namespace attributes are removed from nodes under an Id. 
            /// </summary>
            [Test]
            public void GetProjectExtensionsNodePurgeNamespace()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentExtensions);
                Assertion.AssertEquals(false, p.GetProjectExtensions("id3").Contains(CompatibilityTestHelpers.SchemaUrlMSBuild.ToString()));
            }

            /// <summary>
            /// SetProjectExtensions Test, where item id exists
            /// </summary>
            [Test]
            public void SetProjectExtensionsExistingItem_Valid()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentExtensions);
                p.SetProjectExtensions("id1", "vNew");
                Assertion.AssertEquals("vNew", p.GetProjectExtensions("id1"));
            }

            /// <summary>
            /// SetProjectExtensions Test, where ProjectExtensions node does not exist
            /// </summary>
            [Test]
            public void SetProjectExtensionsProjectExtensionMissing()
            {
                Project p = new Project();
                p.SetProjectExtensions("id1", "vNew");
                Assertion.AssertEquals("vNew", p.GetProjectExtensions("id1"));
                Assertion.AssertEquals(true, p.Xml.Contains("ProjectExtensions"));
            }

            /// <summary>
            /// SetProjectExtensions Test, where item id does not exist
            /// </summary>
            [Test]
            [ExpectedException(typeof(NullReferenceException))] // Ew
            public void SetProjectExtensionsNewItem_Null()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentExtensions);
                string nodeId = null;
                p.SetProjectExtensions(nodeId, "vNew");
            }
        }

        /// <summary>
        /// Tests for EvaluatedItems EvaluatedItemsIgnoringCondition
        /// </summary>
        [TestFixture]
        public sealed class EvaluatedItems 
        {
            /// <summary>
            /// EvaluatedItems, add an item, check addition to OM and xml
            /// </summary>
            [Test]
            public void EvaluatedItemsAdding()
            {
                Project p = new Project();
                p.AddNewItem("namedItem", "v");
                Assertion.AssertEquals(1, p.EvaluatedItems.Count);
                Assertion.AssertEquals("namedItem", p.EvaluatedItems[0].Name);
                Assertion.AssertEquals(true, p.Xml.Contains("namedItem"));
            }

            /// <summary>
            /// EvaluatedItems Test, add two items with same name
            /// </summary>
            [Test]
            public void EvaluatedItemsAddingTwice()
            {
                Project p = new Project();
                p.AddNewItem("n", "v");
                p.AddNewItem("n", "v2");
                Assertion.AssertEquals(2, p.EvaluatedItems.Count);
                Assertion.AssertEquals("v", p.EvaluatedItems[0].Include);
                Assertion.AssertEquals("v2", p.EvaluatedItems[1].Include);
            }

            /// <summary>
            /// EvaluatedItems, should return a cloned type, not a reference
            /// </summary>
            [Test]
            public void EvaluatedItemsClonedReturns()
            {
                Project p = new Project();
                p.AddNewItem("n", "v");
                BuildItemGroup group1 = p.EvaluatedItems;
                Assertion.AssertEquals(true, p.IsDirty);
                BuildItemGroup group2 = p.EvaluatedItems;
                Assertion.AssertEquals(false, Object.ReferenceEquals(group1, group2));
            }

            /// <summary>
            /// EvaluatedItems, Compare evaluated items when filtered on condition
            /// </summary>
            [Test]
            public void EvaluatedItemsIgnoreCondition()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup);
                Assertion.AssertEquals(3, p.EvaluatedItemsIgnoringCondition.Count);
                Assertion.AssertEquals(2, p.EvaluatedItems.Count);
            }
        }

        /// <summary>
        /// Tests for BuildItem Collection Groups and their manipulation
        /// </summary>
        [TestFixture]
        public sealed class BuildItems
        { 
            /// <summary>
            /// AddNewItem Test, pass in a a null item name
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void AddNewItemName_Null()
            {
                Project p = new Project();
                p.AddNewItem(null, "include");
            }

            /// <summary>
            /// AddNewItem Test, pass in an empty item name
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void AddNewItemName_Empty()
            {
                Project p = new Project();
                p.AddNewItem(String.Empty, "include");
            }

            /// <summary>
            /// AddNewItem Test, pass in an empty include value
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentException))]
            public void AddNewItemInclude_Empty()
            {
                Project p = new Project();
                p.AddNewItem("include", String.Empty);
            }

            /// <summary>
            /// AddNewItem Test, pass in a a null item name
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void AddNewItemInclude_Null()
            {
                Project p = new Project();
                p.AddNewItem("item", null);
            }

            /// <summary>
            /// AddNewItem Test, gets added to local project rather than imported
            /// </summary>
            [Test]
            public void AddNewItemPrecidence_Local()
            {
                Project mainProject = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup3);
                Project importedProject = new Project(mainProject.ParentEngine);
                importedProject.LoadXml(TestData.ItemGroup);
                mainProject.AddNewItem("newItem", "i");
                Assertion.AssertEquals(true, mainProject.Xml.IndexOf("newItem") > 0);
            }

            /// <summary>
            /// AddNewItem Test, gets added to last group with no condition that has items of the same type
            /// </summary>
            [Test]
            public void AddNewItemIncludePrecidece_NoCondition()
            {
                Project mainProject = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup3);
                XmlDocument xmldoc = new XmlDocument();
                xmldoc.LoadXml(mainProject.Xml);
                mainProject.AddNewItem("i", "x");

                Assertion.AssertEquals(true, CompatibilityTestHelpers.GetNodesWithName(mainProject.Xml, "ItemGroup")[2].InnerXml.Contains("Include=\"x\""));
            }

            /// <summary>
            /// AddNewItem Test, ensure item is added to first Item group with items of same type
            /// </summary>
            [Test]
            public void AddNewItemIncludePrecidece_SameTypes()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup2);
                p.AddNewItem("i2", "d");
                XmlDocument xmldoc = new XmlDocument();
                xmldoc.LoadXml(p.Xml);
                Assertion.AssertEquals(true, CompatibilityTestHelpers.GetNodesWithName(p.Xml, "ItemGroup")[1].InnerXml.Contains("Include=\"d\"")); 
            }

            /// <summary>
            /// AddNewItem Test, doesn't match anything that exists so create a new group
            /// </summary>
            [Test]
            public void AddNewItemIncludePrecidece_NewGroup()
            {       
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup2);
                XmlDocument xmldoc = new XmlDocument();
                xmldoc.LoadXml(p.Xml);
                Assertion.AssertEquals(3, p.ItemGroups.Count);
                p.AddNewItem("unique", "d");
                Assertion.AssertEquals(4, p.ItemGroups.Count);
            }

            /// <summary>
            /// AddNewItem Test, doesn't match anything that exists so create a new group
            /// </summary>
            [Test]
            public void AddNewItemEscaping()
            {
                Project p = new Project();
                string escaped = @"%25%2a%3f%40%24%28%29%3b\";
                string unescaped = @"%*?@$();\";
                BuildItem buildItem1 = p.AddNewItem("escapedTrue", escaped, true);
                BuildItem buildItem2 = p.AddNewItem("escapedFalse", escaped, false);
                BuildItem buildItem3 = p.AddNewItem("unescapedTrue", unescaped, true);
                BuildItem buildItem4 = p.AddNewItem("unescapedFalse", unescaped, false);

                Assertion.AssertEquals(escaped, buildItem1.FinalItemSpec);
                Assertion.AssertEquals(unescaped, buildItem2.FinalItemSpec);
                Assertion.AssertEquals(unescaped, buildItem3.FinalItemSpec);
                Assertion.AssertEquals(@"\", buildItem4.FinalItemSpec);
            }

            /// <summary>
            /// Tests BuildItemGroup.RemoveItem from an Evaluated and Expanded Group. Wildcard With a preceeding call to EvaluatedItems
            /// </summary>
            /// <bug>Regression for bug:170974</bug>
            /// <remarks>
            /// This method asserts broken behaviour
            /// Should remove the evaluated item foo.foo. As *.foo has been expanded, the persisted item should be foo1.foo.
            /// </remarks>
            [Test]
            public void RemoveEvaluatedItemAfterExpansionFails()
            {
                try
                {
                    CompatibilityTestHelpers.CreateFiles(2, "foo", "foo", ObjectModelHelpers.TempProjectDir);
                    Project p = new Project();
                    object o = p.EvaluatedItems; // this causes failure
                    p.AddNewItem("foos", Path.Combine(ObjectModelHelpers.TempProjectDir, "*.foo"));
                    p.RemoveItem(p.EvaluatedItems[0]);   // Exception thrown here
                    Assertion.Fail("success as failure"); // should not get here due to exception above
                }
                catch (Exception e)
                {
                    // ExpectedException cannot be asserted as InternalErrorExceptions are internally scoped.
                    Assertion.AssertEquals(true, e.GetType().ToString().Contains("InternalErrorException"));
                }
                finally
                {
                    CompatibilityTestHelpers.CleanupDirectory(ObjectModelHelpers.TempProjectDir);
                }
            }

            /// <summary>
            /// Tests BuildItemGroup.RemoveItem from a Evaluated and Expanded Group. Delimited List. With a preceeding call to EvaluatedItems
            /// </summary>
            /// <bug>Regression for bug:170974</bug>
            /// <remarks>
            /// This method asserts broken behaviour
            /// Should remove the evaluated item foo.foo. As *.foo has been expanded, the persisted item should be foo.foo,
            /// and so this should be removed from the xml too. A persisted build item should remain for bar.bar with no evaluated children
            /// </remarks>
            [Test]
            public void RemoveEvaluatedItemDelimtedFails()
            {
                try
                {
                    CompatibilityTestHelpers.CreateFiles(1, "foo", "foo", ObjectModelHelpers.TempProjectDir);
                    Project p = new Project();
                    object o = p.EvaluatedItems; // this causes the failure
                    p.AddNewItem("foos", Path.Combine(ObjectModelHelpers.TempProjectDir, "foo.foo;bar.bar"));
                    p.RemoveItem(p.EvaluatedItems[0]); // Exception thrown here
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
            /// Tests BuildItemGroup.RemoveItem from a Evaluated and Expanded Group. Delimited List. *Without* a preceeding call to EvaluatedItems
            /// </summary>
            /// <bug>Regression for bug:170974</bug>
            [Test]
            public void RemoveEvaluatedItemDelimitedSuccess()
            {
                try
                {
                    CompatibilityTestHelpers.CreateFiles(1, "foo", "foo", ObjectModelHelpers.TempProjectDir);
                    Project p = new Project();
                    p.AddNewItem("foos", Path.Combine(ObjectModelHelpers.TempProjectDir, "foo.foo,bar.bar"));
                    p.RemoveItem(p.EvaluatedItems[0]);
                    Assertion.AssertEquals(0, p.EvaluatedItems.Count);
                }
                finally
                {
                    CompatibilityTestHelpers.CleanupDirectory(ObjectModelHelpers.TempProjectDir);
                }
            }

            /// <summary>
            /// Tests BuildItemGroup.RemoveItem from an Evaluated and Expanded Group. *Without* a preceeding call to EvaluatedItems
            /// </summary>
            /// <bug>Regression for bug:170974</bug>
            [Test]
            public void RemoveEvaluatedItemAfterExpansionSuccess()
            {
                try
                {
                    int numberOfFoos = 5;
                    CompatibilityTestHelpers.CreateFiles(numberOfFoos, "foo", "foo", ObjectModelHelpers.TempProjectDir);
                    CompatibilityTestHelpers.CreateFiles(1, "bar", "bar", ObjectModelHelpers.TempProjectDir);
                    Project p = new Project();
                    p.AddNewItem("foos", Path.Combine(ObjectModelHelpers.TempProjectDir, "*.foo"));
                    p.RemoveItem(p.EvaluatedItems[0]);
                    Assertion.AssertEquals(true, p.EvaluatedItems[0].Include.Contains("foo1.foo"));
                    Assertion.AssertEquals(numberOfFoos - 1, p.EvaluatedItems.Count);
                }
                finally
                {
                    CompatibilityTestHelpers.CleanupDirectory(ObjectModelHelpers.TempProjectDir);
                }
            }

            /// <summary>
            /// AddNewItem Test, doesn't match anything that exists so create a new group
            /// </summary>
            [Test]
            public void RemoveItem()
            {
                Project p = new Project();
                BuildItem buildItem1 = p.AddNewItem("n", "i", true);
                Assertion.AssertNotNull(CompatibilityTestHelpers.FindBuildItem(p, "n"));
                p.RemoveItem(buildItem1);
                Assertion.AssertNull(CompatibilityTestHelpers.FindBuildItem(p, "n"));
            }

            /// <summary>
            /// RemoveItem Test, remove null
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void RemoveItem_null()
            {
                Project p = new Project();
                p.AddNewItem("n", "i", true);
                BuildItem buildItem1 = null;
                p.RemoveItem(buildItem1);
            }

            /// <summary>
            /// RemoveItem Test, remove non related project
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidOperationException))]
            public void RemoveItem_IsNotRelatedToProject()
            {
                Project p = new Project();
                BuildItem buildItem = new BuildItem("n", "i");
                p.RemoveItem(buildItem);
            }

            /// <summary>
            /// RemoveItem Test, remove imported project
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidOperationException))]
            public void RemoveItem_IsNotImported()
            {
                Project p = new Project();
                Project i = new Project(p.ParentEngine);
                BuildItem buildItem = i.AddNewItem("n", "i");
                p.RemoveItem(buildItem);
            }

            /// <summary>
            /// RemoveItem Test, dirty after removal
            /// </summary>
            [Test]
            public void RemoveItemDirtyAfterRemove()
            {
                string projectPath  = ObjectModelHelpers.CreateFileInTempProjectDirectory("save.proj", String.Empty);
                try
                {
                    Project p = new Project();
                    BuildItem buildItem = p.AddNewItem("n", "i");
                    p.Save(projectPath);
                    Assertion.AssertEquals(false, p.IsDirty);
                    p.RemoveItem(buildItem);
                    Assertion.AssertEquals(true, p.IsDirty);
                }
                finally
                {
                    CompatibilityTestHelpers.RemoveFile(projectPath);
                }
            }

            /// <summary>
            /// RemoveItem Test, check that itemgroup is removed if removed item
            /// was the last in the group. 
            /// </summary>
            [Test]
            public void RemoveItemLastInGroup()
            {    
                Project p = new Project();
                BuildItem buildItem = p.AddNewItem("n", "i");
                Assertion.AssertEquals(1, p.ItemGroups.Count);
                p.RemoveItem(buildItem);
                Assertion.AssertEquals(0, p.ItemGroups.Count);
            }

            /// <summary>
            /// RemoveItemsByName Test, no exception on removing null items
            /// </summary>
            [Test]
            public void RemoveItemsByName_Null()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup2);
                string nullstring = null;
                p.RemoveItemsByName(nullstring);
            }

            /// <summary>
            /// RemoveItemsByName Test, remove a named item when items are concete and virtual.
            /// </summary>
            [Test]
            public void RemoveItemsByNameXmlAndVirtual()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup2);
                p.AddNewItem("j", "virtual");
                p.RemoveItemsByName("j");
            }
              
            /// <summary>
            /// ItemGroups Test, Assert colection contains virutal an concrete items
            /// </summary>
            [Test]
            public void ItemGroupsGet()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup2);
                p.AddNewItem("item", "include");
                Assertion.AssertEquals(4, p.ItemGroups.Count);
            }

            /// <summary>
            /// GetEvaluatedItemsByName Test, returns an empty group if name is null.
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void GetEvaluatedItemsByName_Null()
            {
                Project p = new Project();
                string name = null;
                BuildItemGroup emptyGroup = p.GetEvaluatedItemsByName(name);
            }

            /// <summary>
            /// GetEvaluatedItemsByName Test, look for an item name that does not exist
            /// </summary>
            [Test]
            public void GetEvaluatedItemsByNameDoesNotExistItem()
            {
                Project p = new Project();
                string name = "notFound";
                BuildItemGroup emptyGroup = p.GetEvaluatedItemsByName(name);
                Assertion.AssertEquals(0, emptyGroup.Count);  
            }

            /// <summary>
            /// GetEvaluatedItemsByName Test, Only return items that match condition
            /// </summary>
            [Test]
            public void GetEvaluatedItemsByNameTwoItems()
            {
                Project p = new Project();
                string name = "new";
                p.SetProperty("condition", "false");            
                BuildItem buildItem = p.AddNewItem(name, "i1");
                buildItem.Condition = "$(condition)";
                p.AddNewItem(name, "i2");
                BuildItemGroup foundGroup = p.GetEvaluatedItemsByName(name);
                Assertion.AssertEquals(1, foundGroup.Count);
            }

            /// <summary>
            /// GetEvaluatedItemsByName Test, return all items regardless of condition.
            /// </summary>
            [Test]
            public void GetEvaluatedItemsByNameIgnoringCondition()
            {
                Project p = new Project();
                string name = "new";
                BuildItem buildItem = p.AddNewItem(name, "i1");
                p.AddNewItem(name, "i2");
                p.SetProperty("condition", "false");
                buildItem.Condition = "$(condition)";
                BuildItemGroup foundGroup = p.GetEvaluatedItemsByNameIgnoringCondition(name);
                Assertion.AssertEquals(2, foundGroup.Count);
            }

            /// <summary>
            /// RemoveAllItemGroups Test, Remove all Item groups
            /// </summary>
            [Test]
            public void RemoveAllItemGroups() 
            {
                Project p = new Project();
                p.AddNewItem("item", "i");
                Assertion.AssertEquals(1, p.ItemGroups.Count);
                p.RemoveAllItemGroups();
                Assertion.AssertEquals(0, p.ItemGroups.Count);
            }

            /// <summary>
            /// RemoveAllItemGroups Test, project 
            /// </summary>
            [Test]
            public void RemoveAllItemGroupsDirtyAfterRemove()
            {
                Project p = new Project();
                p.AddNewItem("item", "i");
                Assertion.AssertEquals(1, p.ItemGroups.Count);
                p.RemoveAllItemGroups();
                Assertion.AssertEquals(0, p.ItemGroups.Count);
            }

            /// <summary>
            /// RemoveItemGroup Test, ensure removeal does not remove an imported project
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidOperationException))]
            public void RemoveItemGroup_Imported()
            {
                Project p = new Project();
                Project i = new Project(p.ParentEngine);
                BuildItemGroup buildItemGroup = i.AddNewItemGroup();
                p.RemoveItemGroup(buildItemGroup);
            }

            /// <summary>
            /// RemoveItemGroup Test, Remove a null
            /// </summary>
            [Test]
            [ExpectedException(typeof(ArgumentNullException))]
            public void RemoveItemGroup_NullGroup()
            {
                Project p = new Project();
                BuildItemGroup buildItemGroup = null;
                p.RemoveItemGroup(buildItemGroup);
            }

            /// <summary>
            /// RemoveItemGroupsWithMatchingCondition Test,
            /// </summary>
            [Test]
            public void RemoveItemGroupsWithMatchingCondition()
            {
                Project p = new Project();
                BuildItemGroup buildItemGroup = p.AddNewItemGroup();
                buildItemGroup.Condition = "true";
                p.AddNewItemGroup();
                Assertion.AssertEquals(2, p.ItemGroups.Count);
                p.RemoveItemGroupsWithMatchingCondition("true");
                Assertion.AssertEquals(1, p.ItemGroups.Count);
            }

            /// <summary>
            /// RemoveItemGroup Test, Remove a group that isn't in the projkect
            /// </summary>
            [Test]
            [ExpectedException(typeof(InvalidOperationException))]
            public void RemoveItemGroup_NotInProject()
            {
                Project p = new Project();
                BuildItemGroup buildItemGroup1 = new BuildItemGroup();
                p.RemoveItemGroup(buildItemGroup1);
            }

            /// <summary>
            /// AddNewItemGroup Test, Check addition of item groups in order
            /// </summary>
            [Test]
            public void AddNewItemGroup()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup2);
                Assertion.AssertEquals(3, p.ItemGroups.Count);
                BuildItemGroup buildItemGroup1 = p.AddNewItemGroup();
                buildItemGroup1.Condition = "identify1";
                BuildItemGroup buildItemGroup2 = p.AddNewItemGroup();
                buildItemGroup2.Condition = "identify2";
                Assertion.AssertEquals(5, p.ItemGroups.Count);
                Assertion.AssertEquals(true, p.Xml.IndexOf("identify1") < p.Xml.IndexOf("identify2"));
            }

            /// <summary>
            /// AddNewItemGroup Test, Check Addition of group after import elements
            /// </summary>
            [Test]
            public void AddNewItemGroupAfterImports()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ItemGroup2);
                Project imported = new Project(p.ParentEngine);
                imported.LoadXml(TestData.ContentCreateItemTarget);
                Assertion.AssertEquals(3, p.ItemGroups.Count);
                BuildItemGroup buildItemGroup2 = p.AddNewItemGroup();
                buildItemGroup2.Condition = "identify2";
                Assertion.AssertEquals(4, p.ItemGroups.Count);
                Assertion.AssertEquals(true, p.Xml.IndexOf("import") < p.Xml.IndexOf("identify2"));
            }

            /// <summary>
            /// AddNewItemGroup Test, Check Addition of group after propety group
            /// </summary>
            [Test]
            public void AddNewItemGroupAfterPropertyGroup()
            {
                Project p = ObjectModelHelpers.CreateInMemoryProject(TestData.ContentValidTargetsWithOutput);
                Project imported = new Project(p.ParentEngine);
                imported.LoadXml(TestData.ContentCreateItemTarget);
                Assertion.AssertEquals(0, p.ItemGroups.Count);
                BuildItemGroup buildItemGroup = p.AddNewItemGroup();
                buildItemGroup.Condition = "identify";
                Assertion.AssertEquals(1, p.ItemGroups.Count);
                Assertion.AssertEquals(true, p.Xml.IndexOf("</propertyGroup>") < p.Xml.IndexOf("identify"));
                Assertion.AssertEquals(true, p.Xml.IndexOf("Target") > p.Xml.IndexOf("identify"));
            }
        }
    }
}
