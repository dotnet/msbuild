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
        private Dictionary<string, Guid> guidStore = new Dictionary<string, Guid>();

        private Guid calculateMvid(string path)
        {
            if (!guidStore.ContainsKey(path))
            {
                guidStore.Add(path, Guid.NewGuid());
            }
            return guidStore[path];
        }

        [Fact]
        public void TestPrecomputedCacheOutput()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFile standardCache = env.CreateFile(".cache");
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t._cache = new SystemState();
                t._cache.instanceLocalFileStateCache = new Dictionary<string, SystemState.FileState>() {
                    { Path.Combine(standardCache.Path, "assembly1"), new SystemState.FileState(DateTime.Now) },
                    { Path.Combine(standardCache.Path, "assembly2"), new SystemState.FileState(DateTime.Now) { Assembly = new Shared.AssemblyNameExtension("hi") } } };
                t._cache.IsDirty = true;
                t.StateFile = standardCache.Path;
                t.WriteStateFile(calculateMvid);
                int standardLen = File.ReadAllText(standardCache.Path).Length;
                File.Delete(standardCache.Path);
                standardLen.ShouldBeGreaterThan(0);

                string precomputedPath = standardCache.Path + ".cache";
                t._cache.IsDirty = true;
                t.AssemblyInformationCacheOutputPath = precomputedPath;
                t.WriteStateFile(calculateMvid);
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
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t._cache = new SystemState();
                t._cache.instanceLocalFileStateCache = new Dictionary<string, SystemState.FileState>() {
                    { Path.Combine(standardCache.Path, "assembly1"), new SystemState.FileState(DateTime.Now) },
                    { Path.Combine(standardCache.Path, "assembly2"), new SystemState.FileState(DateTime.Now) { Assembly = new Shared.AssemblyNameExtension("hi") } } };
                t.StateFile = standardCache.Path;
                t._cache.IsDirty = true;
                t.WriteStateFile(calculateMvid);

                string dllName = Path.Combine(Path.GetDirectoryName(standardCache.Path), "randomFolder", "dll.dll");
                t._cache.instanceLocalFileStateCache.Add(dllName,
                    new SystemState.FileState(DateTime.Now) {
                        Assembly = new Shared.AssemblyNameExtension("notDll.dll", false),
                        RuntimeVersion = "v4.0.30319",
                        FrameworkNameAttribute = new System.Runtime.Versioning.FrameworkName(".NETFramework", Version.Parse("4.7.2"), "Profile"),
                        scatterFiles = new string[] { "first", "second" } });
                t._cache.instanceLocalFileStateCache[dllName].Assembly.Version = new Version("16.3");
                string precomputedCachePath = standardCache.Path + ".cache";
                t.AssemblyInformationCacheOutputPath = precomputedCachePath;
                t._cache.IsDirty = true;
                t.WriteStateFile(calculateMvid);
                // The cache is already written; this change should do nothing.
                t._cache.instanceLocalFileStateCache[dllName].Assembly = null;

                ResolveAssemblyReference u = new ResolveAssemblyReference();
                u.StateFile = standardCache.Path;
                u.AssemblyInformationCachePaths = new ITaskItem[]
                {
                    new TaskItem(precomputedCachePath)
                };

                u.ReadStateFile(File.GetLastWriteTime, Array.Empty<AssemblyTableInfo>(), calculateMvid, p => true);
                u._cache.instanceLocalFileStateCache.ShouldNotContainKey(dllName);
                File.Delete(standardCache.Path);
                u._cache = null;
                u.ReadStateFile(File.GetLastWriteTime, Array.Empty<AssemblyTableInfo>(), calculateMvid, p => true);
                u._cache.instanceLocalFileStateCache.ShouldContainKey(dllName);
                SystemState.FileState a3 = u._cache.instanceLocalFileStateCache[dllName];
                a3.Assembly.FullName.ShouldBe("notDll.dll");
                a3.Assembly.Version.Major.ShouldBe(16);
                a3.RuntimeVersion.ShouldBe("v4.0.30319");
                a3.FrameworkNameAttribute.Version.ShouldBe(Version.Parse("4.7.2"));
                a3.scatterFiles.Length.ShouldBe(2);
                a3.scatterFiles[1].ShouldBe("second");
            }
        }
    }
}
