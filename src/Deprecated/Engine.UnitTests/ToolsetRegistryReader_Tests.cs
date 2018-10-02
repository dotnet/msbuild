// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Win32;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Unit test for ToolsetRegistryReader class
    /// </summary>
    [TestFixture]
    public class ToolsetRegistryReader_Tests
    {
        // The registry key that is passed as the baseKey parameter to the ToolsetRegistryReader class
        private RegistryKey testRegistryKey = null;
        // Subkey "4.0"
        private RegistryKey currentVersionRegistryKey = null;
        // Subkey "ToolsVersions"
        private RegistryKey toolsVersionsRegistryKey = null;

        // Path to the registry key under HKCU
        // Note that this is a test registry key created solely for unit testing.
        private const string testRegistryPath = @"msbuildUnitTests";

        /// <summary>
        /// Reset the testRegistryKey
        /// </summary>
        [SetUp]
        public void Setup()
        {
            DeleteTestRegistryKey();
            testRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath);
            currentVersionRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath + "\\" + Constants.AssemblyVersion);
            toolsVersionsRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath + "\\ToolsVersions");
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTestRegistryKey();
        }

        /// <summary>
        /// Helper class to delete the testRegistryKey tree.
        /// </summary>
        private void DeleteTestRegistryKey()
        {
            if (Registry.CurrentUser.OpenSubKey(testRegistryPath) != null)
            {
                Registry.CurrentUser.DeleteSubKeyTree(testRegistryPath);
            }
        }

        /// <summary>
        /// If the base key has been deleted, then we just don't get any information (no exception)
        /// </summary>
        [Test]
        public void ReadRegistry_DeletedKey()
        {
            DeleteTestRegistryKey();
            
            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);
            Assertion.AssertEquals(0, values.Count);
        }

        /// <summary>
        /// Tests the tools version 4.0 is written to the the registry at install time
        /// </summary>
        [Test]
        public void DefaultValuesInRegistryCreatedBySetup()
        {
            ToolsetReader reader = new ToolsetRegistryReader();  //we don't use the test registry key because we want to verify the install

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);

            // Check the values in the data
            Assertion.Assert("Tools version 4.0 should be defined by default", values.Contains("4.0"));
            Assertion.AssertEquals("Default tools version should be 2.0", "2.0", defaultToolsVersion);

            if (FrameworkLocationHelper.PathToDotNetFrameworkV35 != null)
            {
                Assertion.Assert("Tools version 2.0 should be defined by default if .NET FX 3.5 exists on the machine.", values.Contains("2.0"));
                Assertion.Assert("Tools version 3.5 should be defined by default if .NET FX 3.5 exists on the machine.", values.Contains("3.5"));
            }
        }

        /// <summary>
        /// Tests we handle no default toolset specified in the registry
        /// </summary>
        [Test]
        public void DefaultValueInRegistryDoesNotExist()
        {
            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath, "3.5" /* fail to find subkey 3.5 */));

            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));

            // Should not throw
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);

            Assertion.AssertEquals(null, defaultToolsVersion);
        }

        /// <summary>
        /// The base key exists but contains no subkey or values: this is okay
        /// </summary>
        [Test]
        public void ReadRegistry_NoSubkeyNoValues()
        {
            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);

            Assertion.AssertEquals(0, values.Count);
            Assertion.AssertEquals(null, defaultToolsVersion);
        }

        /// <summary>
        /// Here we validate that MSBuild does not fail when there are unrecognized values underneath
        /// the ToolsVersion key.
        /// </summary>
        [Test]
        public void ReadRegistry_NoSubkeysOnlyValues()
        {
            toolsVersionsRegistryKey.SetValue("Name1", "Value1");
            toolsVersionsRegistryKey.SetValue("Name2", "Value2");

            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);

            Assertion.AssertEquals(0, values.Count);
            Assertion.AssertEquals(null, defaultToolsVersion);
        }

        /// <summary>
        /// Basekey has only 1 subkey
        /// </summary>
        [Test]
        public void ReadRegistry_OnlyOneSubkey()
        {
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");

            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);

            Assertion.AssertEquals(null, defaultToolsVersion);
            Assertion.AssertEquals(1, values.Count);
            Assertion.AssertEquals(0, values["tv1"].BuildProperties.Count);
            Assertion.Assert(0 == String.Compare("c:\\xxx", values["tv1"].ToolsPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Basic case
        /// </summary>
        [Test]
        public void ReadRegistry_Basic()
        {
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");            
            key1.SetValue("name1", "value1");
            RegistryKey key2 = toolsVersionsRegistryKey.CreateSubKey("tv2");
            key2.SetValue("name2", "value2");
            key2.SetValue("msbuildtoolspath", "c:\\yyy");

            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);

            Assertion.AssertEquals(2, values.Count);
            Assertion.AssertEquals(1, values["tv1"].BuildProperties.Count);
            Assertion.Assert(0 == String.Compare("c:\\xxx", values["tv1"].ToolsPath, StringComparison.OrdinalIgnoreCase));
            Assertion.Assert(0 == String.Compare("value1", values["tv1"].BuildProperties["name1"].Value, StringComparison.OrdinalIgnoreCase));
            Assertion.AssertEquals(1, values["tv2"].BuildProperties.Count);
            Assertion.Assert(0 == String.Compare("c:\\yyy", values["tv2"].ToolsPath, StringComparison.OrdinalIgnoreCase));
            Assertion.Assert(0 == String.Compare("value2", values["tv2"].BuildProperties["name2"].Value, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// baseKey contains some non-String data
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void ReadRegistry_NonStringData()
        {
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");
            key1.SetValue("name1", "value1");
            RegistryKey key2 = toolsVersionsRegistryKey.CreateSubKey("tv2");
            key2.SetValue("msbuildtoolspath", "c:\\xxx");
            key2.SetValue("name2", new String[] { "value2a", "value2b" }, RegistryValueKind.MultiString);

            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);
        }

        /// <summary>
        /// Registry has the following structure
        /// [HKCU]\basekey\
        ///    Key1
        ///        SubKey1
        ///    Key2
        ///        SubKey2
        /// </summary>
        [Test]
        public void ReadRegistry_IgnoreSubKeysExceptTopMostSubKeys()
        {
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");
            key1.SetValue("name1", "value1");
            RegistryKey subKey1 = key1.CreateSubKey("SubKey1");
            subKey1.SetValue("name1a", "value1a");
            subKey1.SetValue("name2a", "value2a");
            RegistryKey key2 = toolsVersionsRegistryKey.CreateSubKey("tv2");
            key2.SetValue("msbuildtoolspath", "c:\\yyy");
            key2.SetValue("name2", "value2");
            RegistryKey subKey2 = key2.CreateSubKey("SubKey2");
            subKey2.SetValue("name3a", "value3a");
            subKey2.SetValue("name2a", "value2a");

            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);

            Assertion.AssertEquals(2, values.Count);
            Assertion.AssertEquals(1, values["tv1"].BuildProperties.Count);
            Assertion.Assert(0 == String.Compare("c:\\xxx", values["tv1"].ToolsPath, StringComparison.OrdinalIgnoreCase));
            Assertion.Assert(0 == String.Compare("value1", values["tv1"].BuildProperties["name1"].Value, StringComparison.OrdinalIgnoreCase));
            Assertion.AssertEquals(1, values["tv2"].BuildProperties.Count);
            Assertion.Assert(0 == String.Compare("c:\\yyy", values["tv2"].ToolsPath, StringComparison.OrdinalIgnoreCase));
            Assertion.Assert(0 == String.Compare("value2", values["tv2"].BuildProperties["name2"].Value, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Regular case of getting default tools version
        /// </summary>
        [Test]
        public void GetDefaultToolsVersionFromRegistry_Basic()
        {
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", "tv1");
            RegistryKey key1 = toolsVersionsRegistryKey.CreateSubKey("tv1"); // Need matching tools version
            key1.SetValue("msbuildtoolspath", "c:\\xxx");

            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);

            Assertion.AssertEquals("tv1", defaultToolsVersion);
        }

        /// <summary>
        /// Default value is not set
        /// </summary>
        [Test]
        public void GetDefaultToolsVersionFromRegistry_DefaultValueNotSet()
        {
            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);

            Assertion.AssertEquals(null, defaultToolsVersion);
        }

        /// <summary>
        /// "DefaultToolsVersion" has non-String data
        /// </summary>
        [Test]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetDefaultToolsVersionFromRegistry_NonStringData()
        {
            currentVersionRegistryKey.SetValue("DefaultToolsVersion", new String[] { "2.0.xxxx.a", "2.0.xxxx.b" }, RegistryValueKind.MultiString);

            ToolsetReader reader = new ToolsetRegistryReader(new MockRegistryKey(testRegistryPath));
            ToolsetCollection values = new ToolsetCollection(new Engine(ToolsetDefinitionLocations.None));
            string defaultToolsVersion = reader.ReadToolsets(values, new BuildPropertyGroup(), new BuildPropertyGroup(), false);
        }
    }
}
