// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class RARPrecomputedCache_Tests
    {
        [Fact]
        public void TestPrecomputedCacheOutput()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFile standardCache = env.CreateFile(".cache");
                ResolveAssemblyReference t = new ResolveAssemblyReference()
                {
                    _cache = new SystemState()
                };
                t._cache.instanceLocalFileStateCache = new Dictionary<string, SystemState.FileState>() {
                    { Path.Combine(standardCache.Path, "assembly1"), new SystemState.FileState(DateTime.Now) },
                    { Path.Combine(standardCache.Path, "assembly2"), new SystemState.FileState(DateTime.Now) { Assembly = new Shared.AssemblyNameExtension("hi") } } };
                t._cache.IsDirty = true;
                t.StateFile = standardCache.Path;
                t.WriteStateFile();
                int standardLen = File.ReadAllText(standardCache.Path).Length;
                File.Delete(standardCache.Path);
                standardLen.ShouldBeGreaterThan(0);

                string precomputedPath = standardCache.Path + ".cache";
                t._cache.IsDirty = true;
                t.AssemblyInformationCacheOutputPath = precomputedPath;
                t.WriteStateFile();
                File.Exists(standardCache.Path).ShouldBeFalse();
                int preLen = File.ReadAllText(precomputedPath).Length;
                preLen.ShouldBeGreaterThan(0);
                preLen.ShouldNotBe(standardLen);
            }
        }

        [Fact]
        public void TestPreComputedCacheInputAndOutput()
        {
            using (TestEnvironment env = TestEnvironment.Create()) {
                TransientTestFile standardCache = env.CreateFile(".cache");
                ResolveAssemblyReference rarWriterTask = new ResolveAssemblyReference()
                {
                    _cache = new SystemState()
                };
                rarWriterTask._cache.instanceLocalFileStateCache = new Dictionary<string, SystemState.FileState>() {
                    { Path.Combine(standardCache.Path, "assembly1"), new SystemState.FileState(DateTime.Now) },
                    { Path.Combine(standardCache.Path, "assembly2"), new SystemState.FileState(DateTime.Now) { Assembly = new Shared.AssemblyNameExtension("hi") } } };
                rarWriterTask.StateFile = standardCache.Path;
                rarWriterTask._cache.IsDirty = true;
                rarWriterTask.WriteStateFile();

                string dllName = Path.Combine(Path.GetDirectoryName(standardCache.Path), "randomFolder", "dll.dll");
                rarWriterTask._cache.instanceLocalFileStateCache.Add(dllName,
                    new SystemState.FileState(DateTime.Now) {
                        Assembly = new Shared.AssemblyNameExtension("notDll.dll", false),
                        RuntimeVersion = "v4.0.30319",
                        FrameworkNameAttribute = new System.Runtime.Versioning.FrameworkName(".NETFramework", Version.Parse("4.7.2"), "Profile"),
                        scatterFiles = new string[] { "first", "second" } });
                rarWriterTask._cache.instanceLocalFileStateCache[dllName].Assembly.Version = new Version("16.3");
                string precomputedCachePath = standardCache.Path + ".cache";
                rarWriterTask.AssemblyInformationCacheOutputPath = precomputedCachePath;
                rarWriterTask._cache.IsDirty = true;
                rarWriterTask.WriteStateFile();
                // The cache is already written; this change should do nothing.
                rarWriterTask._cache.instanceLocalFileStateCache[dllName].Assembly = null;

                ResolveAssemblyReference rarReaderTask = new ResolveAssemblyReference();
                rarReaderTask.StateFile = standardCache.Path;
                rarReaderTask.AssemblyInformationCachePaths = new ITaskItem[]
                {
                    new TaskItem(precomputedCachePath)
                };

                // At this point, we should have created two cache files: one "normal" one and one "precomputed" one.
                // When we read the state file the first time, it should read from the caches produced in a normal
                // build, partially because we can read it faster. If that cache does not exist, as with the second
                // time we try to read the state file, it defaults to reading the "precomputed" cache. In this case,
                // the normal cache does not have dll.dll, whereas the precomputed cache does, so it should not be
                // present when we read the first time but should be present the second time. Then we verify that the
                // information contained in that cache matches what we'd expect.
                rarReaderTask.ReadStateFile(File.GetLastWriteTime, Array.Empty<AssemblyTableInfo>(), p => true);
                rarReaderTask._cache.instanceLocalFileStateCache.ShouldNotContainKey(dllName);
                File.Delete(standardCache.Path);
                rarReaderTask._cache = null;
                rarReaderTask.ReadStateFile(File.GetLastWriteTime, Array.Empty<AssemblyTableInfo>(), p => true);
                rarReaderTask._cache.instanceLocalFileStateCache.ShouldContainKey(dllName);
                SystemState.FileState assembly3 = rarReaderTask._cache.instanceLocalFileStateCache[dllName];
                assembly3.Assembly.FullName.ShouldBe("notDll.dll");
                assembly3.Assembly.Version.Major.ShouldBe(16);
                assembly3.RuntimeVersion.ShouldBe("v4.0.30319");
                assembly3.FrameworkNameAttribute.Version.ShouldBe(Version.Parse("4.7.2"));
                assembly3.scatterFiles.Length.ShouldBe(2);
                assembly3.scatterFiles[1].ShouldBe("second");
            }
        }
    }
}
