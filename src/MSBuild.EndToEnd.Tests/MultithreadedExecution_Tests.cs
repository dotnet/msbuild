// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.EndToEndTests
{
    /// <summary>
    /// Tests for multithreaded MSBuild execution scenarios using test assets.
    /// </summary>
    public class MultithreadedExecution_Tests : IClassFixture<TestSolutionAssetsFixture>, IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private readonly string _testAssetDir;

        private readonly int _timeoutInMilliseconds = 60_000;

        // Common parameters for all multithreaded tests:
        // /nodereuse:false - Prevents MSBuild server processes from persisting between tests,
        //                    ensuring proper test isolation and avoiding potential timeouts
        // /v:minimal - Reduces log verbosity for cleaner test output and better performance
        private const string CommonMSBuildArgs = "/nodereuse:false /v:minimal";

        public MultithreadedExecution_Tests(ITestOutputHelper output, TestSolutionAssetsFixture testAssetFixture)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
            _testAssetDir = testAssetFixture.TestAssetDir;
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        /// <summary>
        /// Prepares an isolated copy of test assets in a temporary directory for each test run.
        /// This ensures fresh builds and proper test isolation.
        /// </summary>
        /// <param name="testAsset">Test asset</param>
        /// <returns>TestSolutionAsset for the copied asset in a temporary folder.</returns>
        private TestSolutionAsset PrepareIsolatedTestAssets(TestSolutionAsset testAsset)
        {
            string sourceAssetDir = Path.Combine(_testAssetDir, testAsset.SolutionFolder);
            
            // Ensure source test asset exists
            Directory.Exists(sourceAssetDir).ShouldBeTrue($"Test asset not found: {sourceAssetDir}.");

            // Create isolated copy of entire test asset directory structure
            TransientTestFolder workFolder = _env.CreateFolder(createFolder: true);
            
            FileSystemUtilities.CopyFilesRecursively(sourceAssetDir, workFolder.Path);
            
            // Return TestSolutionAsset with temp folder and project file
            return new TestSolutionAsset(workFolder.Path, testAsset.ProjectRelativePath);
        }
        
        /// <summary>
        /// Helper method to resolve TestSolutionAsset instances by name.
        /// This is the easiest way to work around the limitation that [InlineData] cannot pass complex objects like TestSolutionAsset directly.
        /// </summary>
        private static TestSolutionAsset GetTestAssetByName(string testAssetName)
        {
            return testAssetName switch
            {
                nameof(TestSolutionAssetsFixture.SingleProject) => TestSolutionAssetsFixture.SingleProject,
                nameof(TestSolutionAssetsFixture.ProjectWithDependencies) => TestSolutionAssetsFixture.ProjectWithDependencies,
                _ => throw new ArgumentException($"Unknown test asset name: {testAssetName}", nameof(testAssetName))
            };
        }

        /// <summary>
        /// Tests building projects with various multithreading flags.
        /// </summary>
        [Theory]
        [InlineData(nameof(TestSolutionAssetsFixture.SingleProject), "/m:1 /mt")]
        [InlineData(nameof(TestSolutionAssetsFixture.SingleProject), "/m:8 /mt")]
        [InlineData(nameof(TestSolutionAssetsFixture.ProjectWithDependencies), "/m:1 /mt")]
        [InlineData(nameof(TestSolutionAssetsFixture.ProjectWithDependencies), "/m:2 /mt")]
        [InlineData(nameof(TestSolutionAssetsFixture.ProjectWithDependencies), "/m:8 /mt")]
        public void MultithreadedBuild_Success(string testAssetName, string multithreadingArgs)
        {
            // Resolve TestSolutionAsset from name
            TestSolutionAsset testAsset = GetTestAssetByName(testAssetName);
            
            // Prepare isolated copy of test assets to ensure fresh builds
            TestSolutionAsset isolatedAsset = PrepareIsolatedTestAssets(testAsset);

            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                $"\"{isolatedAsset.ProjectPath}\" {multithreadingArgs} {CommonMSBuildArgs}", 
                out bool success, 
                timeoutMilliseconds: _timeoutInMilliseconds);

            success.ShouldBeTrue($"Build failed with args '{multithreadingArgs}' for {testAsset.SolutionFolder}. Output:\\n{output}");
            
            _output.WriteLine($"Built {testAsset.SolutionFolder} with arguments {multithreadingArgs}.");
        }

        /// <summary>
        /// Tests binary logging with multithreaded builds and verifies replay functionality.
        /// </summary>
        [Theory]
        [InlineData(nameof(TestSolutionAssetsFixture.SingleProject), "/m:8 /mt")]
        public void MultithreadedBuild_BinaryLogging(string testAssetName, string multithreadingArgs)
        {
            // Resolve TestSolutionAsset from name
            TestSolutionAsset testAsset = GetTestAssetByName(testAssetName);
            
            // Prepare isolated copy of test assets to ensure fresh builds
            TestSolutionAsset isolatedAsset = PrepareIsolatedTestAssets(testAsset);
            
            string binlogPath = Path.Combine(isolatedAsset.SolutionFolder, "build.binlog");

            // Build with binary logging
            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                $"\"{isolatedAsset.ProjectPath}\" {multithreadingArgs} /bl:\"{binlogPath}\" {CommonMSBuildArgs}", 
                out bool success, 
                timeoutMilliseconds: _timeoutInMilliseconds);

            success.ShouldBeTrue($"Build failed with args '{multithreadingArgs}' for {testAsset.SolutionFolder}. Output:\\n{output}.");
            
            // Verify binary log was created and has content
            File.Exists(binlogPath).ShouldBeTrue("Binary log file was not created.");
            
            // Test binlog replay
            string replayOutput = RunnerUtilities.ExecBootstrapedMSBuild(
                $"\"{binlogPath}\" {CommonMSBuildArgs}", 
                out bool replaySuccess, 
                timeoutMilliseconds: _timeoutInMilliseconds);
            
            replaySuccess.ShouldBeTrue($"Binlog replay failed. Output:\\n{replayOutput}");
            
            _output.WriteLine($"Built and replayed {testAsset.SolutionFolder} with arguments {multithreadingArgs}.");
        }
    }
}
