// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Win32;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using InvalidToolsetDefinitionException = Microsoft.Build.Exceptions.InvalidToolsetDefinitionException;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    /// Unit test for ToolsetRegistryReader class
    /// </summary>
    [TestClass]
    public class ToolsetRegistryReader_Tests
    {
        // The registry key that is passed as the baseKey parameter to the ToolsetRegistryReader class
        private RegistryKey _testRegistryKey = null;
        // Subkey "3.5"
        private RegistryKey _currentVersionRegistryKey = null;
        // Subkey "ToolsVersions"
        private RegistryKey _toolsVersionsRegistryKey = null;

        // Path to the registry key under HKCU
        // Note that this is a test registry key created solely for unit testing.
        private const string testRegistryPath = @"msbuildUnitTests";

        /// <summary>
        /// Store the value of the "VisualStudioVersion" environment variable here so that 
        /// we can unset it for the duration of the test.
        /// </summary>
        private string _oldVisualStudioVersion;

        /// <summary>
        /// Reset the testRegistryKey
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            DeleteTestRegistryKey();
            _testRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath);
            _currentVersionRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath + "\\" + Constants.AssemblyVersion);
            _toolsVersionsRegistryKey = Registry.CurrentUser.CreateSubKey(testRegistryPath + "\\ToolsVersions");

            _oldVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");
            Environment.SetEnvironmentVariable("VisualStudioVersion", null);
        }

        [TestCleanup]
        public void TearDown()
        {
            DeleteTestRegistryKey();

            Environment.SetEnvironmentVariable("VisualStudioVersion", _oldVisualStudioVersion);
        }

        /// <summary>
        /// Callback for toolset collection
        /// </summary>
        public void ToolsetAdded(Toolset toolset)
        {
            // Do nothing
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
        [TestMethod]
        public void ReadRegistry_DeletedKey()
        {
            DeleteTestRegistryKey();

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
            Assert.AreEqual(0, values.Count);
        }

        /// <summary>
        /// Tests the tools version 4.0 is written to the the registry at install time
        /// </summary>
        [TestMethod]
        [Ignore]
        // Ignore: Test requires installed toolset.
        public void DefaultValuesInRegistryCreatedBySetup()
        {
            ToolsetReader reader = new ToolsetRegistryReader(new ProjectCollection().EnvironmentProperties, new PropertyDictionary<ProjectPropertyInstance>());  //we don't use the test registry key because we want to verify the install

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            // Check the values in the data
            Assert.IsTrue(values.ContainsKey("4.0"), "Tools version 4.0 should be defined by default");
            Assert.IsTrue(values.ContainsKey(ObjectModelHelpers.MSBuildDefaultToolsVersion), String.Format("Tools version {0} should be defined by default", ObjectModelHelpers.MSBuildDefaultToolsVersion));
            Assert.AreEqual("2.0", defaultToolsVersion, "Default tools version should be 2.0");
        }

        /// <summary>
        /// Tests we handle no default toolset specified in the registry
        /// </summary>
        [TestMethod]
        public void DefaultValueInRegistryDoesNotExist()
        {
            ToolsetReader reader = new ToolsetRegistryReader(new ProjectCollection().EnvironmentProperties, new PropertyDictionary<ProjectPropertyInstance>(), new MockRegistryKey(testRegistryPath, "3.5" /* fail to find subkey 3.5 */));

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            // Should not throw
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(null, defaultToolsVersion);
        }

        /// <summary>
        /// The base key exists but contains no subkey or values: this is okay
        /// </summary>
        [TestMethod]
        public void ReadRegistry_NoSubkeyNoValues()
        {
            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(0, values.Count);
            Assert.AreEqual(null, defaultToolsVersion);
        }

        /// <summary>
        /// Here we validate that MSBuild does not fail when there are unrecognized values underneath
        /// the ToolsVersion key.
        /// </summary>
        [TestMethod]
        public void ReadRegistry_NoSubkeysOnlyValues()
        {
            _toolsVersionsRegistryKey.SetValue("Name1", "Value1");
            _toolsVersionsRegistryKey.SetValue("Name2", "Value2");

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(0, values.Count);
            Assert.AreEqual(null, defaultToolsVersion);
        }

        /// <summary>
        /// Basekey has only 1 subkey
        /// </summary>
        [TestMethod]
        public void ReadRegistry_OnlyOneSubkey()
        {
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(null, defaultToolsVersion);
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(0, values["tv1"].Properties.Count);
            Assert.IsTrue(0 == String.Compare("c:\\xxx", values["tv1"].ToolsPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Basic case
        /// </summary>
        [TestMethod]
        public void ReadRegistry_Basic()
        {
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");
            key1.SetValue("name1", "value1");
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("tv2");
            key2.SetValue("name2", "value2");
            key2.SetValue("msbuildtoolspath", "c:\\yyy");

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(2, values.Count);
            Assert.AreEqual(1, values["tv1"].Properties.Count);
            Assert.IsTrue(0 == String.Compare("c:\\xxx", values["tv1"].ToolsPath, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(0 == String.Compare("value1", values["tv1"].Properties["name1"].EvaluatedValue, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(1, values["tv2"].Properties.Count);
            Assert.IsTrue(0 == String.Compare("c:\\yyy", values["tv2"].ToolsPath, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(0 == String.Compare("value2", values["tv2"].Properties["name2"].EvaluatedValue, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// baseKey contains some non-String data
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void ReadRegistry_NonStringData()
        {
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");
            key1.SetValue("name1", "value1");
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("tv2");
            key2.SetValue("msbuildtoolspath", "c:\\xxx");
            key2.SetValue("name2", new String[] { "value2a", "value2b" }, RegistryValueKind.MultiString);

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
        }

        /// <summary>
        /// Registry has the following structure
        /// [HKCU]\basekey\
        ///    Key1
        ///        SubKey1
        ///    Key2
        ///        SubKey2
        ///        SubKey3
        /// </summary>
        [TestMethod]
        public void ReadRegistry_HasSubToolsets()
        {
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");
            key1.SetValue("name1", "value1");
            RegistryKey subKey1 = key1.CreateSubKey("SubKey1");
            subKey1.SetValue("name1a", "value1a");
            subKey1.SetValue("name2a", "value2a");
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("tv2");
            key2.SetValue("msbuildtoolspath", "c:\\yyy");
            key2.SetValue("name2", "value2");
            RegistryKey subKey2 = key2.CreateSubKey("SubKey2");
            subKey2.SetValue("name3a", "value3a");
            subKey2.SetValue("name2a", "value2a");
            RegistryKey subKey3 = key2.CreateSubKey("SubKey3");
            subKey3.SetValue("name4a", "value4a");
            subKey3.SetValue("name5a", "value5a");

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(2, values.Count);
            Assert.AreEqual(1, values["tv1"].Properties.Count);
            Assert.AreEqual("c:\\xxx", values["tv1"].ToolsPath);
            Assert.AreEqual("value1", values["tv1"].Properties["name1"].EvaluatedValue);
            Assert.AreEqual(1, values["tv1"].SubToolsets.Count);
            Assert.AreEqual(2, values["tv1"].SubToolsets["SubKey1"].Properties.Count);
            Assert.AreEqual("value1a", values["tv1"].SubToolsets["SubKey1"].Properties["name1a"].EvaluatedValue);
            Assert.AreEqual("value2a", values["tv1"].SubToolsets["SubKey1"].Properties["name2a"].EvaluatedValue);

            Assert.AreEqual(1, values["tv2"].Properties.Count);
            Assert.AreEqual("c:\\yyy", values["tv2"].ToolsPath);
            Assert.AreEqual("value2", values["tv2"].Properties["name2"].EvaluatedValue);
            Assert.AreEqual(2, values["tv2"].SubToolsets.Count);
            Assert.AreEqual(2, values["tv2"].SubToolsets["SubKey2"].Properties.Count);
            Assert.AreEqual("value3a", values["tv2"].SubToolsets["SubKey2"].Properties["name3a"].EvaluatedValue);
            Assert.AreEqual("value2a", values["tv2"].SubToolsets["SubKey2"].Properties["name2a"].EvaluatedValue);
            Assert.AreEqual(2, values["tv2"].SubToolsets["SubKey3"].Properties.Count);
            Assert.AreEqual("value4a", values["tv2"].SubToolsets["SubKey3"].Properties["name4a"].EvaluatedValue);
            Assert.AreEqual("value5a", values["tv2"].SubToolsets["SubKey3"].Properties["name5a"].EvaluatedValue);
        }

        /// <summary>
        /// Registry has the following structure
        /// [HKCU]\basekey\
        ///    Key1
        ///        SubKey1
        ///            SubSubKey1
        /// </summary>
        [TestMethod]
        public void ReadRegistry_IgnoreSubToolsetSubKeys()
        {
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");
            key1.SetValue("name1", "value1");
            RegistryKey subKey1 = key1.CreateSubKey("SubKey1");
            subKey1.SetValue("name1a", "value1a");
            subKey1.SetValue("name2a", "value2a");
            RegistryKey subSubKey1 = subKey1.CreateSubKey("SubSubKey1");
            subSubKey1.SetValue("name2b", "value2b");

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(1, values["tv1"].Properties.Count);
            Assert.AreEqual("c:\\xxx", values["tv1"].ToolsPath);
            Assert.AreEqual("value1", values["tv1"].Properties["name1"].EvaluatedValue);
            Assert.AreEqual(1, values["tv1"].SubToolsets.Count);
            Assert.AreEqual(2, values["tv1"].SubToolsets["SubKey1"].Properties.Count);
            Assert.AreEqual("value1a", values["tv1"].SubToolsets["SubKey1"].Properties["name1a"].EvaluatedValue);
            Assert.AreEqual("value2a", values["tv1"].SubToolsets["SubKey1"].Properties["name2a"].EvaluatedValue);
        }

        /// <summary>
        /// Verifies that if a value is defined in both the base toolset and the 
        /// selected subtoolset, the subtoolset value overrides -- even if that 
        /// value is empty.
        /// </summary>
        [TestMethod]
        public void ReadRegistry_SubToolsetOverridesBaseToolsetEntries()
        {
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");
            key1.SetValue("name1", "value1");
            key1.SetValue("name2", "value2");
            RegistryKey subKey1 = key1.CreateSubKey("Foo");
            subKey1.SetValue("name1", "value1a");
            subKey1.SetValue("name2", "");

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(2, values["tv1"].Properties.Count);
            Assert.AreEqual("c:\\xxx", values["tv1"].ToolsPath);
            Assert.AreEqual("value1", values["tv1"].Properties["name1"].EvaluatedValue);
            Assert.AreEqual("value2", values["tv1"].Properties["name2"].EvaluatedValue);
            Assert.AreEqual(1, values["tv1"].SubToolsets.Count);
            Assert.AreEqual(2, values["tv1"].SubToolsets["Foo"].Properties.Count);
            Assert.AreEqual("value1a", values["tv1"].SubToolsets["Foo"].Properties["name1"].EvaluatedValue);
            Assert.AreEqual("", values["tv1"].SubToolsets["Foo"].Properties["name2"].EvaluatedValue);

            // Check when requesting the final evaluated value of the property in the context of its sub-toolset
            // that the sub-toolset overrides
            Assert.AreEqual("value1a", values["tv1"].GetProperty("name1", "Foo").EvaluatedValue);
            Assert.AreEqual("", values["tv1"].GetProperty("name2", "Foo").EvaluatedValue);
        }

        /// <summary>
        /// Verifies that if a value is defined in both the base toolset and the 
        /// selected subtoolset, the subtoolset value overrides -- even if that 
        /// value is empty.
        /// </summary>
        [TestMethod]
        public void ReadRegistry_UnselectedSubToolsetIsIgnored()
        {
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");
            key1.SetValue("name1", "value1");
            key1.SetValue("name2", "value2");
            RegistryKey subKey1 = key1.CreateSubKey("Foo");
            subKey1.SetValue("name1", "value1a");
            subKey1.SetValue("name2", "");

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(1, values.Count);
            Assert.AreEqual(2, values["tv1"].Properties.Count);
            Assert.AreEqual("c:\\xxx", values["tv1"].ToolsPath);
            Assert.AreEqual("value1", values["tv1"].Properties["name1"].EvaluatedValue);
            Assert.AreEqual("value2", values["tv1"].Properties["name2"].EvaluatedValue);
        }

        /// <summary>
        /// Regular case of getting default tools version
        /// </summary>
        [TestMethod]
        public void GetDefaultToolsVersionFromRegistry_Basic()
        {
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "tv1");
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1"); // Need matching tools version
            key1.SetValue("msbuildtoolspath", "c:\\xxx");

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual("tv1", defaultToolsVersion);
        }

        /// <summary>
        /// Default value is not set
        /// </summary>
        [TestMethod]
        public void GetDefaultToolsVersionFromRegistry_DefaultValueNotSet()
        {
            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(null, defaultToolsVersion);
        }

        /// <summary>
        /// "DefaultToolsVersion" has non-String data
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetDefaultToolsVersionFromRegistry_NonStringData()
        {
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", new String[] { "2.0.xxxx.a", "2.0.xxxx.b" }, RegistryValueKind.MultiString);

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
        }

        private ToolsetRegistryReader GetStandardRegistryReader()
        {
            return new ToolsetRegistryReader(new ProjectCollection().EnvironmentProperties, new PropertyDictionary<ProjectPropertyInstance>(), new MockRegistryKey(testRegistryPath));
        }

        /// <summary>
        /// Regular case of getting overridetaskspath
        /// </summary>
        [TestMethod]
        public void GetOverrideTasksPathFromRegistry_Basic()
        {
            _currentVersionRegistryKey.SetValue("MsBuildOverrideTasksPath", "c:\\Foo");

            ToolsetReader reader = GetStandardRegistryReader();
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual("c:\\Foo", msbuildOverrideTasksPath);
        }

        /// <summary>
        /// OverrideTasksPath is not set
        /// </summary>
        [TestMethod]
        public void GetOverrideTasksPathFromRegistry_ValueNotSet()
        {
            ToolsetReader reader = GetStandardRegistryReader();
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(null, msbuildOverrideTasksPath);
        }

        /// <summary>
        /// "OverrideTasksPath" has non-String data
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetOverrideTasksPathFromRegistry_NonStringData()
        {
            _currentVersionRegistryKey.SetValue("MsBuildOverrideTasksPath", new String[] { "2938304894", "3948394.2.3.3.3" }, RegistryValueKind.MultiString);

            ToolsetReader reader = GetStandardRegistryReader();
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
        }

        /// <summary>
        /// Regular case of getting the default override toolsversion
        /// </summary>
        [TestMethod]
        public void GetDefaultOverrideToolsVersionFromRegistry_Basic()
        {
            _currentVersionRegistryKey.SetValue("DefaultOverrideToolsVersion", "15.0");

            ToolsetReader reader = GetStandardRegistryReader();
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual("15.0", defaultOverrideToolsVersion);
        }

        /// <summary>
        /// DefaultOverrideToolsVersion is not set
        /// </summary>
        [TestMethod]
        public void GetDefaultOverrideToolsVersionFromRegistry_ValueNotSet()
        {
            ToolsetReader reader = GetStandardRegistryReader();
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.AreEqual(null, defaultOverrideToolsVersion);
        }

        /// <summary>
        /// "DefaultOverrideToolsVersion" has non-String data
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidToolsetDefinitionException))]
        public void GetDefaultOverrideToolsVersionFromRegistry_NonStringData()
        {
            _currentVersionRegistryKey.SetValue("DefaultOverrideToolsVersion", new String[] { "2938304894", "3948394.2.3.3.3" }, RegistryValueKind.MultiString);

            ToolsetReader reader = GetStandardRegistryReader();
            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);
        }

        [TestMethod]
        public void ReadToolsets_NoBinPathOrToolsPath()
        {
            RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", "c:\\xxx");
            key1.SetValue("name1", "value1");
            RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("tv2");
            key2.SetValue("name2", "value2");
            RegistryKey key3 = _toolsVersionsRegistryKey.CreateSubKey("tv3");
            key3.SetValue("msbuildtoolspath", "c:\\zzz");
            key3.SetValue("name3", "value3");

            ToolsetReader reader = GetStandardRegistryReader();
            string msbuildOverrideTasksPath = null;
            string defaultOverrideToolsVersion = null;

            Dictionary<string, Toolset> values = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            //should not throw
            string defaultToolsVersion = reader.ReadToolsets(values, new PropertyDictionary<ProjectPropertyInstance>(), new PropertyDictionary<ProjectPropertyInstance>(), false, out msbuildOverrideTasksPath, out defaultOverrideToolsVersion);

            Assert.IsTrue(values.ContainsKey("tv1"));

            //should not contain the second toolset because it does not define a tools/bin path
            Assert.IsFalse(values.ContainsKey("tv2"));

            Assert.IsTrue(values.ContainsKey("tv3"));
        }
    }
}
