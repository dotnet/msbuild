// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Test that we can retrieve the list of SDKs and output them to the project file.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using System.Linq;
using Microsoft.Build.Execution;

namespace Microsoft.Build.UnitTests.GetInstalledSDKLocations_Tests
{
    /// <summary>
    /// Test the GetInstalledSDKLocations task
    /// </summary>
    [TestClass]
    public class GetInstalledSDKLocationsTestFixture
    {
        private static string s_fakeSDKStructureRoot = null;
        private static string s_fakeSDKStructureRoot2 = null;

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            s_fakeSDKStructureRoot = MakeFakeSDKStructure();
            s_fakeSDKStructureRoot2 = MakeFakeSDKStructure2();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (FileUtilities.DirectoryExistsNoThrow(s_fakeSDKStructureRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(s_fakeSDKStructureRoot, true);
            }

            if (FileUtilities.DirectoryExistsNoThrow(s_fakeSDKStructureRoot2))
            {
                FileUtilities.DeleteDirectoryNoThrow(s_fakeSDKStructureRoot2, true);
            }
        }

        #region TestMethods
        /// <summary>
        /// Make sure we get a ArgumentException if null is passed into the target platform version.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullTargetPlatformVersion()
        {
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = "Hello";
            t.TargetPlatformVersion = null;
            t.Execute();
        }

        /// <summary>
        /// Make sure we get a ArgumentException if null is passed into the target platform version.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullTargetPlatformIdentifier()
        {
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = null;
            t.TargetPlatformVersion = "1.0";
            t.Execute();
        }

        /// <summary>
        /// Make sure we get an error message if an empty platform identifier is passed in.
        /// </summary>
        [TestMethod]
        public void EmptyTargetPlatformIdentifier()
        {
            MockEngine engine = new MockEngine();
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = String.Empty;
            t.TargetPlatformVersion = "1.0";
            t.BuildEngine = engine;
            bool success = t.Execute();
            Assert.IsFalse(success);

            Assert.IsTrue(engine.Errors == 1);
            engine.AssertLogContains("MSB3784");
        }

        /// <summary>
        /// Make sure we get an error message if an empty platform Version is passed in.
        /// </summary>
        [TestMethod]
        public void EmptyTargetPlatformVersion()
        {
            MockEngine engine = new MockEngine();
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = "Hello";
            t.TargetPlatformVersion = String.Empty;
            t.BuildEngine = engine;
            bool success = t.Execute();
            Assert.IsFalse(success);


            Assert.IsTrue(engine.Errors == 1);
            engine.AssertLogContains("MSB3784");
        }

        /// <summary>
        /// Make sure we get an error message if an empty platform Version is passed in.
        /// </summary>
        [TestMethod]
        public void BadTargetPlatformVersion()
        {
            MockEngine engine = new MockEngine();
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = "Hello";
            t.TargetPlatformVersion = "CAT";
            t.BuildEngine = engine;
            bool success = t.Execute();
            Assert.IsFalse(success);


            Assert.IsTrue(engine.Errors == 1);
            engine.AssertLogContains("MSB3786");
        }

        /// <summary>
        /// Make sure we get an Warning if no SDKs were found.
        /// </summary>
        [TestMethod]
        public void NoSDKsFound()
        {
            MockEngine engine = new MockEngine();
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = "Hello";
            t.TargetPlatformVersion = "1.0";
            t.BuildEngine = engine;
            bool success = t.Execute();
            Assert.IsTrue(success);

            Assert.IsTrue(engine.Warnings == 1);
            engine.AssertLogContains("MSB3785");
        }

        /// <summary>
        /// Get a good set of SDKS installed on the machine from the fake SDK location.
        /// </summary>
        [TestMethod]
        public void GetSDKVersions()
        {
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", s_fakeSDKStructureRoot + ";" + s_fakeSDKStructureRoot2);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
                MockEngine engine = new MockEngine();
                GetInstalledSDKLocations t = new GetInstalledSDKLocations();
                t.TargetPlatformIdentifier = "Windows";
                t.TargetPlatformVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue).ToString();
                t.SDKRegistryRoot = "Somewhere";
                t.SDKRegistryRoot = "Hello;Jello";
                t.BuildEngine = engine;
                bool success = t.Execute();
                Assert.IsTrue(success);

                ITaskItem[] installedSDKs = t.InstalledSDKs;
                Assert.IsTrue(installedSDKs.Length == 6);

                Dictionary<string, string> sdksAndVersions = new Dictionary<string, string>();

                foreach (ITaskItem item in installedSDKs)
                {
                    sdksAndVersions.Add(item.GetMetadata("SDKName"), item.GetMetadata("PlatformVersion"));
                }

                Assert.IsTrue(sdksAndVersions["MyAssembly, Version=1.0"] == "1.0");
                Assert.IsTrue(sdksAndVersions["MyAssembly, Version=2.0"] == "1.0");
                Assert.IsTrue(sdksAndVersions["MyAssembly, Version=3.0"] == "2.0");
                Assert.IsTrue(sdksAndVersions["MyAssembly, Version=4.0"] == "1.0");
                Assert.IsTrue(sdksAndVersions["MyAssembly, Version=5.0"] == "1.0");
                Assert.IsTrue(sdksAndVersions["MyAssembly, Version=6.0"] == "2.0");

