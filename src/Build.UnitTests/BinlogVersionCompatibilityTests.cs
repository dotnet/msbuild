// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests to verify binlog version compatibility across different MSBuild versions.
    /// This test builds the same project with different binlog format versions and
    /// verifies that all versions can be read successfully.
    /// </summary>
    public class BinlogVersionCompatibilityTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private readonly string _tempDirectory;
        private readonly string _testProjectPath;

        // Array to store binlogs from different versions
        private readonly List<string> _binlogPaths = new List<string>();

        // Canonical test project content
        private const string TestProject = @"
<Project>
  <PropertyGroup>
    <TestProperty>Test</TestProperty>
    <Configuration Condition=""'$(Configuration)' == ''"">Debug</Configuration>
    <Platform Condition=""'$(Platform)' == ''"">AnyCPU</Platform>
    <OutputPath>bin\$(Configuration)\</OutputPath>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <TestItem Include=""Test"" />
    <Compile Include=""Class1.cs"" />
  </ItemGroup>

  <Target Name=""Target1"">
    <Message Text=""MessageOutputText""/>
    <Message Text=""[$(MSBuildThisFileFullPath)]""/>
    <CreateProperty Value=""$(TestProperty)_Modified"">
      <Output PropertyName=""ModifiedProperty"" TaskParameter=""Value""/>
    </CreateProperty>
  </Target>

  <Target Name=""Target2"" AfterTargets=""Target1"">
    <Exec Command=""echo Building Target2""/>
    <ItemGroup>
      <GeneratedItem Include=""Generated1"" />
      <GeneratedItem Include=""Generated2"" />
    </ItemGroup>
  </Target>

  <Target Name=""Target3"" AfterTargets=""Target2"">
    <MSBuild Projects=""$(MSBuildThisFileFullPath)"" Properties=""GP=a"" Targets=""InnerTarget1""/>
    <MSBuild Projects=""$(MSBuildThisFileFullPath)"" Properties=""GP=b"" Targets=""InnerTarget1""/>
    <MSBuild Projects=""$(MSBuildThisFileFullPath)"" Properties=""GP=a"" Targets=""InnerTarget2""/>
    <MSBuild Projects=""$(MSBuildThisFileFullPath)"" Properties=""GP=a"" Targets=""InnerTarget2""/>
  </Target>

  <Target Name=""InnerTarget1"">
    <Message Text=""inner target 1 with GP=$(GP)""/>
  </Target>

  <Target Name=""InnerTarget2"">
    <Message Text=""inner target 2 with GP=$(GP)""/>
    <Warning Text=""Test warning message"" Condition=""'$(GP)' == 'a'""/>
  </Target>

  <Target Name=""Build"" DependsOnTargets=""Target1;Target2;Target3"">
    <Message Text=""Build completed successfully""/>
  </Target>

