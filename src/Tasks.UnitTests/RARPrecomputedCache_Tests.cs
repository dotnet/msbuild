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
        public void TestPrecomputedCache()
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
                File.ReadAllText(standardCache.Path).Length.ShouldBeGreaterThan(0);

                TransientTestFile precomputedCache = env.CreateFile(".cache");
            }


            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Regular"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\SystemRuntime\Regular.dll");


            // build mode
            t.FindDependencies = true;
            Assert.True(
                t.Execute
                (
                    fileExists,
                    directoryExists,
                    getDirectories,
                    getAssemblyName,
                    getAssemblyMetadata,
#if FEATURE_WIN32_REGISTRY
                    getRegistrySubKeyNames,
                    getRegistrySubKeyDefaultValue,
#endif
                    getLastWriteTime,
                    getRuntimeVersion,
#if FEATURE_WIN32_REGISTRY
                    openBaseKey,
#endif
                    checkIfAssemblyIsInGac,
                    isWinMDFile,
                    readMachineTypeFromPEHeader
                )
            );
        }
    }
}
