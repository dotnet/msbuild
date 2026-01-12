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
    public class MultithreadedExecution_Tests : IClassFixture<TestAssetsFixture>, IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private readonly string _testAssetDir;

        // Common parameters for all multithreaded tests:
        // /nodereuse:false - Prevents MSBuild server processes from persisting between tests,
        //                    ensuring proper test isolation and avoiding potential timeouts
        // /v:minimal - Reduces log verbosity for cleaner test output and better performance
        private const string CommonMSBuildArgs = "/nodereuse:false /v:minimal";

        public MultithreadedExecution_Tests(ITestOutputHelper output, TestAssetsFixture testAssetFixture)
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
        /// Tests building projects with various multithreading flags.
        /// </summary>
        [Theory]
        [InlineData(TestAssetsFixture.SingleProjectPath, "/m:1 /mt")]
        [InlineData(TestAssetsFixture.SingleProjectPath, "/m:8 /mt")]
        [InlineData(TestAssetsFixture.ProjectWithDependencies, "/m:1 /mt")]
        [InlineData(TestAssetsFixture.ProjectWithDependencies, "/m:2 /mt")]
        [InlineData(TestAssetsFixture.ProjectWithDependencies, "/m:8 /mt")]
        public void MultithreadedBuild_Success(string projectRelativePath, string multithreadingArgs)
        {
            string projectPath = Path.Combine(_testAssetDir, projectRelativePath);
            
            // Ensure test asset exists - fail if missing
            File.Exists(projectPath).ShouldBeTrue($"Test asset not found: {projectPath}.");

            string output = RunnerUtilities.ExecBootstrapedMSBuild(
                $"\"{projectPath}\" {multithreadingArgs} {CommonMSBuildArgs}", 
                out bool success);

            success.ShouldBeTrue($"Build failed with args '{multithreadingArgs}' for {projectRelativePath}. Output:\\n{output}");
            
            _output.WriteLine($"Built {Path.GetFileNameWithoutExtension(projectRelativePath)} with arguments {multithreadingArgs}.");
        }

        /// <summary>
        /// Tests binary logging with multithreaded builds and verifies replay functionality.
        /// </summary>
        [Theory]
        [InlineData(TestAssetsFixture.SingleProjectPath, "/m:8 /mt")]
        public void MultithreadedBuild_BinaryLogging(string projectRelativePath, string multithreadingArgs)
        {
            string projectPath = Path.Combine(_testAssetDir, projectRelativePath);
            
            // Ensure test asset exists - fail if missing
            File.Exists(projectPath).ShouldBeTrue($"Test asset not found: {projectPath}.");

            var tempFolder = _env.CreateFolder();
            string binlogPath = Path.Combine(tempFolder.Path, "build.binlog");

            try
            {
                // Build with binary logging
                string output = RunnerUtilities.ExecBootstrapedMSBuild(
                    $"\"{projectPath}\" {multithreadingArgs} /bl:\"{binlogPath}\" {CommonMSBuildArgs}", 
                    out bool success);

                success.ShouldBeTrue($"Build failed with args '{multithreadingArgs}' for {projectRelativePath}. Output:\\n{output}.");
                
                // Verify binary log was created and has content
                File.Exists(binlogPath).ShouldBeTrue("Binary log file was not created.");
                
                // Test binlog replay
                string replayOutput = RunnerUtilities.ExecBootstrapedMSBuild($"\"{binlogPath}\" {CommonMSBuildArgs}", out bool replaySuccess);
                
                replaySuccess.ShouldBeTrue($"Binlog replay failed. Output:\\n{replayOutput}");
                
                _output.WriteLine($"Built and replayed {Path.GetFileNameWithoutExtension(projectRelativePath)} with arguments {multithreadingArgs}.");
            }
            finally
            {
                if (File.Exists(binlogPath))
                {
                    File.Delete(binlogPath);
                }
            }
        }
    }
}
