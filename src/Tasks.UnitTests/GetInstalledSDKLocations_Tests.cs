// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Evaluation;
using System.Linq;
using Microsoft.Build.Execution;
using Xunit;
using Microsoft.Build.Tasks;

namespace Microsoft.Build.UnitTests.GetInstalledSDKLocation_Tests
{
    public class FakeSDKStructure : IDisposable
    {
        public string FakeSdkStructureRoot { get; }
        public string FakeSdkStructureRoot2 { get; }

        public FakeSDKStructure()
        {
            FakeSdkStructureRoot = FakeSDKStructure.MakeFakeSDKStructure();
            FakeSdkStructureRoot2 = FakeSDKStructure.MakeFakeSDKStructure2();
        }

        public void Dispose()
        {
            if (FileUtilities.DirectoryExistsNoThrow(FakeSdkStructureRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(FakeSdkStructureRoot, true);
            }

            if (FileUtilities.DirectoryExistsNoThrow(FakeSdkStructureRoot2))
            {
                FileUtilities.DeleteDirectoryNoThrow(FakeSdkStructureRoot2, true);
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
                Directory.CreateDirectory(
                    Path.Combine(new[] { tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0" }));
                Directory.CreateDirectory(
                    Path.Combine(new[] { tempPath, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0" }));
                Directory.CreateDirectory(
                    Path.Combine(new[] { tempPath, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "3.0" }));
                File.WriteAllText(
                    Path.Combine(
                        new[] { tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0", "SDKManifest.xml" }),
                    "Hello");
                File.WriteAllText(
                    Path.Combine(
                        new[] { tempPath, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0", "SDKManifest.xml" }),
                    "Hello");
                File.WriteAllText(
                    Path.Combine(
                        new[] { tempPath, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "3.0", "SDKManifest.xml" }),
                    "Hello");

                //Bad because of v in the sdk version
                Directory.CreateDirectory(
                    Path.Combine(new[] { tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "v1.1" }));

                //Bad because no extensionSDKs directory under the platform version
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows", "v3.0") + Path.DirectorySeparatorChar);

                // Bad because the directory under the identifier is not a version
                Directory.CreateDirectory(
                    Path.Combine(tempPath, "Windows", "NotAVersion") + Path.DirectorySeparatorChar);

                // Bad because the directory under the identifier is not a version
                Directory.CreateDirectory(
                    Path.Combine(
                        new[] { tempPath, "Windows", "NotAVersion", "ExtensionSDKs", "Assembly", "1.0" }));

                // Good but are in a different target platform 
                // Doors does not have an sdk manifest but does have extensionsdks under it so they should be found
                // when we are targeting doors
                Directory.CreateDirectory(
                    Path.Combine(new[] { tempPath, "Doors", "2.0", "ExtensionSDKs", "MyAssembly", "3.0" }));
                File.WriteAllText(
                    Path.Combine(
                        new[] { tempPath, "Doors", "2.0", "ExtensionSDKs", "MyAssembly", "3.0", "SDKManifest.xml" }),
                    "Hello");

                // Walls has an SDK manifest so it should be found when looking for targetplatform sdks.
                // But it has no extensionSDKs so none should be found
                Directory.CreateDirectory(Path.Combine(tempPath, "Walls" + Path.DirectorySeparatorChar + "1.0" + Path.DirectorySeparatorChar));
                File.WriteAllText(Path.Combine(tempPath, "Walls", "1.0", "SDKManifest.xml"), "Hello");
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
                Directory.CreateDirectory(
                    Path.Combine(new[] { tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "4.0" }));
                Directory.CreateDirectory(
                    Path.Combine(new[] { tempPath, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "5.0" }));
                Directory.CreateDirectory(
                    Path.Combine(new[] { tempPath, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "6.0" }));
                File.WriteAllText(
                    Path.Combine(
                        new[] { tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "4.0", "SDKManifest.xml" }),
                    "Hello");
                File.WriteAllText(
                    Path.Combine(
                        new[] { tempPath, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "5.0", "SDKManifest.xml" }),
                    "Hello");
                File.WriteAllText(
                    Path.Combine(
                        new[] { tempPath, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "6.0", "SDKManifest.xml" }),
                    "Hello");
            }
            catch (Exception)
            {
                FileUtilities.DeleteDirectoryNoThrow(tempPath, true);
                return null;
            }

            return tempPath;
        }
    }

    /// <summary>
    /// Test the GetInstalledSDKLocations task
    /// </summary>
    public class GetInstalledSDKLocationsTestFixture : IClassFixture<FakeSDKStructure>
    {
        private readonly string _fakeSDKStructureRoot;
        private readonly string _fakeSDKStructureRoot2;

        public GetInstalledSDKLocationsTestFixture(FakeSDKStructure fakeSDKStructure)
        {
            _fakeSDKStructureRoot = fakeSDKStructure.FakeSdkStructureRoot;
            _fakeSDKStructureRoot2 = fakeSDKStructure.FakeSdkStructureRoot2;
        }

        #region TestMethods
        /// <summary>
        /// Make sure we get a ArgumentException if null is passed into the target platform version.
        /// </summary>
        [Fact]
        public void NullTargetPlatformVersion()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                GetInstalledSDKLocations t = new GetInstalledSDKLocations();
                t.TargetPlatformIdentifier = "Hello";
                t.TargetPlatformVersion = null;
                t.Execute();
            }
           );
        }
        /// <summary>
        /// Make sure we get a ArgumentException if null is passed into the target platform version.
        /// </summary>
        [Fact]
        public void NullTargetPlatformIdentifier()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                GetInstalledSDKLocations t = new GetInstalledSDKLocations();
                t.TargetPlatformIdentifier = null;
                t.TargetPlatformVersion = "1.0";
                t.Execute();
            }
           );
        }
        /// <summary>
        /// Make sure we get an error message if an empty platform identifier is passed in.
        /// </summary>
        [Fact]
        public void EmptyTargetPlatformIdentifier()
        {
            MockEngine engine = new MockEngine();
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = String.Empty;
            t.TargetPlatformVersion = "1.0";
            t.BuildEngine = engine;
            bool success = t.Execute();
            Assert.False(success);

            Assert.Equal(1, engine.Errors);
            engine.AssertLogContains("MSB3784");
        }

        /// <summary>
        /// Make sure we get an error message if an empty platform Version is passed in.
        /// </summary>
        [Fact]
        public void EmptyTargetPlatformVersion()
        {
            MockEngine engine = new MockEngine();
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = "Hello";
            t.TargetPlatformVersion = String.Empty;
            t.BuildEngine = engine;
            bool success = t.Execute();
            Assert.False(success);


            Assert.Equal(1, engine.Errors);
            engine.AssertLogContains("MSB3784");
        }

        /// <summary>
        /// Make sure we get an error message if an empty platform Version is passed in.
        /// </summary>
        [Fact]
        public void BadTargetPlatformVersion()
        {
            MockEngine engine = new MockEngine();
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = "Hello";
            t.TargetPlatformVersion = "CAT";
            t.BuildEngine = engine;
            bool success = t.Execute();
            Assert.False(success);


            Assert.Equal(1, engine.Errors);
            engine.AssertLogContains("MSB3786");
        }

        /// <summary>
        /// Make sure we get an Warning if no SDKs were found.
        /// </summary>
        [Fact]
        public void NoSDKsFound()
        {
            MockEngine engine = new MockEngine();
            GetInstalledSDKLocations t = new GetInstalledSDKLocations();
            t.TargetPlatformIdentifier = "Hello";
            t.TargetPlatformVersion = "1.0";
            t.BuildEngine = engine;
            bool success = t.Execute();
            Assert.True(success);

            Assert.Equal(1, engine.Warnings);
            engine.AssertLogContains("MSB3785");
        }

        /// <summary>
        /// Get a good set of SDKS installed on the machine from the fake SDK location.
        /// </summary>
        [Fact]
        public void GetSDKVersions()
        {
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", _fakeSDKStructureRoot + ";" + _fakeSDKStructureRoot2);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
                MockEngine engine = new MockEngine();
                GetInstalledSDKLocations t = new GetInstalledSDKLocations();
                t.TargetPlatformIdentifier = "Windows";
                t.TargetPlatformVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue).ToString();
                t.SDKRegistryRoot = "Somewhere";
                t.SDKRegistryRoot = "Hello;Jello";
                t.BuildEngine = engine;
                bool success = t.Execute();
                Assert.True(success);

                ITaskItem[] installedSDKs = t.InstalledSDKs;
                Assert.Equal(6, installedSDKs.Length);

                Dictionary<string, string> sdksAndVersions = new Dictionary<string, string>();

                foreach (ITaskItem item in installedSDKs)
                {
                    sdksAndVersions.Add(item.GetMetadata("SDKName"), item.GetMetadata("PlatformVersion"));
                }

                Assert.Equal("1.0", sdksAndVersions["MyAssembly, Version=1.0"]);
                Assert.Equal("1.0", sdksAndVersions["MyAssembly, Version=2.0"]);
                Assert.Equal("2.0", sdksAndVersions["MyAssembly, Version=3.0"]);
                Assert.Equal("1.0", sdksAndVersions["MyAssembly, Version=4.0"]);
                Assert.Equal("1.0", sdksAndVersions["MyAssembly, Version=5.0"]);
                Assert.Equal("2.0", sdksAndVersions["MyAssembly, Version=6.0"]);

                Assert.False(sdksAndVersions.ContainsValue("3.0"));
                Assert.False(sdksAndVersions.ContainsValue("4.0"));
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
        [Fact]
        public void GetGoodSDKs()
        {
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", _fakeSDKStructureRoot + ";" + _fakeSDKStructureRoot2);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
                MockEngine engine = new MockEngine();
                GetInstalledSDKLocations t = new GetInstalledSDKLocations();
                t.TargetPlatformIdentifier = "Windows";
                t.TargetPlatformVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue).ToString();
                t.SDKRegistryRoot = "Somewhere";
                t.SDKRegistryRoot = "Hello;Jello";
                t.BuildEngine = engine;
                bool success = t.Execute();
                Assert.True(success);

                ITaskItem[] installedSDKs = t.InstalledSDKs;
                Assert.Equal(6, installedSDKs.Length);

                Dictionary<string, string> extensionSDKs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ITaskItem item in installedSDKs)
                {
                    extensionSDKs.Add(item.GetMetadata("SDKName"), item.ItemSpec);
                }

                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=1.0"]);
                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=2.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=2.0"]);
                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=3.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "3.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=3.0"]);

                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=4.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot2, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "4.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=4.0"]);
                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=5.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot2, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "5.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=5.0"]);
                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=6.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot2, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "6.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=6.0"]);
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
        [Fact]
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
                t.SDKDirectoryRoots = new string[] { _fakeSDKStructureRoot, _fakeSDKStructureRoot2 };
                bool success = t.Execute();
                Assert.True(success);

                ITaskItem[] installedSDKs = t.InstalledSDKs;
                Assert.Equal(6, installedSDKs.Length);

                Dictionary<string, string> extensionSDKs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ITaskItem item in installedSDKs)
                {
                    extensionSDKs.Add(item.GetMetadata("SDKName"), item.ItemSpec);
                }

                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "1.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=1.0"]);
                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=2.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "2.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=2.0"]);
                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=3.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "3.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=3.0"]);

                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=4.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot2, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "4.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=4.0"]);
                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=5.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot2, "Windows", "1.0", "ExtensionSDKs", "MyAssembly", "5.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=5.0"]);
                Assert.True(extensionSDKs.ContainsKey("MyAssembly, Version=6.0"));
                Assert.Equal(
                    Path.Combine(
                            new[] { _fakeSDKStructureRoot2, "Windows", "2.0", "ExtensionSDKs", "MyAssembly", "6.0" })
                        + Path.DirectorySeparatorChar,
                    extensionSDKs["MyAssembly, Version=6.0"]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            }
        }

        #endregion
    }
}
