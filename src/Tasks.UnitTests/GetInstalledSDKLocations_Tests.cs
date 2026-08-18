// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

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

                // Bad because of v in the sdk version
                Directory.CreateDirectory(
                    Path.Combine(new[] { tempPath, "Windows", "v1.0", "ExtensionSDKs", "MyAssembly", "v1.1" }));

                // Bad because no extensionSDKs directory under the platform version
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
    /// </summary>W
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
        [WindowsOnlyFact]
        public void NullTargetPlatformVersion()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                GetInstalledSDKLocations t = new GetInstalledSDKLocations();
                t.TargetPlatformIdentifier = "Hello";
                t.TargetPlatformVersion = null;
                t.Execute();
            });
        }
        /// <summary>
        /// Make sure we get a ArgumentException if null is passed into the target platform version.
        /// </summary>
        [WindowsOnlyFact]
        public void NullTargetPlatformIdentifier()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                GetInstalledSDKLocations t = new GetInstalledSDKLocations();
                t.TargetPlatformIdentifier = null;
                t.TargetPlatformVersion = "1.0";
                t.Execute();
            });
        }
        /// <summary>
        /// Make sure we get an error message if an empty platform identifier is passed in.
        /// </summary>
        [WindowsOnlyFact]
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
        [WindowsOnlyFact]
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
        [WindowsOnlyFact]
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
        [WindowsOnlyFact]
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
        [WindowsOnlyFact]
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
        [WindowsOnlyFact]
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
        [WindowsOnlyFact]
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

    /// <summary>
    /// Tests for GetInstalledSDKLocations multithreaded task migration.
    /// Verifies interface compliance, path absolutization via TaskEnvironment,
    /// concurrent execution safety, and behavioral equivalence across environments.
    /// </summary>
    public sealed class GetInstalledSDKLocationsMultiThreadTests : IClassFixture<FakeSDKStructure>, IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private readonly string _fakeSDKStructureRoot;

        public GetInstalledSDKLocationsMultiThreadTests(FakeSDKStructure fakeSDKStructure, ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
            _fakeSDKStructureRoot = fakeSDKStructure.FakeSdkStructureRoot;
        }

        public void Dispose() => _env.Dispose();


        /// <summary>
        /// Verifies that relative SDKDirectoryRoots are resolved via TaskEnvironment's project
        /// directory, NOT via the process working directory. Uses two TaskEnvironments pointing
        /// at different project directories with the same relative root name — one finds SDKs
        /// (correct parent) and the other finds none (wrong parent), proving absolutization
        /// goes through TaskEnvironment.
        /// </summary>
        [WindowsOnlyFact]
        public void Execute_WithRelativeDirectoryRoots_AbsolutizesViaTaskEnvironment()
        {
            string parentDir = Path.GetDirectoryName(_fakeSDKStructureRoot);
            string relativeName = Path.GetFileName(_fakeSDKStructureRoot);

            // Set CWD to an unrelated directory so relative paths can't accidentally
            // resolve via process CWD.
            _env.SetCurrentDirectory(Path.GetTempPath());

            // TaskEnvironment pointing at the CORRECT parent — relative root should resolve
            // to the real fake SDK structure.
            var correctEnv = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(
                parentDir,
                new Dictionary<string, string>
                {
                    ["MSBUILDDISABLEREGISTRYFORSDKLOOKUP"] = "true",
                });

            var engineCorrect = new MockEngine(_output);
            var taskCorrect = new GetInstalledSDKLocations
            {
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue).ToString(),
                SDKDirectoryRoots = new[] { relativeName },
                SDKRegistryRoot = String.Empty,
                BuildEngine = engineCorrect,
                TaskEnvironment = correctEnv,
            };
            bool successCorrect = taskCorrect.Execute();

            successCorrect.ShouldBeTrue();
            taskCorrect.InstalledSDKs.ShouldNotBeNull();
            taskCorrect.InstalledSDKs.Length.ShouldBe(3, "Correct project directory should resolve the relative root to the real SDK structure");

            // Output metadata must preserve the original relative value (Sin 1 check).
            foreach (ITaskItem item in taskCorrect.InstalledSDKs)
            {
                item.GetMetadata("DirectoryRoots").ShouldBe(relativeName);
            }

            // TaskEnvironment pointing at the WRONG parent — same relative name resolves
            // to a non-existent directory, proving resolution goes through TaskEnvironment.
            string wrongParent = _env.CreateFolder().Path;
            var wrongEnv = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(
                wrongParent,
                new Dictionary<string, string>
                {
                    ["MSBUILDDISABLEREGISTRYFORSDKLOOKUP"] = "true",
                });

            var engineWrong = new MockEngine(_output);
            var taskWrong = new GetInstalledSDKLocations
            {
                TargetPlatformIdentifier = "Windows",
                TargetPlatformVersion = new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue).ToString(),
                SDKDirectoryRoots = new[] { relativeName },
                SDKRegistryRoot = String.Empty,
                BuildEngine = engineWrong,
                TaskEnvironment = wrongEnv,
            };
            bool successWrong = taskWrong.Execute();

            successWrong.ShouldBeTrue();
            taskWrong.InstalledSDKs.ShouldNotBeNull();
            taskWrong.InstalledSDKs.Length.ShouldBe(0, "Wrong project directory should not find any SDKs");
        }
    }
}
