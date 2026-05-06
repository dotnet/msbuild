// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    public class ResolveAssemblyReferenceCacheSerialization : IDisposable
    {
        private readonly string _rarCacheFile;
        private readonly TaskLoggingHelper _taskLoggingHelper;

        private static readonly DateTime s_now = DateTime.Now;

        public ResolveAssemblyReferenceCacheSerialization()
        {
            var tempPath = Path.GetTempPath();
            _rarCacheFile = Path.Combine(tempPath, Guid.NewGuid() + ".UnitTest.RarCache");
            _taskLoggingHelper = new TaskLoggingHelper(new MockEngine(), "TaskA")
            {
                TaskResources = AssemblyResources.PrimaryResources
            };
        }

        public void Dispose()
        {
            if (File.Exists(_rarCacheFile))
            {
                FileUtilities.DeleteNoThrow(_rarCacheFile);
            }
        }

        private static DateTime GetLastWriteTime(string path) => path switch
        {
            "path1" => s_now,
            "path2" => s_now,
            "dllName" => s_now.AddSeconds(-10),
            _ => throw new ArgumentException(),
        };

        [Fact]
        public void RoundTripEmptyState()
        {
            SystemState systemState = new();

            systemState.SerializeCache(_rarCacheFile, _taskLoggingHelper, serializeEmptyState: true);

            var deserialized = StateFileBase.DeserializeCache<SystemState>(_rarCacheFile, _taskLoggingHelper);

            deserialized.ShouldNotBeNull();
        }

        [Fact]
        public void CorrectFileVersion()
        {
            SystemState systemState = new();

            systemState.SerializeCache(_rarCacheFile, _taskLoggingHelper, serializeEmptyState: true);
            using (var cacheStream = new FileStream(_rarCacheFile, FileMode.Open, FileAccess.ReadWrite))
            {
                cacheStream.Seek(0, SeekOrigin.Begin);
                cacheStream.WriteByte(StateFileBase.CurrentSerializationVersion);
                cacheStream.Close();
            }

            var deserialized = StateFileBase.DeserializeCache<SystemState>(_rarCacheFile, _taskLoggingHelper);

            deserialized.ShouldNotBeNull();
        }

        [Fact]
        public void WrongFileVersion()
        {
            SystemState systemState = new();

            systemState.SerializeCache(_rarCacheFile, _taskLoggingHelper, serializeEmptyState: true);
            using (var cacheStream = new FileStream(_rarCacheFile, FileMode.Open, FileAccess.ReadWrite))
            {
                cacheStream.Seek(0, SeekOrigin.Begin);
                cacheStream.WriteByte(StateFileBase.CurrentSerializationVersion - 1);
                cacheStream.Close();
            }

            var deserialized = StateFileBase.DeserializeCache<SystemState>(_rarCacheFile, _taskLoggingHelper);

            deserialized.ShouldBeNull();
        }

        [Fact]
        public void ValidateSerializationAndDeserialization()
        {
            Dictionary<string, SystemState.FileState> cache = new() {
                    { "path1", new SystemState.FileState(GetLastWriteTime("path1")) },
                    { "path2", new SystemState.FileState(GetLastWriteTime("path2")) { Assembly = new AssemblyNameExtension("hi") } },
                    { "dllName", new SystemState.FileState(GetLastWriteTime("dllName")) {
                        Assembly = null,
                        RuntimeVersion = "v4.0.30319",
                        FrameworkNameAttribute = new FrameworkName(".NETFramework", Version.Parse("4.7.2"), "Profile"),
                        scatterFiles = new string[] { "first", "second" } } } };
            SystemState sysState = new();
            sysState.SetGetLastWriteTime(GetLastWriteTime);
            sysState.instanceLocalFileStateCache = cache;

            // Get all FileState entries to make sure they are marked as having been used.
            _ = sysState.GetFileState("path1");
            _ = sysState.GetFileState("path2");
            _ = sysState.GetFileState("dllName");

            sysState.HasStateToSave.ShouldBe(true);

            SystemState sysState2 = null;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFile file = env.CreateFile();
                sysState.SerializeCache(file.Path, null);
                sysState2 = StateFileBase.DeserializeCache<SystemState>(file.Path, null);
            }

            Dictionary<string, SystemState.FileState> cache2 = sysState2.instanceLocalFileStateCache;
            cache2.Count.ShouldBe(cache.Count);
            cache2["path2"].Assembly.Name.ShouldBe(cache["path2"].Assembly.Name);
            SystemState.FileState dll = cache["dllName"];
            SystemState.FileState dll2 = cache2["dllName"];
            dll2.Assembly.ShouldBe(dll.Assembly);
            dll2.FrameworkNameAttribute.FullName.ShouldBe(dll.FrameworkNameAttribute.FullName);
            dll2.LastModified.ShouldBe(dll.LastModified);
            dll2.RuntimeVersion.ShouldBe(dll.RuntimeVersion);
            dll2.scatterFiles.Length.ShouldBe(dll.scatterFiles.Length);
            dll2.scatterFiles[1].ShouldBe(dll.scatterFiles[1]);
        }

        [Fact]
        public void OutgoingCacheIsSmallerThanIncomingCache()
        {
            Dictionary<string, SystemState.FileState> cache = new() {
                    { "path1", new SystemState.FileState(GetLastWriteTime("path1")) },
                    { "path2", new SystemState.FileState(GetLastWriteTime("path2")) } };
            SystemState sysState = new();
            sysState.SetGetLastWriteTime(GetLastWriteTime);
            sysState.instanceLocalFileStateCache = cache;

            // Get only the first FileState entry.
            _ = sysState.GetFileState("path1");

            sysState.HasStateToSave.ShouldBe(true);

            SystemState sysState2 = null;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFile file = env.CreateFile();
                sysState.SerializeCache(file.Path, null);
                sysState2 = StateFileBase.DeserializeCache<SystemState>(file.Path, null);
            }

            // The new cache has only the entry that was actually used.
            Dictionary<string, SystemState.FileState> cache2 = sysState2.instanceLocalFileStateCache;
            cache2.Count.ShouldBe(1);
            cache2.ShouldContainKey("path1");
        }

        [Fact]
        public void OutgoingCacheIsEmpty()
        {
            Dictionary<string, SystemState.FileState> cache = new() {
                    { "path1", new SystemState.FileState(GetLastWriteTime("path1")) },
                    { "path2", new SystemState.FileState(GetLastWriteTime("path2")) } };
            SystemState sysState = new();
            sysState.SetGetLastWriteTime(GetLastWriteTime);
            sysState.instanceLocalFileStateCache = cache;

            sysState.HasStateToSave.ShouldBe(false);

            SystemState sysState2 = null;
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFile file = env.CreateFile();
                sysState.SerializeCache(file.Path, null);
                sysState2 = StateFileBase.DeserializeCache<SystemState>(file.Path, null);
            }

            // The new cache was not written to disk at all because none of the entries were actually used.
            sysState2.ShouldBeNull();
        }

        /// <summary>
        /// SystemState.DeserializePrecomputedCaches absolutizes the state file path via TaskEnvironment
        /// (relative to the project directory), not via the process current working directory.
        /// A relative state file path that exists in CWD but not in the project directory should
        /// fail to deserialize.
        /// </summary>
        [Fact]
        public void DeserializePrecomputedCaches_AbsolutizesStateFilePathViaTaskEnvironment()
        {
            using TestEnvironment env = TestEnvironment.Create();

            // cacheDir holds the real cache file (and becomes CWD); projectDir is empty.
            string cacheDir = env.CreateFolder().Path;
            string projectDir = env.CreateFolder().Path;
            string cacheFileName = "rar.cache";
            string cacheFullPath = Path.Combine(cacheDir, cacheFileName);

            WriteCacheFileWithSingleEntry(cacheFullPath, "TestAssembly.dll");

            env.SetCurrentDirectory(cacheDir);

            ITaskItem[] stateFiles = [new TaskItem(cacheFileName)];
            TaskEnvironment taskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);

            SystemState result = SystemState.DeserializePrecomputedCaches(
                stateFiles,
                null,
                _ => true,
                taskEnvironment);

            // The relative path is resolved via TaskEnvironment to projectDir/rar.cache, which doesn't
            // exist. If the implementation incorrectly used CWD, it would have found the cache and
            // populated entries.
            result.ShouldNotBeNull();
            result.instanceLocalFileStateCache.ShouldBeEmpty();
        }

        /// <summary>
        /// With Wave18_8 disabled, SystemState.DeserializePrecomputedCaches uses the raw
        /// stateFile.ToString() path which means a relative state file path is opened relative
        /// to the process current working directory — NOT relative to TaskEnvironment.ProjectDirectory.
        /// </summary>
        [Fact]
        public void DeserializePrecomputedCaches_Wave18_8_Disabled_UsesRawPathRelativeToCwd()
        {
            using TestEnvironment env = TestEnvironment.Create();

            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_8.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            try
            {
                // cacheDir holds the real cache file (and becomes CWD); projectDir is empty.
                string cacheDir = env.CreateFolder().Path;
                string projectDir = env.CreateFolder().Path;
                string cacheFileName = "rar.cache";
                string cacheFullPath = Path.Combine(cacheDir, cacheFileName);

                WriteCacheFileWithSingleEntry(cacheFullPath, "TestAssembly.dll");

                env.SetCurrentDirectory(cacheDir);

                ITaskItem[] stateFiles = [new TaskItem(cacheFileName)];
                TaskEnvironment taskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir);

                SystemState result = SystemState.DeserializePrecomputedCaches(
                    stateFiles,
                    null,
                    _ => true,
                    taskEnvironment);

                // Under wave-off, the bare relative path is resolved via CWD (= cacheDir),
                // where the cache exists, so deserialization succeeded and the entry was processed.
                result.ShouldNotBeNull();
                result.instanceLocalFileStateCache.ShouldNotBeEmpty();
            }
            finally
            {
                ChangeWaves.ResetStateForTests();
            }
        }

        /// <summary>
        /// Writes a serialized SystemState cache with a single entry to <paramref name="cacheFullPath"/>.
        /// </summary>
        private static void WriteCacheFileWithSingleEntry(string cacheFullPath, string relativeKey)
        {
            SystemState sysState = new();
            sysState.instanceLocalOutgoingFileStateCache[relativeKey] = new SystemState.FileState(DateTime.UtcNow);
            sysState.SerializeCache(cacheFullPath, log: null);
        }
    }
}