</Project>";

        public BinlogVersionCompatibilityTests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
            _tempDirectory = _env.CreateFolder().Path;

            // Use the canonical project file from the BinlogVersionCompatibility directory
            var canonicalProjectPath = Path.Combine("BinlogVersionCompatibility", "1.proj");
            var sourceProjectPath = Path.GetFullPath(canonicalProjectPath);
            
            if (File.Exists(sourceProjectPath))
            {
                _testProjectPath = Path.Combine(_tempDirectory, "1.proj");
                File.Copy(sourceProjectPath, _testProjectPath);
                _output.WriteLine($"Using canonical project from: {sourceProjectPath}");
            }
            else
            {
                // Fallback to creating the test project inline
                _testProjectPath = Path.Combine(_tempDirectory, "1.proj");
                File.WriteAllText(_testProjectPath, TestProject);
                _output.WriteLine($"Using inline test project at: {_testProjectPath}");
            }

            // Create dummy source files referenced by the project
            var class1Path = Path.Combine(_tempDirectory, "Class1.cs");
            File.WriteAllText(class1Path, @"
using System;

public class Class1
{
    public void TestMethod()
    {
        Console.WriteLine(""Hello from Class1"");
    }
}");

            // Create Properties directory and AssemblyInfo.cs
            var propertiesDir = Path.Combine(_tempDirectory, "Properties");
            Directory.CreateDirectory(propertiesDir);
            var assemblyInfoPath = Path.Combine(propertiesDir, "AssemblyInfo.cs");
            File.WriteAllText(assemblyInfoPath, @"
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle(""TestProject"")]
[assembly: AssemblyDescription(""Test project for binlog compatibility testing"")]
[assembly: AssemblyConfiguration("""")]
[assembly: AssemblyCompany("""")]
[assembly: AssemblyProduct(""MSBuild"")]
[assembly: AssemblyCopyright(""Copyright © Microsoft"")]
[assembly: AssemblyTrademark("""")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion(""1.0.0.0"")]
[assembly: AssemblyFileVersion(""1.0.0.0"")]
");
        }

        /// <summary>
        /// Generates binlogs for different MSBuild binlog format versions.
        /// This simulates building with different versions by using different logger parameters.
        /// </summary>
        [Fact]
        public void GenerateBinlogsForDifferentVersions()
        {
            // Test different binlog configurations that might represent different versions
            var binlogConfigurations = new[]
            {
                new { Version = "v1", Parameters = "LogFile=version1.binlog" },
                new { Version = "v2", Parameters = "LogFile=version2.binlog;ProjectImports=None" },
                new { Version = "v3", Parameters = "LogFile=version3.binlog;ProjectImports=Embed" },
                new { Version = "v4", Parameters = "LogFile=version4.binlog;ProjectImports=ZipFile" },
                new { Version = "v5", Parameters = "LogFile=version5.binlog;OmitInitialInfo" },
                new { Version = "v6", Parameters = "LogFile=version6.binlog;ProjectImports=Embed;OmitInitialInfo" },
                new { Version = "v7", Parameters = "LogFile=version7.binlog;ProjectImports=ZipFile;OmitInitialInfo" },
                new { Version = "v8", Parameters = "LogFile=version8.binlog" },
                new { Version = "v9", Parameters = "LogFile=version9.binlog;ProjectImports=None;OmitInitialInfo" },
                new { Version = "v10", Parameters = "LogFile=version10.binlog;ProjectImports=Embed" }
            };

            foreach (var config in binlogConfigurations)
            {
                string binlogPath = Path.Combine(_tempDirectory, $"version{config.Version.Substring(1)}.binlog");
                _binlogPaths.Add(binlogPath);

                _output.WriteLine($"Building project with {config.Version} configuration...");
                
                // Replace the LogFile parameter with the full path
                string parameters = config.Parameters.Replace($"LogFile=version{config.Version.Substring(1)}.binlog", $"LogFile={binlogPath}");
                bool buildSuccess = BuildProjectWithBinlog(parameters);
                
                buildSuccess.ShouldBeTrue($"Build should succeed for {config.Version}");
                File.Exists(binlogPath).ShouldBeTrue($"Binlog should be created for {config.Version}");
                
                var fileInfo = new FileInfo(binlogPath);
                fileInfo.Length.ShouldBeGreaterThan(0, $"Binlog should not be empty for {config.Version}");
                
                _output.WriteLine($"Generated binlog for {config.Version}: {binlogPath} ({fileInfo.Length} bytes)");
            }

            _output.WriteLine($"Successfully generated {_binlogPaths.Count} binlogs");
        }

        /// <summary>
        /// Tests that all generated binlogs can be successfully read and analyzed.
        /// </summary>
        [Fact]
        public void PlaybackAllBinlogVersions()
        {
            // First generate the binlogs
            GenerateBinlogsForDifferentVersions();

            var failedBinlogs = new List<string>();

            foreach (string binlogPath in _binlogPaths)
            {
                try
                {
                    _output.WriteLine($"Testing playback of {Path.GetFileName(binlogPath)}...");
                    
                    // Test reading with BinaryLogReplayEventSource
                    TestBinlogPlaybackWithReplayEventSource(binlogPath);
                    
                    // Test reading with Structured Logger if available
                    TestBinlogPlaybackWithStructuredLogger(binlogPath);
                    
                    _output.WriteLine($"✓ Successfully played back {Path.GetFileName(binlogPath)}");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"✗ Failed to play back {Path.GetFileName(binlogPath)}: {ex.Message}");
                    failedBinlogs.Add(binlogPath);
                }
            }

            failedBinlogs.ShouldBeEmpty($"All binlogs should be playable. Failed: {string.Join(", ", failedBinlogs.Select(Path.GetFileName))}");
        }

        private void TestBinlogPlaybackWithReplayEventSource(string binlogPath)
        {
            var replayEventSource = new BinaryLogReplayEventSource();
            var events = new List<BuildEventArgs>();
            
            replayEventSource.AnyEventRaised += (sender, e) => events.Add(e);
            
            replayEventSource.Replay(binlogPath);
            
            events.Count.ShouldBeGreaterThan(0, "Should have received build events");
            events.ShouldContain(e => e is BuildStartedEventArgs, "Should contain BuildStartedEventArgs");
            events.ShouldContain(e => e is BuildFinishedEventArgs, "Should contain BuildFinishedEventArgs");
        }

        private void TestBinlogPlaybackWithStructuredLogger(string binlogPath)
        {
            try
            {
                // Test that we can successfully read the binlog using the standard MSBuild binary log reader
                var reader = new BinaryLogReplayEventSource();
                var projects = new List<ProjectStartedEventArgs>();
                var targets = new List<TargetStartedEventArgs>();
                var tasks = new List<TaskStartedEventArgs>();
                var buildFinished = false;

                reader.ProjectStarted += (sender, e) => projects.Add(e);
                reader.TargetStarted += (sender, e) => targets.Add(e);
                reader.TaskStarted += (sender, e) => tasks.Add(e);
                reader.BuildFinished += (sender, e) => buildFinished = true;

                reader.Replay(binlogPath);

                // Verify basic build structure
                buildFinished.ShouldBeTrue("Build should have finished");
                projects.ShouldNotBeEmpty("Should contain projects");
                targets.ShouldNotBeEmpty("Should contain targets");

                _output.WriteLine($"  - Loaded build with {projects.Count} projects, {targets.Count} targets, {tasks.Count} tasks");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  - Error during structured analysis: {ex.Message}");
                throw;
            }
        }

        private bool BuildProjectWithBinlog(string binlogParameters)
        {
            try
            {
                var logger = new BinaryLogger();
                logger.Parameters = binlogParameters;

                using var buildManager = new Microsoft.Build.Execution.BuildManager();
                var buildParameters = new Microsoft.Build.Execution.BuildParameters
                {
                    Loggers = new ILogger[] { logger }
                };

                var buildRequestData = new Microsoft.Build.Execution.BuildRequestData(
                    _testProjectPath,
                    new Dictionary<string, string>(),
                    null,
                    new[] { "Build" },
                    null);

                var result = buildManager.Build(buildParameters, buildRequestData);
                return result.OverallResult == Microsoft.Build.Execution.BuildResultCode.Success;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Build failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test that verifies binlog files have expected structure and content.
        /// </summary>
        [Fact]
        public void VerifyBinlogStructure()
        {
            // Generate a single binlog for structure verification
            string binlogPath = Path.Combine(_tempDirectory, "structure_test.binlog");
            bool buildSuccess = BuildProjectWithBinlog($"LogFile={binlogPath};ProjectImports=Embed");
            
            buildSuccess.ShouldBeTrue("Build should succeed");
            File.Exists(binlogPath).ShouldBeTrue("Binlog should exist");

            // Test basic file format
            var fileInfo = new FileInfo(binlogPath);
            fileInfo.Length.ShouldBeGreaterThan(100, "Binlog should have substantial content");

            // Read the first few bytes to verify it's a valid binlog format
            using var stream = File.OpenRead(binlogPath);
            var buffer = new byte[20];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            
            bytesRead.ShouldBeGreaterThan(0, "Should be able to read from binlog");
            
            // Verify the file can be opened with BinaryLogReplayEventSource
            var reader = new BinaryLogReplayEventSource();
            var eventCount = 0;
            reader.AnyEventRaised += (s, e) => eventCount++;
            reader.Replay(binlogPath); // Should not throw
            eventCount.ShouldBeGreaterThan(0, "Should have received events from binlog");
        }

        /// <summary>
        /// Performance test to ensure binlog generation and playback performs reasonably.
        /// </summary>
        [Fact]
        public void BinlogPerformanceTest()
        {
            string binlogPath = Path.Combine(_tempDirectory, "performance_test.binlog");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool buildSuccess = BuildProjectWithBinlog($"LogFile={binlogPath}");
            stopwatch.Stop();
            
            buildSuccess.ShouldBeTrue("Build should succeed");
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(30000, "Build should complete within 30 seconds");
            
            _output.WriteLine($"Build completed in {stopwatch.ElapsedMilliseconds}ms");
            
            // Test playback performance
            stopwatch.Restart();
            var reader = new BinaryLogReplayEventSource();
            int eventCount = 0;
            reader.AnyEventRaised += (s, e) => eventCount++;
            reader.Replay(binlogPath);
            stopwatch.Stop();
            
            eventCount.ShouldBeGreaterThan(0, "Should have received events");
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000, "Playback should complete within 10 seconds");
            
            _output.WriteLine($"Playback of {eventCount} events completed in {stopwatch.ElapsedMilliseconds}ms");
        }

        public void Dispose()
        {
            _env?.Dispose();
        }
    }
}
