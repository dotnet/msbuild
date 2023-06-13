// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

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
                t._cache.instanceLocalFileStateCache = new Dictionary<string, ResolveAssemblyReferenceCache.FileState>() {
                    { Path.Combine(standardCache.Path, "assembly1"), new ResolveAssemblyReferenceCache.FileState(DateTime.Now) },
                    { Path.Combine(standardCache.Path, "assembly2"), new ResolveAssemblyReferenceCache.FileState(DateTime.Now) { Assembly = new Shared.AssemblyNameExtension("hi") } } };
                t._cache.IsDirty = true;
                t.StateFile = standardCache.Path;
                t.WriteStateFile(p => DateTime.Now);
                int standardLen = File.ReadAllText(standardCache.Path).Length;
                File.Delete(standardCache.Path);
                standardLen.ShouldBeGreaterThan(0);

                string precomputedPath = standardCache.Path + ".cache";
                t._cache.IsDirty = true;
                t.AssemblyInformationCacheOutputPath = precomputedPath;
                t.WriteStateFile(p => DateTime.Now);
                File.Exists(standardCache.Path).ShouldBeFalse();
                int preLen = File.ReadAllText(precomputedPath).Length;
                preLen.ShouldBeGreaterThan(0);
                preLen.ShouldNotBe(standardLen);
            }
        }

        [Fact]
        public void StandardCacheTakesPrecedence()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFile standardCache = env.CreateFile(".cache");
                ResolveAssemblyReference rarWriterTask = new ResolveAssemblyReference()
                {
                    _cache = new SystemState()
                };
                rarWriterTask._cache.instanceLocalFileStateCache = new Dictionary<string, ResolveAssemblyReferenceCache.FileState>();
                rarWriterTask.StateFile = standardCache.Path;
                rarWriterTask._cache.IsDirty = true;
                // Write standard cache
                rarWriterTask.WriteStateFile(p => DateTime.Now);

                string dllName = Path.Combine(Path.GetDirectoryName(standardCache.Path), "randomFolder", "dll.dll");
                rarWriterTask._cache.instanceLocalFileStateCache.Add(dllName,
                    new ResolveAssemblyReferenceCache.FileState(DateTime.Now)
                    {
                        Assembly = null,
                        RuntimeVersion = "v4.0.30319",
                        FrameworkNameAttribute = new System.Runtime.Versioning.FrameworkName(".NETFramework", Version.Parse("4.7.2"), "Profile"),
                        ScatterFiles = new string[] { "first", "second" }
                    });
                string precomputedCachePath = standardCache.Path + ".cache";
                rarWriterTask.AssemblyInformationCacheOutputPath = precomputedCachePath;
                rarWriterTask._cache.IsDirty = true;
                // Write precomputed cache
                rarWriterTask.WriteStateFile(p => DateTime.Now);

                ResolveAssemblyReference rarReaderTask = new ResolveAssemblyReference();
                rarReaderTask.StateFile = standardCache.Path;
                rarReaderTask.AssemblyInformationCachePaths = new ITaskItem[]
                {
                    new TaskItem(precomputedCachePath)
                };

                // At this point, we should have created two cache files: one "normal" one and one "precomputed" one.
                // When we read the state file, it should read from the caches produced in a normal build. In this case,
                // the normal cache does not have dll.dll, whereas the precomputed cache does, so it should not be
                // present when we read it.
                rarReaderTask.ReadStateFile(p => true, p => DateTime.Now);
                rarReaderTask._cache.instanceLocalFileStateCache.ShouldNotContainKey(dllName);
            }
        }

        [Fact]
        public void TestPreComputedCacheInputMatchesOutput()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFile precomputedCache = env.CreateFile(".cache");
                ResolveAssemblyReference rarWriterTask = new ResolveAssemblyReference()
                {
                    _cache = new SystemState()
                };
                string dllName = Path.Combine(Path.GetDirectoryName(precomputedCache.Path), "randomFolder", "dll.dll");
                rarWriterTask._cache.instanceLocalFileStateCache = new Dictionary<string, ResolveAssemblyReferenceCache.FileState>() {
                    { Path.Combine(precomputedCache.Path, "..", "assembly1", "assembly1"), new ResolveAssemblyReferenceCache.FileState(DateTime.Now) },
                    { Path.Combine(precomputedCache.Path, "assembly2"), new ResolveAssemblyReferenceCache.FileState(DateTime.Now) { Assembly = new Shared.AssemblyNameExtension("hi") } },
                    { dllName, new ResolveAssemblyReferenceCache.FileState(DateTime.Now) {
                        Assembly = null,
                        RuntimeVersion = "v4.0.30319",
                        FrameworkNameAttribute = new System.Runtime.Versioning.FrameworkName(".NETFramework", Version.Parse("4.7.2"), "Profile"),
                        ScatterFiles = new string[] { "first", "second" } } } };

                rarWriterTask.AssemblyInformationCacheOutputPath = precomputedCache.Path;
                rarWriterTask._cache.IsDirty = true;

                // Throws an exception because precomputedCache.Path already exists.
                Should.Throw<InvalidOperationException>(() => rarWriterTask.WriteStateFile(p => DateTime.Now));
                File.Delete(precomputedCache.Path);
                rarWriterTask.WriteStateFile(p => DateTime.Now);

                ResolveAssemblyReference rarReaderTask = new ResolveAssemblyReference();
                rarReaderTask.StateFile = precomputedCache.Path.Substring(0, precomputedCache.Path.Length - 6); // Not a real path; should not be used.
                rarReaderTask.AssemblyInformationCachePaths = new ITaskItem[]
                {
                    new TaskItem(precomputedCache.Path)
                };

                // At this point, the standard cache does not exist, so it defaults to reading the "precomputed" cache.
                // Then we verify that the information contained in that cache matches what we'd expect.
                rarReaderTask.ReadStateFile(p => true, p => DateTime.Now);
                rarReaderTask._cache.instanceLocalFileStateCache.ShouldContainKey(dllName);
                ResolveAssemblyReferenceCache.FileState assembly3 = rarReaderTask._cache.instanceLocalFileStateCache[dllName];
                assembly3.Assembly.ShouldBeNull();
                assembly3.RuntimeVersion.ShouldBe("v4.0.30319");
                assembly3.FrameworkNameAttribute.Version.ShouldBe(Version.Parse("4.7.2"));
                assembly3.ScatterFiles.Length.ShouldBe(2);
                assembly3.ScatterFiles[1].ShouldBe("second");
            }
        }
    }
}