                Assert.IsFalse(sdksAndVersions.ContainsValue("3.0"));
                Assert.IsFalse(sdksAndVersions.ContainsValue("4.0"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", null);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            }
        }

        /// <summary>
        /// Get a good set of SDKS installed on the machine from the fake SDK location.
        /// </summary>
        [TestMethod]
        public void GetGoodSDKs()
        {
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", s_fakeSDKStructureRoot + ";" + s_fakeSDKStructureRoot2);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
                MockEngine engine = new MockEngine();
                GetInstalledSDKLocations t = new GetInstalledSDKLocations();
                t.TargetPlatformIdentifier = "Windows";
                t.TargetPlatformVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue).ToString();
                t.SDKRegistryRoot = "Somewhere";
                t.SDKRegistryRoot = "Hello;Jello";
                t.BuildEngine = engine;
                bool success = t.Execute();
                Assert.IsTrue(success);

                ITaskItem[] installedSDKs = t.InstalledSDKs;
                Assert.IsTrue(installedSDKs.Length == 6);

                Dictionary<string, string> extensionSDKs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ITaskItem item in installedSDKs)
                {
                    extensionSDKs.Add(item.GetMetadata("SDKName"), item.ItemSpec);
                }

                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(s_fakeSDKStructureRoot, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=2.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=2.0"].Equals(Path.Combine(s_fakeSDKStructureRoot, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=3.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=3.0"].Equals(Path.Combine(s_fakeSDKStructureRoot, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0\\"), StringComparison.OrdinalIgnoreCase));

                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=4.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=4.0"].Equals(Path.Combine(s_fakeSDKStructureRoot2, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\4.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=5.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=5.0"].Equals(Path.Combine(s_fakeSDKStructureRoot2, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\5.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=6.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=6.0"].Equals(Path.Combine(s_fakeSDKStructureRoot2, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\6.0\\"), StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", null);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            }
        }

        /// <summary>
        /// Get a good set of SDKS installed on the machine from the fake SDK location.
        /// </summary>
        [TestMethod]
        public void GetGoodSDKs2()
        {
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

                MockEngine engine = new MockEngine();
                GetInstalledSDKLocations t = new GetInstalledSDKLocations();
                t.TargetPlatformIdentifier = "Windows";
                t.TargetPlatformVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue).ToString();
                t.BuildEngine = engine;
                t.SDKRegistryRoot = String.Empty;
                t.SDKDirectoryRoots = new string[] { s_fakeSDKStructureRoot, s_fakeSDKStructureRoot2 };
                bool success = t.Execute();
                Assert.IsTrue(success);

                ITaskItem[] installedSDKs = t.InstalledSDKs;
                Assert.IsTrue(installedSDKs.Length == 6);

                Dictionary<string, string> extensionSDKs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ITaskItem item in installedSDKs)
                {
                    extensionSDKs.Add(item.GetMetadata("SDKName"), item.ItemSpec);
                }

                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(s_fakeSDKStructureRoot, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=2.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=2.0"].Equals(Path.Combine(s_fakeSDKStructureRoot, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=3.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=3.0"].Equals(Path.Combine(s_fakeSDKStructureRoot, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0\\"), StringComparison.OrdinalIgnoreCase));

                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=4.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=4.0"].Equals(Path.Combine(s_fakeSDKStructureRoot2, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\4.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=5.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=5.0"].Equals(Path.Combine(s_fakeSDKStructureRoot2, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\5.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(extensionSDKs.ContainsKey("MyAssembly, Version=6.0"));
                Assert.IsTrue(extensionSDKs["MyAssembly, Version=6.0"].Equals(Path.Combine(s_fakeSDKStructureRoot2, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\6.0\\"), StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            }
        }

        /// <summary>
        /// Make a fake SDK structure on disk for testing.
        /// </summary>
        private static string MakeFakeSDKStructure()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "FakeSDKDirectory");
            try
            {
                // Good
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0"));
                File.WriteAllText(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0", "sdkmanifest.xml"), "Hello");

                //Bad because of v in the sdk version
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\FluterShy\\v1.1"));

                //Bad because no extensionSDKs directory under the platform version
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\v3.0\\"));

                // Bad because the directory under the identifier is not a version
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\NotAVersion\\"));

                // Bad because the directory under the identifier is not a version
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\NotAVersion\\ExtensionSDKs\\Assembly\\1.0"));

                // Good but are in a different target platform 
                // Doors does not have an sdk manifest but does have extensionsdks under it so they should be found
                // when we are targeting doors
                Directory.CreateDirectory(Path.Combine(tempPath, "Doors\\2.0\\ExtensionSDKs\\MyAssembly\\3.0"));
                File.WriteAllText(Path.Combine(tempPath, "Doors\\2.0\\ExtensionSDKs\\MyAssembly\\3.0\\", "sdkmanifest.xml"), "Hello");

                // Walls has an SDK manifest so it should be found when looking for targetplatform sdks.
                // But it has no extensionSDKs so none should be found
                Directory.CreateDirectory(Path.Combine(tempPath, "Walls\\1.0\\"));
                File.WriteAllText(Path.Combine(tempPath, "Walls\\1.0\\", "sdkmanifest.xml"), "Hello");
            }
            catch (Exception)
            {
                FileUtilities.DeleteDirectoryNoThrow(tempPath, true);
                return null;
            }

            return tempPath;
        }

        /// <summary>
        /// Make a fake SDK structure on disk for testing.
        /// </summary>
        private static string MakeFakeSDKStructure2()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "FakeSDKDirectory2");
            try
            {
                // Good
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\4.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\5.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\6.0"));
                File.WriteAllText(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\4.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\5.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\6.0", "sdkmanifest.xml"), "Hello");
            }
            catch (Exception)
            {
                FileUtilities.DeleteDirectoryNoThrow(tempPath, true);
                return null;
            }

            return tempPath;
        }
        #endregion
    }
}
