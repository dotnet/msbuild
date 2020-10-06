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
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t._cache = new SystemState();
                t._cache.instanceLocalFileStateCache = new Dictionary<string, SystemState.FileState>() {
                    { "assembly1", new SystemState.FileState(DateTime.Now) },
                    { "assembly2", new SystemState.FileState(DateTime.Now) { Assembly = new Shared.AssemblyNameExtension("hi") } } };
                TransientTestFile standardCache = env.CreateFile(".cache");
                t.StateFile = standardCache.Path;
                t.WriteStateFile();
                int standardLen = File.ReadAllText(standardCache.Path).Length;
                File.Delete(standardCache.Path);
                standardLen.ShouldBeGreaterThan(0);

                TransientTestFile precomputedCache = env.CreateFile(standardCache.Path + ".cache", string.Empty);
                t.AssemblyInformationCacheOutputPath = precomputedCache.Path;
                t.WriteStateFile();
                File.Exists(standardCache.Path).ShouldBeFalse();
                int preLen = File.ReadAllText(precomputedCache.Path).Length;
                preLen.ShouldBeGreaterThan(0);
                preLen.ShouldNotBe(standardLen);
            }
        }

        [Fact]
        public void TestPreComputedCacheInputAndOutput()
        {
            using (TestEnvironment env = TestEnvironment.Create()) {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t._cache = new SystemState();
                t._cache.instanceLocalFileStateCache = new Dictionary<string, SystemState.FileState>() {
                    { "assembly1", new SystemState.FileState(DateTime.Now) },
                    { "assembly2", new SystemState.FileState(DateTime.Now) { Assembly = new Shared.AssemblyNameExtension("hi") } } };
                TransientTestFile standardCache = env.CreateFile(".cache");
                t.StateFile = standardCache.Path;
                t.WriteStateFile();

                t._cache.instanceLocalFileStateCache.Add("..\\.nuget\\packages\\system.text.encodings.web\\4.7.0\\lib\\netstandard2.0\\System.Text.Encodings.Web.dll",
                    new SystemState.FileState(DateTime.Now) {
                        Assembly = null,
                        RuntimeVersion = "v4.0.30319",
                        FrameworkNameAttribute = new System.Runtime.Versioning.FrameworkName(".NETFramework", Version.Parse("4.7.2"), "Profile"),
                        scatterFiles = new string[] { "first", "second" } });
                TransientTestFile precomputedCache = env.CreateFile(standardCache.Path + ".cache", string.Empty);
                t.AssemblyInformationCacheOutputPath = precomputedCache.Path;
                t.WriteStateFile();

                ResolveAssemblyReference u = new ResolveAssemblyReference();
                u.StateFile = standardCache.Path;
                u.AssemblyInformationCachePaths = new ITaskItem[]
                {
                    new TaskItem(precomputedCache.Path)
                };

                u.ReadStateFile(File.GetLastWriteTime, Array.Empty<AssemblyTableInfo>());
                u._cache.instanceLocalFileStateCache.ShouldNotContainKey("..\\.nuget\\packages\\system.text.encodings.web\\4.7.0\\lib\\netstandard2.0\\System.Text.Encodings.Web.dll");
                File.Delete(standardCache.Path);
                u.ReadStateFile(File.GetLastWriteTime, Array.Empty<AssemblyTableInfo>());
                u._cache.instanceLocalFileStateCache.ShouldContainKey("..\\.nuget\\packages\\system.text.encodings.web\\4.7.0\\lib\\netstandard2.0\\System.Text.Encodings.Web.dll");
                SystemState.FileState a3 = u._cache.instanceLocalFileStateCache["..\\.nuget\\packages\\system.text.encodings.web\\4.7.0\\lib\\netstandard2.0\\System.Text.Encodings.Web.dll"];
                a3.Assembly.ShouldBeNull();
                a3.RuntimeVersion.ShouldBe("v4.0.30319");
                a3.FrameworkNameAttribute.Version.ShouldBe(Version.Parse("4.7.2"));
                a3.scatterFiles.Length.ShouldBe(2);
                a3.scatterFiles[1].ShouldBe("second");
            }
        }
    }
}
