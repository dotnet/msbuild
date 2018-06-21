// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Win32;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Test Fixture Class for the v9 Object Model Public Interface Compatibility Tests for the Engine Class.
    /// </summary>
    [TestFixture]
    public sealed class Engine_Tests
    {
        //// Note to CTI - http://msdn2.microsoft.com/en-us/library/microsoft.build.buildengine.engine.aspx is the 
        ////    MSDN of the Engine Class.
        #region Common Helpers
        /// <summary>
        /// String of Special Characters to use in tests
        /// </summary>
        private const string SpecialCharacters = "%24%40%3b%5c%25";

        /// <summary>
        /// String of EscapableCharacters to use in tests
        /// </summary>
        private const string EscapableCharacters = @"%*?@$();\";
        #endregion

        #region Constructor Tests
        /// <summary>
        /// Tests the Engine Constructor - Engine() and verifies the defaults
        /// </summary>
        [Test]
        public void ConstructorEngineNoParameters()
        {
            Engine e = new Engine();

            string binPath20 = GetBinPathFromRegistry("2.0");

            if (binPath20 == null)
            {
                // if 2.0 can't be found in the registry, it's still the default, 
                // but we need to get it another way.
                binPath20 = FrameworkLocationHelper.PathToDotNetFrameworkV20;
            }

            if (binPath20 != null)
            {
                Assertion.AssertEquals("2.0", e.DefaultToolsVersion);
                Assertion.AssertEquals(binPath20, e.BinPath);
            }
            else
            {
                Assertion.AssertEquals("4.0", e.DefaultToolsVersion);
                Assertion.AssertEquals(GetBinPathFromRegistry("4.0"), e.BinPath);
            }

            Assertion.AssertEquals(true, e.BuildEnabled);
            Assertion.AssertEquals(false, e.IsBuilding);
            Assertion.AssertEquals(false, e.OnlyLogCriticalEvents);
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(string binPath) with an Empty String
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ConstructorEngineBinPathEmptyString()
        {
            Engine e = new Engine(String.Empty);
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(string binPath) with null
        /// </summary>
        [Test]
        public void ConstructorEngineBinPathNull()
        {
            Engine e = new Engine((string)null);

            string binPath20 = GetBinPathFromRegistry("2.0");

            if (binPath20 == null)
            {
                binPath20 = FrameworkLocationHelper.PathToDotNetFrameworkV20;
            }

            if (binPath20 != null)
            {
                // if 2.0 is in the registry, that's what the default is
                Assertion.AssertEquals(binPath20, e.BinPath);
            }
            else
            {
                // Otherwise, the default binpath is 4.0
                Assertion.AssertEquals(GetBinPathFromRegistry("4.0"), e.BinPath);
            }
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(string binPath) with binpath 
        ///     set to the system root drive
        /// </summary>
        [Test]
        public void ConstructorEngineBinPathRootDrive()
        {
            Engine e = new Engine(@"c:\");
            Assertion.AssertEquals(@"c:\", e.BinPath);
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(string binPath) with a simple path
        ///     that contains a trailing backslash.  Verify trailing backslash is 
        ///     removed.
        /// </summary>
        [Test]
        public void ConstructorEngineBinPathSimplePathWithTrailingBackSlash()
        {
            Engine e = new Engine(@"c:\somepath\");
            Assertion.AssertEquals(@"c:\somepath", e.BinPath);
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(string binPath) with a simple path
        ///     that does not contain a trailing backslash
        /// </summary>
        [Test]
        public void ConstructorEngineBinPathSimplePathWithoutTrailingBackSlash()
        {
            Engine e = new Engine(@"c:\somepath");
            Assertion.AssertEquals(@"c:\somepath", e.BinPath);
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(string binPath) with a long path
        /// </summary>
        [Test]
        [ExpectedException(typeof(PathTooLongException))]
        public void ConstructorEngineBinPathPathTooLong()
        {
            string path = CompatibilityTestHelpers.GenerateLongPath(1000);
            Engine e = new Engine(path);
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(string binPath) with Special Characters
        /// </summary>
        [Test]
        public void ConstructorEngineBinPathSpecialCharacters()
        {
            Engine e = new Engine(SpecialCharacters);
            Assertion.AssertEquals(SpecialCharacters, e.BinPath);
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(string binPath) with Escapable Characters
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ConstructorEngineBinPathEscapableCharacters()
        {
            Engine e = new Engine(EscapableCharacters);
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(ToolsetDefinitionLocations locations) with ToolsetDefinitionLocations.None
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorEngineToolsetDefinitionLocationsNone()
        {
            Engine e = new Engine(ToolsetDefinitionLocations.None);
            //// Need to actually verify
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(ToolsetDefinitionLocations locations) with ToolsetDefinitionLocations.ConfigurationFile
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorEngineToolsetDefinitionLocationsConfigurationFile()
        {
            Engine e = new Engine(ToolsetDefinitionLocations.ConfigurationFile);
            //// Need to actually verify
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(ToolsetDefinitionLocations locations) with ToolsetDefinitionLocations.Registry
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorEngineToolsetDefinitionLocationsRegistry()
        {
            Engine e = new Engine(ToolsetDefinitionLocations.Registry);
            //// Need to actually verify
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(BuildPropertyGroup globalProperties) with a null BuildPropertyGroup
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorEngineBuildPropertyGroupNull()
        {
            BuildPropertyGroup group = null;
            Engine e = new Engine(group);

            Assertion.AssertEquals(0, e.GlobalProperties.Count);
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(BuildPropertyGroup globalProperties) with a BuildPropertyGroup
        ///     that contains one property
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorEngineBuildPropertyGroupOneProperty()
        {
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(BuildPropertyGroup globalProperties) with a BuildPropertyGroup
        ///     that contains many properties, some with conditions, etc.
        /// </summary>
        [Ignore("nyi")]
        public void ConstructorEngineBuildPropertyGroupManyProperties()
        {
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(BuildPropertyGroup globalProperties, ToolsetDefinitionLocations locations)
        ///     with valid BuildPropertyGroup and ToolsetDefinitionLocations set to ConfigurationFile
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorEngineBuildPropertyGroupValidAndToolsetDefinitionLocationsConfigurationFile()
        {
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(BuildPropertyGroup globalProperties, ToolsetDefinitionLocations locations)
        ///     with valid BuildPropertyGroup and ToolsetDefinitionLocations set to Registry
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorEngineBuildPropertyGroupValidAndToolsetDefinitionLocationsRegistry()
        {
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(BuildPropertyGroup globalProperties, ToolsetDefinitionLocations locations)
        ///     with valid BuildPropertyGroup and ToolsetDefinitionLocations set to None
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorEngineBuildPropertyGroupValidAndToolsetDefinitionLocationsNone()
        {
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(BuildPropertyGroup globalProperties, ToolsetDefinitionLocations locations,
        ///                                         int numberOfCpus, string localNodeProviderParameters) with simple, valid
        ///                                         values for each parameter (1 CPU)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorMultiProcAwareSimpleValid()
        {
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(BuildPropertyGroup globalProperties, ToolsetDefinitionLocations locations,
        ///                                         int numberOfCpus, string localNodeProviderParameters) with simple, valid
        ///                                         values for each parameter (2 CPUs)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorMultiProcAware2CPUs()
        {
        }

        /// <summary>
        /// Tests the Engine Constructor - Engine(BuildPropertyGroup globalProperties, ToolsetDefinitionLocations locations,
        ///                                         int numberOfCpus, string localNodeProviderParameters) with simple, valid
        ///                                         values for each parameter (0 CPUs)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ConstructorMultiProcAware0CPUs()
        {
        }
        #endregion

        #region BinPath Tests
        /// <summary>
        /// Tests Engine.BinPath Get for basic path
        /// </summary>
        [Test]
        public void BinPathGetBasic()
        {
            Engine e = new Engine(@"c:\somepath");
            Assertion.AssertEquals(@"c:\somepath", e.BinPath);
        }

        /// <summary>
        /// Tests Engine.BinPath Set to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void BinPathSetNull()
        {
            Engine e = new Engine();
            e.BinPath = null;
        }

        /// <summary>
        /// Tests Engine.BinPath Set to Empty String
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void BinPathSetEmptyString()
        {
            Engine e = new Engine();
            e.BinPath = String.Empty;
        }

        /// <summary>
        /// Tests Engine.BinPath Set to too long of a path
        /// </summary>
        [Test]
        [ExpectedException(typeof(PathTooLongException))]
        public void BinPathSetTooLongPath()
        {
            Engine e = new Engine();
            string path = CompatibilityTestHelpers.GenerateLongPath(1000);
            e.BinPath = path;
        }

        /// <summary>
        /// Tests Engine.BinPath Set to Escapable Characters
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void BinPathSetEscapableCharacters()
        {
            Engine e = new Engine();
            e.BinPath = EscapableCharacters;
        }

        /// <summary>
        /// Tests Engine.BinPath Set to raw drive letter
        /// </summary>
        [Test]
        public void BinPathSetRawDriveLetter()
        {
            ExpectedSetValidBinPaths("C:", "C:");
        }

        /// <summary>
        /// Tests Engine.BinPath Set to Drive
        /// </summary>
        [Test]
        public void BinPathSetDriveLetter()
        {
            ExpectedSetValidBinPaths(@"C:\", @"C:\");
        }

        /// <summary>
        /// Tests Engine.BinPath Set to Drive with extra trailing slash
        /// </summary>
        [Test]
        public void BinPathSetDriveLetterExtraTrailingSlash()
        {
            ExpectedSetValidBinPaths(@"C:\\", @"C:\");
        }

        /// <summary>
        /// Tests Engine.BinPath Set to root level folder no trailing slash
        /// </summary>
        [Test]
        public void BinPathSetRootLevelFolder()
        {
            ExpectedSetValidBinPaths(@"C:\foo", @"C:\foo");
        }

        /// <summary>
        /// Tests Engine.BinPath Set to root level folder with one trailing slash
        /// </summary>
        [Test]
        public void BinPathSetRootLevelFolderOneTrailingSlash()
        {
            ExpectedSetValidBinPaths(@"C:\foo\", @"C:\foo");
        }

        /// <summary>
        /// Tests Engine.BinPath Set to root level folder with two trailing slashes
        /// </summary>
        [Test]
        public void BinPathSetRootLevelFolderTwoTrailingSlash()
        {
            ExpectedSetValidBinPaths(@"C:\foo\\", @"C:\foo\");
        }

        /// <summary>
        /// Tests Engine.BinPath Set to UNC path no trailing slash
        /// </summary>
        [Test]
        public void BinPathSetUNCPath()
        {
            ExpectedSetValidBinPaths(@"\\foo\share", @"\\foo\share");
        }

        /// <summary>
        /// Tests Engine.BinPath Set to UNC path with one trailing slash
        /// </summary>
        [Test]
        public void BinPathSetUNCPathOneTrailingSlash()
        {
            ExpectedSetValidBinPaths(@"\\foo\share\", @"\\foo\share");
        }

        /// <summary>
        /// Tests Engine.BinPath Set to UNC path with two trailing slashes
        /// </summary>
        [Test]
        public void BinPathSetUNCPathTwoTrailingSlash()
        {
            ExpectedSetValidBinPaths(@"\\foo\share\\", @"\\foo\share\");
        }

        /// <summary>
        /// Tests Engine.BinPath Set to Special Characters
        /// </summary>
        [Test]
        public void BinPathSetSpecialCharacters()
        {
            ExpectedSetValidBinPaths(SpecialCharacters, SpecialCharacters);
        }
        #endregion

        #region BuildEnabled Tests
        /// <summary>
        /// Tests Engine.BuildEnabled for the default case (expected to be true)
        /// </summary>
        [Test]
        public void BuildEnabledDefault()
        {
            Engine e = new Engine();
            Assertion.AssertEquals(true, e.BuildEnabled);
        }

        /// <summary>
        /// Tests Engine.BuildEnabled Setting to false
        /// </summary>
        [Test]
        public void BuildEnabledSetToFalse()
        {
            Engine e = new Engine();
            e.BuildEnabled = false;

            Assertion.AssertEquals(false, e.BuildEnabled);
        }

        /// <summary>
        /// Tests Engine.BuildEnabled Setting to true
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildEnabledSetToTrue()
        {
        }
        #endregion

        #region OnlyLogCriticalEvents Tests
        /// <summary>
        /// Tests Engine.OnlyLogCriticalEvents for the default case (expected to be false)
        /// </summary>
        [Test]
        public void OnlyLogCriticalEventsDefault()
        {
            Engine e = new Engine();
            Assertion.AssertEquals(false, e.OnlyLogCriticalEvents);
        }

        /// <summary>
        /// Tests Engine.OnlyLogCriticalEvents Setting to false
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void OnlyLogCriticalEventsSetToFalse()
        {
        }

        /// <summary>
        /// Tests Engine.OnlyLogCriticalEvents Setting to true
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void OnlyLogCriticalEventsSetToTrue()
        {
        }
        #endregion

        #region DefaultToolsVersion Tests
        /// <summary>
        /// Tests Engine.DefaultToolsVersion Get default
        /// </summary>
        [Test]
        public void DefaultToolsVersionGetDefault()
        {
            Engine e = new Engine();
            Assertion.AssertEquals("2.0", e.DefaultToolsVersion);
        }

        /// <summary>
        /// Tests Engine.DefaultToolsVersion attempt to Set to null
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DefaultToolsVersionSetToNull()
        {
            Engine e = new Engine();
            e.DefaultToolsVersion = null;
        }

        /// <summary>
        /// Tests Engine.DefaultToolsVersion Set to 3.5
        /// </summary>
        [Test]
        public void DefaultToolsVersionSetTo3Point5()
        {
            Engine e = new Engine();
            e.DefaultToolsVersion = "3.5";
            Assertion.AssertEquals("3.5", e.DefaultToolsVersion);
        }

        /// <summary>
        /// Tests Engine.DefaultToolsVersion Set to 2.0
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void DefaultToolsVersionSetTo2Point0()
        {
        }

        /// <summary>
        /// Tests Engine.DefaultToolsVersion attempt to set after loading project
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        [Ignore("nyi")]
        public void DefaultToolsVersionAttemptToSetAfterLoadingProject()
        {
        }

        /// <summary>
        /// Tests Engine.DefaultToolsVersion attempt to Set to String.Empty
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        [Ignore("nyi")]
        public void DefaultToolsVersionSetToEmptyString()
        {
        }
        #endregion

        #region IsBuilding Tests
        /// <summary>
        /// Tests Engine.IsBuilding, verifies when you've only just created the Engine object, that IsBuilding is false
        /// </summary>
        [Test]
        public void IsBuildingAfterOnlyCreatingEngine()
        {
            Engine e = new Engine();
            Assertion.AssertEquals(false, e.IsBuilding);
        }

        /// <summary>
        /// Tests Engine.IsBuilding While a Build is taking place (expected true)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void IsBuildingWhileABuildTakesPlace()
        {
            // likely, start build on another tread to allow you to verify during the build
        }

        /// <summary>
        /// Tests Engine.IsBuilding After a Build has completed (expected false)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void IsBuildingAfterABuildCompletes()
        {
        }
        #endregion

        #region GlobalProperties Tests
        /// <summary>
        /// Tests Engine.GlobalProperties Set to null
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GlobalPropertiesSetNull()
        {
        }

        /// <summary>
        /// Tests Engine.GlobalProperties Set to String.Empty
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GlobalPropertiesSetEmptyString()
        {
        }

        /// <summary>
        /// Tests Engine.GlobalProperties Set One Property
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GlobalPropertiesSetOneProperty()
        {
        }

        /// <summary>
        /// Tests Engine.GlobalProperties Set Multiple Properties
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GlobalPropertiesSetMultipleProperties()
        {
        }

        /// <summary>
        /// Tests Engine.GlobalProperties Attempt Set after Engine.Unload
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GlobalPropertiesSetAfterUnloadingEngine()
        {
        }

        /// <summary>
        /// Tests Engine.GlobalProperties Get when no Properties Exist
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GlobalPropertiesGetWhenNonExist()
        {
        }

        /// <summary>
        /// Tests Engine.GlobalProperties Get when only one property exists
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GlobalPropertiesGetWhenOneExists()
        {
        }

        /// <summary>
        /// Tests Engine.GlobalProperties Get when multiple properties exist
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GlobalPropertiesGetWhenMultipleExist()
        {
        }

        /// <summary>
        /// Tests Engine.GlobalProperties Get after Engine.Unload
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GlobalPropertiesGetAfterUnloadingEngine()
        {
        }
        #endregion

        #region Toolsets Tests
        /// <summary>
        /// Tests Engine.Toolsets when no toolsets exist (if it's possible to get into this state)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ToolsetsGetNoneExist()
        {
        }

        /// <summary>
        /// Tests Engine.Toolsets when only one toolsets exist
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ToolsetsGetWhenOnlyOneExists()
        {
        }

        /// <summary>
        /// Tests Engine.Toolsets when Multiple Toolsets Exist
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ToolsetsGetWhenMultipleExist()
        {
        }
        #endregion

        #region BuildProject Group of Tests (grouped in sub regions based on overload)
        #region BuildProject(Project project) Tests
        /*
         * Noting that BuildProject(Project project) calls
         * BuildProject(project, targetnames[], targetOutputs, buildFlags)
         * with (project, null, null, BuildSettings.None)
         * where project is the project that you passed in.
         */

        /// <summary>
        /// Tests Engine.BuildProject(project) - just pass in an empty project
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithEmptyProject()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project) - with a null project
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithNullProject()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project) - with in-memory project
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithInMemoryProject()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project) - with project loaded from disk
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectFromDisk()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project) - with an imported project
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithImportedProject()
        {
        }
        #endregion

        #region BuildProject(Project project, string targetName) Tests
        /*
         * Noting that BuildProject(Project project, string targetName) calls
         * BuildProject(project, targetnames[], targetOutputs, buildFlags)
         * with (project, targetName, null, BuildSettings.None)
         * where project is the project that you passed in and targetNames[] is
         * the targetName that you passed in.
         */

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName) - with empty project and valid target name
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithEmptyProjectValidTargetName()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName) - with a null project and valid target name
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithNullProjectValidTargetName()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName) - with in-memory project and valid target name
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithInMemoryProjectValidTargetName()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName) - with project loaded from disk and valid target name
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectFromDiskValidTargetName()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName) - with an imported project and valid target name
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithImportedProjectValidTargetName()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName) - with a project and an invalid target name (not null)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectAndInvalidTargetName()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName) - with a project and an String.Empty target name
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectAndEmptyStringTargetName()
        {
        }
        #endregion

        #region BuildProject(Project project, string[] targetName) Tests
        /*
         * Noting that BuildProject(Project project, string[] targetName) calls
         * BuildProject(project, targetnames[], targetOutputs, buildFlags)
         * with (project, targetName[], null, BuildSettings.None)
         * where project is the project that you passed in and targetNames[] is
         * the targetName that you passed in.
         */

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[]) - with project and an array of String.Empties
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectAndEmptyTargetNameArray()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[]) - with project and an array of different target names
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectAndSetOfDifferentTargetNames()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[]) - with project and an array of same target names
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectAndSetOfSameTargetNames()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[]) - with project and an array of mixed valid, null, empty
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectAndSetOfMixedTargetNames()
        {
            //// comment - string[] targetName should have some valid target names, some null, and some String.Empty
        }
        #endregion

        #region BuildProject(Project project, string[] targetName, IDictionary targetOutputs) Tests
        /*
         * Noting that BuildProject(Project project, string[] targetName, IDictionary targetOutputs) calls
         * BuildProject(project, targetnames[], targetOutputs, buildFlags)
         * with (project, targetName[], targetOutputs, BuildSettings.None)
         * where project is the project that you passed in and targetNames[] is
         * the targetName that you passed in and targetOutputs.
         */

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[], targetOutputs) - where targetOutputs is just one Empty String.
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithEmptyStringTargetOutput()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[], targetOutputs) - where targetOutputs are a set of different/valid outputs
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectAndDifferentValidOutputs()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[], targetOutputs) - where targetOutputs are a set of same/valid outputs
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectAndSameValidOutputs()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[], targetOutputs) - where the targetOutputs set has one null entry, rest are valid
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithProjectAndMixedOutputsWithOneNull()
        {
        }
        #endregion

        #region BuildProject(Project project, string[] targetName, IDictionary targetOutputs, BuildSettings buildFlags) Tests
        /*
         * Noting that all BuildProject() overloads, end up calling this method with buildFlags set to BuildSettings.None
         * Therefore, here we only need to test the other BuildSettings choice, BuildSettings.DoNotResetPreviouslyBuiltTargets
         */

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[], targetOutputs, buildFlags) - with BuildSettings.DoNotResetPreviouslyBuiltTargets
        ///     and null everything else (pass in null for all other parameters)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithBuildSettingDoNotResetPreviouslyBuiltTargetsAndNullEverythingElse()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[], targetOutputs, buildFlags) - with BuildSettings.DoNotResetPreviouslyBuiltTargets
        ///     and valid, in-memory project, a single, valid targetname, and valid targetOutputs.
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithBuildSettingDoNotResetPreviouslyBuiltTargetsInMemoryProjectValidAllParams()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProject(project, targetName[], targetOutputs, buildFlags) - with BuildSettings.DoNotResetPreviouslyBuiltTargets
        ///     and valid, on disk project, several valid targetNames, and valid targetOutputs
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectWithBuildSettingDoNotResetPreviouslyBuiltTargetsOnDiskProjectValidAllParams()
        {
        }
        #endregion
        #endregion

        #region BuildProjectFile Group of Tests (grouped in sub regions based on overload)
        #region BuildProjectFile(string projectFile)
        /* This overload calls BuildProjectFile(projectFile, null, this.GlobalProperties, null, BuildSettings.None)
         *  as such, passing on the projectFile.
         */

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile) where projectfile string is an String.Empty
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void BuildProjectFileEmptyString()
        {
            Engine e = new Engine();
            e.BuildProjectFile(String.Empty);
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile) where projectfile string is null
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileNull()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile) where projectfile is a simple, valid file on disk
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileSimpleValidOnDisk()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile) where projectfile exists on disk, but actual file
        ///     contents is empty (a blank file)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileBlankProjectFile()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile) where projectfile imports another project
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileImportsAnotherProject()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile) where projectfile name has a long file name
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileLongFileName()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile) where projectfile lives in deep folder location
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileDeepFolderLocation()
        {
        }
        #endregion

        #region BuildProjectFile(string projectFile, string targetName)
        /* This overload calls BuildProjectFile(projectFile, new string[] {targetName}, this.GlobalProperties,
         *      null, BuildSettings.None) as such, passing on the projectFile and your targetName.  Because the 
         *      null targetName is already covered in the BuildProjectFile(projectFile) overload, we don't
         *      need to test it again.
         */

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, targetName) where targetName is an empty string
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetNameEmptyString()
        {
            ////Engine e = new Engine();
            ////e.BuildProjectFile(@"c:\foo.proj", String.Empty);
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, targetName) where targetName is valid (example, targetName = 'Build')
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetNameValid()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, targetName) where targetName doesn't exist in project
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetNameDoesNotExist()
        {
        }
        #endregion

        #region BuildProjectFile(string projectFile, string[] targetNames)
        /* This overload calls BuildProjectFile(projectFile, targetNames, this.GlobalProperties,
                null, BuildSettings.None) as such, passing on the projectFile and your targetNames.  Because the 
         *      null targetNames is already covered in the BuildProjectFile(projectFile) overload, we don't
         *      need to test it again.
         */

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames) where targetNames is an array of empty strings
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetNamesEmptyStrings()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames) where targetNames is an array of all the same targets ({Build, Build, Build})
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetNamesAllSame()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames) where targetNames is an array of all different targets
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetNamesAllDifferent()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames) where targetNames is an array of some same, some different
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetNamesMixedSameAndDifferent()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames) where targetNames is an array of different, non-existing targets
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetNamesDifferentNonExisting()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames) where targetNames is an array of some existing, some non-existing
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetNamesSomeExistingSomeNonExisting()
        {
        }
        #endregion

        #region BuildProjectFile(string projectFile, string[] targetNames, BuildPropertyGroup globalProperties)
        /* This overload calls BuildProjectFile(projectFile, targetNames, globalProperties,
                null, BuildSettings.None) as such, passing on the projectFile, your targetNames and globalProperties
         */

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties) 
        ///     where globalProperties are null
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileGlobalPropertiesNull()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties) 
        ///     where globalProperties are valid, different
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileGlobalPropertiesValidDifferent()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties) 
        ///     where globalProperties are valid, same
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileGlobalPropertiesValidSame()
        {
        }
        #endregion

        #region BuildProjectFile(string projectFile, string[] targetNames, BuildPropertyGroup globalProperties, IDictionary targetOutputs)
        /* This overload calls BuildProjectFile(projectFile, targetNames, globalProperties,
                targetOutputs, BuildSettings.None) as such, passing on the projectFile, your targetNames, globalProperties and
         *      targetOutputs.
         */

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties, IDictionary targetOutputs) 
        ///     where targetOutputs are null
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetOutputsNull()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties, IDictionary targetOutputs) 
        ///     where targetOutputs String.Empty
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetOutputsEmptyString()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties, IDictionary targetOutputs) 
        ///     where targetOutputs simple valid
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetOutputsValid()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties, IDictionary targetOutputs) 
        ///     where targetOutputs invalid
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileTargetOutputsInvalid()
        {
        }
        #endregion

        #region BuildProjectFile(string projectFile, string[] targetNames, BuildPropertyGroup globalProperties, IDictionary targetOutputs, BuildSettings buildFlags)
        /* This overload calls BuildProjectFile(projectFile, targetNames, globalProperties, targetOutputs, buildFlags, null)
         *      as such, passing on the projectFile, your targetNames, globalProperties, targetOutputs, and buildFlags.
         *      Since all previous overloads pass BuildSettings.None, we only need to test BuildSettings.DoNotResetPreviouslyBuiltTargets
         */

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties,
        ///                                 IDictionary targetOutputs, BuildSettings buildFlags) where
        ///                                 buildFlags set to DoNotResetPreviouslyBuiltTargets and all other
        ///                                 parameters are valid, project file on disk, with just a single target
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileBuildFlagsDoNotResetPreviouslyBuiltTargetsOneTargetName()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties,
        ///                                 IDictionary targetOutputs, BuildSettings buildFlags) where
        ///                                 buildFlags set to DoNotResetPreviouslyBuiltTargets and all other
        ///                                 parameters are valid, project file on disk, with multiple different targetNames
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileBuildFlagsDoNotResetPreviouslyBuiltTargetsMultipleDifferentTargetNames()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties,
        ///                                 IDictionary targetOutputs, BuildSettings buildFlags) where
        ///                                 buildFlags set to DoNotResetPreviouslyBuiltTargets and all other
        ///                                 parameters are valid, project file on disk, with multiple same targetNames
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileBuildFlagsDoNotResetPreviouslyBuiltTargetsMultipleSameTargetNames()
        {
        }
        #endregion

        #region BuildProjectFile(string projectFile, string[] targetNames, BuildPropertyGroup globalProperties, IDictionary targetOutputs, BuildSettings buildFlags, string toolsVersion)
        /* This is the method that all of the other BuildProjectFile overloads call (with null toolsVersion).  Therefore, 
         *  we don't need to re-test setting toolsVersion to null.
         */

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties,
        ///                                 IDictionary targetOutputs, BuildSettings buildFlags, string toolsVersion)
        ///                                 where toolsVersion Set to String.Empty
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileToolsVersionEmptyString()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties,
        ///                                 IDictionary targetOutputs, BuildSettings buildFlags, string toolsVersion)
        ///                                 where toolsVersion Set to '2.0'
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileToolsVersion2Point0()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties,
        ///                                 IDictionary targetOutputs, BuildSettings buildFlags, string toolsVersion)
        ///                                 where toolsVersion Set to '3.5'
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileToolsVersion3Point5()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFile(projectFile, string[] targetNames, BuildPropertyGroup globalProperties,
        ///                                 IDictionary targetOutputs, BuildSettings buildFlags, string toolsVersion)
        ///                                 where toolsVersion Set to 'foobar' (invalid)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFileToolsVersionInvalid()
        {
        }
        #endregion
        #endregion

        #region BuildProjectFiles Tests
        /* Noting, BuildProjectFiles is really for Building Multiple Projects in Parallel
         *  The following, commented out test method comes from the Dev Unit tests for this.
         *  I'm providing this as an example of what to do here.  When complete, please delete
         *  the example Dev Unit test.
         */
        #region Example Dev Unit Test For BuildProjectFiles()
        //// Also, note in this example, there are examples of other Engine.Objects that should be 
        //// useful for you.  RegisterDistributedLogger() tests below, you'll need to look at this
        //// ***************START: Example Dev Unit test***************
        ////[Test]
        ////public void BuildProjectFilesInParallel()
        ////{
        ////    //Gets the currently loaded assembly in which the specified class is defined
        ////    Assembly engineAssembly = Assembly.GetAssembly(typeof(Engine));
        ////    string loggerClassName = "Microsoft.Build.BuildEngine.ConfigurableForwardingLogger";
        ////    string loggerAssemblyName = engineAssembly.GetName().FullName;
        ////    LoggerDescription forwardingLoggerDescription = new LoggerDescription(loggerClassName, loggerAssemblyName, null, null, LoggerVerbosity.Normal);

        ////    string[] fileNames = new string[10];
        ////    string traversalProject = TraversalProjectFile("ABC");
        ////    string[][] targetNamesPerProject = new string[fileNames.Length][];
        ////    IDictionary[] targetOutPutsPerProject = new IDictionary[fileNames.Length];
        ////    BuildPropertyGroup[] globalPropertiesPerProject = new BuildPropertyGroup[fileNames.Length];
        ////    string[] tempfilesToDelete = new string[fileNames.Length];
        ////    Engine engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation=" + AppDomain.CurrentDomain.BaseDirectory);
        ////    engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
        ////    try
        ////    {
        ////        for (int i = 0; i < fileNames.Length; i++)
        ////        {
        ////            string[] ProjectFiles1 = CreateGlobalPropertyProjectFileWithExtension("ABC");
        ////            fileNames[i] = ProjectFiles1[0];
        ////            tempfilesToDelete[i] = ProjectFiles1[1];
        ////            targetNamesPerProject[i] = new string[] { "Build" };
        ////        }

        ////        // Test building a traversal
        ////        engine.BuildProjectFile(traversalProject);
        ////        engine.Shutdown();

        ////        // Test building the same set of files in parallel
        ////        Console.Out.WriteLine("1:" + Process.GetCurrentProcess().MainModule.FileName);
        ////        Console.Out.WriteLine("2:" + AppDomain.CurrentDomain.BaseDirectory);
        ////        engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation=" + AppDomain.CurrentDomain.BaseDirectory);
        ////        engine.RegisterDistributedLogger(new ConsoleLogger(LoggerVerbosity.Normal), forwardingLoggerDescription);
        ////        engine.BuildProjectFiles(fileNames, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
        ////        engine.Shutdown();

        ////        // Do the same using singleproc
        ////        engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 4, "msbuildlocation=" + AppDomain.CurrentDomain.BaseDirectory);
        ////        engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
        ////        engine.BuildProjectFile(traversalProject);
        ////        engine.Shutdown();

        ////        engine = new Engine(null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry, 1, "msbuildlocation=" + AppDomain.CurrentDomain.BaseDirectory);
        ////        engine.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal));
        ////        engine.BuildProjectFiles(fileNames, targetNamesPerProject, globalPropertiesPerProject, targetOutPutsPerProject, BuildSettings.None, new string[fileNames.Length]);
        ////    }
        ////    finally
        ////    {
        ////        engine.Shutdown();
        ////        for (int i = 0; i < fileNames.Length; i++)
        ////        {
        ////            File.Delete(fileNames[i]);
        ////            File.Delete(tempfilesToDelete[i]);
        ////        }
        ////        File.Delete(traversalProject);
        ////    }
        ////}
        //// ***************END: example Dev unit test***************
        #endregion
        //// Please delete example dev unit test when you complete all of the Engine class tests (not just this section)

        /// <summary>
        /// Tests Engine.BuildProjectFiles(string[] projectFiles, string[][] targetNamesPerProject,
        ///                                 BuildPropertyGroup[] globalPropertiesPerProject, 
        ///                                 IDictionary[] targetOutputsPerProject, BuildSettings buildFlags,
        ///                                 string [] toolsVersions) with several project files (no traversal projects)
        ///                                 with no Project to Project (P2P) references between set of projects
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFilesSeveralProjects()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFiles(string[] projectFiles, string[][] targetNamesPerProject,
        ///                                 BuildPropertyGroup[] globalPropertiesPerProject, 
        ///                                 IDictionary[] targetOutputsPerProject, BuildSettings buildFlags,
        ///                                 string [] toolsVersions) with a single traversal project that
        ///                                 points to several projects (no P2P references)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFilesTraveralProjectWithProjectsNoP2Ps()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFiles(string[] projectFiles, string[][] targetNamesPerProject,
        ///                                 BuildPropertyGroup[] globalPropertiesPerProject, 
        ///                                 IDictionary[] targetOutputsPerProject, BuildSettings buildFlags,
        ///                                 string [] toolsVersions) with a single traversal project that points to 
        ///                                 4 projects (projects A, B, C and D) where A has a P2P to C, and B has
        ///                                 a P2P to D.
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFilesTraveralProjectWithProjectsThatHaveP2Ps()
        {
        }

        /// <summary>
        /// Tests Engine.BuildProjectFiles(string[] projectFiles, string[][] targetNamesPerProject,
        ///                                 BuildPropertyGroup[] globalPropertiesPerProject, 
        ///                                 IDictionary[] targetOutputsPerProject, BuildSettings buildFlags,
        ///                                 string [] toolsVersions) with a traversal project, which in turn
        ///                                 points to several other traversal projects, each of which have their
        ///                                 own set of projects (some with P2Ps, some without)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void BuildProjectFilesTraveralProjectPointingToTraveralProjectsWithProjects()
        {
        }

        //// Need to also add tests for each of the following error conditions that BuildProjectFiles() verifies before doing anything
         ////   error.VerifyThrowArgumentArraysSameLength(projectFiles, targetNamesPerProject, "projectFiles", "targetNamesPerProject");
         ////   error.VerifyThrowArgument(projectFiles.Length > 0, "projectFilesEmpty");
         ////   error.VerifyThrowArgumentArraysSameLength(projectFiles, globalPropertiesPerProject, "projectFiles", "globalPropertiesPerProject");
         ////   error.VerifyThrowArgumentArraysSameLength(projectFiles, targetOutputsPerProject, "projectFiles", "targetOutputsPerProject");
         ////   error.VerifyThrowArgumentArraysSameLength(projectFiles, toolsVersions, "projectFiles", "toolsVersions");
        #endregion

        #region CreateNewProject Tests
        /// <summary>
        /// Tests Engine.CreateNewProject()
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void CreateNewProject()
        {
            Engine e = new Engine();
            e.CreateNewProject();
            //// TODO - figure out how to verify CreateNewProject actually worked
        }
        #endregion

        #region GetLoadedProject Tests
        /// <summary>
        /// Tests Engine.GetLoadedProject(string projectFullFileName) with projectFullFileName set to Null
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GetLoadedProjectNull()
        {
        }

        /// <summary>
        /// Tests Engine.GetLoadedProject(string projectFullFileName) with projectFullFileName set to String.Empty
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GetLoadedProjectEmptyString()
        {
        }

        /// <summary>
        /// Tests Engine.GetLoadedProject(string projectFullFileName) with projectFullFileName set to a valid, existing
        ///     project file
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GetLoadedProjectExisting()
        {
        }

        /// <summary>
        /// Tests Engine.GetLoadedProject(string projectFullFileName) with projectFullFileName set to an non-existing 
        /// project file name, yet path exist
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GetLoadedProjectNonExistingProjectFileValidPath()
        {
        }

        /// <summary>
        /// Tests Engine.GetLoadedProject(string projectFullFileName) with projectFullFileName set to an non-existing
        /// project file name where path also doesn't exist
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GetLoadedProjectNonExistingProjectFileAndPath()
        {
        }

        /// <summary>
        /// Tests Engine.GetLoadedProject(string projectFullFileName) with projectFullFileName set to an existing
        /// project file name, but file contents is invalid (just random characters within file)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GetLoadedProjectExistingProjectFileInvalidFileContents()
        {
        }

        /// <summary>
        /// Tests Engine.GetLoadedProject(string projectFullFileName) with projectFullFileName set to an existing
        /// project file name, but file contents is empty
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void GetLoadedProjectExistingProjectFileEmptyFileContents()
        {
        }
        #endregion

        #region RegisterDistributedLogger Tests
        /// <summary>
        /// Tests Engine.RegisterDistributedLogger(ILogger centralLogger, LoggerDescription forwardingLogger) when
        ///     passing in Nulls for both parameters
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterDistributedLoggerNullParams()
        {
        }

        /// <summary>
        /// Tests Engine.RegisterDistributedLogger(ILogger centralLogger, LoggerDescription forwardingLogger) when
        ///     you have a valid centralLogger and forwardingLogger
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterDistributedLoggerValidCentralAndForwardingLoggers()
        {
        }

        /// <summary>
        /// Tests Engine.RegisterDistributedLogger(ILogger centralLogger, LoggerDescription forwardingLogger) when
        ///     the CentralLogger has already been Registered, but the ForwardingLogger has not
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterDistributedLoggerCentralAlreadyRegisteredForwarindNot()
        {
        }

        /// <summary>
        /// Tests Engine.RegisterDistributedLogger(ILogger centralLogger, LoggerDescription forwardingLogger) when
        ///     the CentralLogger has not been registered yet, but the ForwardingLogger has been registered
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterDistributedLoggerCentralNotForwardingAlreadyRegistered()
        {
        }

        /// <summary>
        /// Tests Engine.RegisterDistributedLogger(ILogger centralLogger, LoggerDescription forwardingLogger) when
        ///     when both central and forwarding loggers have been registered already
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterDistributedLoggerBothAlreadyRegistered()
        {
        }

        /// <summary>
        /// Tests Engine.RegisterDistributedLogger(ILogger centralLogger, LoggerDescription forwardingLogger) when
        ///     when you use the same logger for the central logger and the fowarding logger (not already registered)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterDistributedLoggerSameLoggerForCentralAndForwarding()
        {
        }
        #endregion

        #region RegisterLogger Tests
        /// <summary>
        /// Tests Engine.RegisterLogger(ILogger logger) with 
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterLogger()
        {
        }

        /// <summary>
        /// Tests Engine.RegisterLogger(ILogger logger) with null logger
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterLoggerNull()
        {
        }

        /// <summary>
        /// Tests Engine.RegisterLogger(ILogger logger) with basic logger
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterLoggerBasicLogger()
        {
        }

        /// <summary>
        /// Tests Engine.RegisterLogger(ILogger logger) with logger that's already been registered
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void RegisterLoggerLoggerAlreadyRegistered()
        {
        }
        #endregion

        #region Shutdown Tests
        /// <summary>
        /// Tests Engine.Shutdown() when you've not loaded anything (basically just new up a new Engine
        ///     and shut it down
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ShutdownWithNothingLoaded()
        {
        }

        /// <summary>
        /// Tests Engine.Shutdown() when you've loaded a project, but never built (and don't unload project)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ShutdownAfterLoadingOnlyProject()
        {
        }

        /// <summary>
        /// Tests Engine.Shutdown() when when you've loaded a project, built it, and unloaded it, then engine.shutdown()
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ShutdownLoadProjectBuildUnloadProject()
        {
        }

        /// <summary>
        /// Tests Engine.Shutdown() when when you've loaded a project, loggers, etc, no building, nothing unloaded
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ShutdownLoadProjectLoggersNoBuild()
        {
        }

        /// <summary>
        /// Tests Engine.Shutdown() when you've loaded a project, loggers, etc, built, then only unloaded the loggers, 
        ///     project still loaded
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ShutdownLoadProjectLoggersBuildUnloadOnlyLoggers()
        {
        }

        /// <summary>
        /// Tests Engine.Shutdown() when you've loaded a project, loggers, etc, built, then unloaded everything
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void ShutdownLoadedBuiltUnloaded()
        {
        }
        #endregion

        #region UnloadAllProjects Tests
        /// <summary>
        /// Tests Engine.UnloadAllProjects() when no projects are loaded
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadAllProjectsWhenNoProjectsLoaded()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadAllProjects() when only one project has been loaded
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadAllProjectsOneProjectLoaded()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadAllProjects() when Multiple projects are loaded
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadAllProjectsMultipleProjectsLoaded()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadAllProjects() when mix, some projects already unloaded, some still loaded
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadAllProjectsSomeProjectsUnloadedSomeProjectsLoaded()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadAllProjects() when when imported projects exist
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadAllProjectsWithImportedProjects()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadAllProjects() with a traversal project that references many projects, all loaded and have built
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadAllProjectsWithTraversalProject()
        {
        }
        #endregion

        #region UnloadProject Tests
        /// <summary>
        /// Tests Engine.UnloadProject(Project project) simple project that has been loaded
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadProjectSimpleProject()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadProject(Project project) passing in null
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadProjectNull()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadProject(Project project) a project that hasn't been loaded
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadProjectANonLoadedProject()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadProject(Project project) an imported project from another project
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadProjectImportedProject()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadProject(Project project) a traversal project
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadProjectTraversalProject()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadProject(Project project) one of many Loaded Projects (ensure other loaded projects remain loaded)
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadProjectOneOfMany()
        {
        }

        /// <summary>
        /// Tests Engine.UnloadProject(Project project) several projects, one at a time
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnloadProjectSeveralOneAtATime()
        {
        }
        #endregion

        #region UnregisterAllLoggers Tests
        /// <summary>
        /// Tests Engine.UnregisterAllLoggers() when you have no loggers registered
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnregisterAllLoggersWhenNoLoggersAreRegistered()
        {
        }

        /// <summary>
        /// Tests Engine.UnregisterAllLoggers()  when you have only one logger registered
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnregisterAllLoggersWithOneLogger()
        {
        }

        /// <summary>
        /// Tests Engine.UnregisterAllLoggers() when you have multiple loggers registered
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnregisterAllLoggersWithMultipleLoggers()
        {
        }

        /// <summary>
        /// Tests Engine.UnregisterAllLoggers() when you have only Distributed Central and forwarding loggers registered
        /// </summary>
        [Test]
        [Ignore("nyi")]
        public void UnregisterAllLoggersWithDistributedLoggers()
        {
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// blah
        /// </summary>
        /// <param name="binPath">a</param>
        /// <param name="expectedResult">a</param>
        private void ExpectedSetValidBinPaths(string binPath, string expectedResult)
        {
            Engine e = new Engine();
            e.BinPath = binPath;
            Assertion.AssertEquals(expectedResult, e.BinPath);
        }

        /// <summary>
        /// Gets you the FX path string
        /// </summary>
        /// <param name="toolsVersion">2.0, 3.5, etc</param>
        /// <returns>Full path to the FX folder based on the toolsversion</returns>
        private string GetBinPathFromRegistry(string toolsVersion)
        {
            // [HKLM]\SOFTWARE\Microsoft
            //   msbuild
            //     3.5
            //       @DefaultToolsVersion = 2.0
            //     ToolsVersions
            //       2.0
            //         @MSBuildToolsPath = D:\SomeFolder
            //       3.5
            //         @MSBuildToolsPath = D:\SomeOtherFolder
            //         @MSBuildBinPath = D:\SomeOtherFolder
            //         @SomePropertyName = PropertyOtherValue
            string toolsPath = (string)Registry.GetValue(@"hkey_local_machine\software\microsoft\msbuild\toolsversions\" + toolsVersion, "MSBuildToolsPath", null);

            if (toolsPath != null)
            {
                toolsPath = toolsPath.TrimEnd(new char[] { '\\' });
            }

            return toolsPath;
        }

        #endregion
    }
}
