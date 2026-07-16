// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.CommandLine.Experimental;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for MSBUILD_LOGGING_ARGS environment variable functionality.
    /// </summary>
    [TestClass]
    public class XMakeBinlogSwitchTests : IDisposable
    {
        private readonly TestContext _output;
        private readonly TestEnvironment _env;

        public XMakeBinlogSwitchTests(TestContext output)
        {
            _output = output;
            _env = TestEnvironment.Create(output);
        }

        public void Dispose() => _env.Dispose();

        /// <summary>
        /// Test that MSBUILD_LOGGING_ARGS with -bl creates a binary log.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarWithBinaryLogger()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;
            string binlogPath = Path.Combine(directory.Path, "test.binlog");

            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", $"-bl:{binlogPath}");

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            File.Exists(binlogPath).ShouldBeTrue($"Binary log should have been created at {binlogPath}");
        }

        /// <summary>
        /// Test that MSBUILD_LOGGING_ARGS with multiple -bl switches creates multiple binary logs.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarWithMultipleBinaryLoggers()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;
            string binlogPath1 = Path.Combine(directory.Path, "test1.binlog");
            string binlogPath2 = Path.Combine(directory.Path, "test2.binlog");

            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", $"-bl:{binlogPath1} -bl:{binlogPath2}");

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            File.Exists(binlogPath1).ShouldBeTrue($"First binary log should have been created at {binlogPath1}");
            File.Exists(binlogPath2).ShouldBeTrue($"Second binary log should have been created at {binlogPath2}");
        }

        /// <summary>
        /// Test that MSBUILD_LOGGING_ARGS with {} placeholder generates unique filenames.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarWithWildcardPlaceholder()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;

            // Use {} placeholder for unique filename generation
            string binlogPattern = Path.Combine(directory.Path, "build-{}.binlog");
            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", $"-bl:{binlogPattern}");

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            // Find the generated binlog file (should have unique characters instead of {})
            string[] binlogFiles = Directory.GetFiles(directory.Path, "build-*.binlog");
            binlogFiles.Length.ShouldBe(1, $"Expected exactly one binlog file to be created in {directory.Path}");

            // The filename should not contain {} - it should have been replaced with unique characters
            binlogFiles[0].ShouldNotContain("{}");
            binlogFiles[0].ShouldContain("build-");
        }

        /// <summary>
        /// Test that MSBUILD_LOGGING_ARGS with multiple {} placeholders generates unique filenames with each placeholder replaced.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarWithMultipleWildcardPlaceholders()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;

            // Use multiple {} placeholders for unique filename generation
            string binlogPattern = Path.Combine(directory.Path, "build-{}-test-{}.binlog");
            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", $"-bl:{binlogPattern}");

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            // Find the generated binlog file (should have unique characters instead of {})
            string[] binlogFiles = Directory.GetFiles(directory.Path, "build-*-test-*.binlog");
            binlogFiles.Length.ShouldBe(1, $"Expected exactly one binlog file to be created in {directory.Path}");

            // The filename should not contain {} - both placeholders should have been replaced
            binlogFiles[0].ShouldNotContain("{}");
            binlogFiles[0].ShouldContain("build-");
            binlogFiles[0].ShouldContain("-test-");
        }

        /// <summary>
        /// Test that MSBUILD_LOGGING_ARGS ignores unsupported arguments and continues with valid ones.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarIgnoresUnsupportedArguments()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;
            string binlogPath = Path.Combine(directory.Path, "test.binlog");

            // Set env var with mixed valid and invalid arguments
            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", $"-bl:{binlogPath} -maxcpucount:4 -verbosity:detailed");

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            // Binary log should still be created (valid argument)
            File.Exists(binlogPath).ShouldBeTrue($"Binary log should have been created at {binlogPath}");

            // Warning should appear for invalid arguments
            output.ShouldContain("MSB1070");
        }

        /// <summary>
        /// Test that MSBUILD_LOGGING_ARGS works with /noautoresponse.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarWorksWithNoAutoResponse()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;
            string binlogPath = Path.Combine(directory.Path, "test.binlog");

            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", $"-bl:{binlogPath}");

            // Use /noautoresponse - MSBUILD_LOGGING_ARGS should still work
            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\" /noautoresponse", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            File.Exists(binlogPath).ShouldBeTrue($"Binary log should have been created even with /noautoresponse");
        }

        /// <summary>
        /// Test that MSBUILD_LOGGING_ARGS_LEVEL=message emits diagnostics as messages instead of warnings.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarLevelMessageSuppressesWarnings()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;

            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", "-maxcpucount:4");
            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS_LEVEL", "message");

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            output.ShouldNotContain("MSB1070");
        }

        /// <summary>
        /// Test that MSBUILD_LOGGING_ARGS emits warnings by default when MSBUILD_LOGGING_ARGS_LEVEL is not set.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarDefaultLevelEmitsWarnings()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;

            // Set env var with invalid argument, but do NOT set MSBUILD_LOGGING_ARGS_LEVEL
            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", "-maxcpucount:4");

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            // Warning SHOULD appear when level is not set (default behavior)
            output.ShouldContain("MSB1070");
        }

        /// <summary>
        /// Test that empty or whitespace MSBUILD_LOGGING_ARGS is ignored.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarEmptyIsIgnored()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;

            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", "   ");

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);
        }

        /// <summary>
        /// Test that -check switch is allowed in MSBUILD_LOGGING_ARGS.
        /// </summary>
        [MSBuildTestMethod]
        public void LoggingArgsEnvVarAllowsCheckSwitch()
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;

            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", "-check");

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            output.ShouldNotContain("MSB1070");
        }

        /// <summary>
        /// Test that only logging-related switches are allowed.
        /// </summary>
        [MSBuildTestMethod]
        [DataRow("-bl")]
        [DataRow("-bl:test.binlog")]
        [DataRow("-binarylogger")]
        [DataRow("-binarylogger:test.binlog")]
        [DataRow("/bl")]
        [DataRow("/bl:test.binlog")]
        [DataRow("--bl")]
        [DataRow("-check")]
        [DataRow("/check")]
        public void LoggingArgsEnvVarAllowedSwitches(string switchArg)
        {
            CommandLineParser parser = new();
            _ = _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", switchArg);

            CommandLineSwitches switches = new();
            List<BuildManager.DeferredBuildMessage> deferredBuildMessages = new();
            parser.GatherLoggingArgsEnvironmentVariableSwitches(ref switches, deferredBuildMessages, "test");

            switches.HaveErrors().ShouldBeFalse($"Switch {switchArg} should be allowed");
        }

        /// <summary>
        /// Test that non-logging switches are rejected.
        /// </summary>
        [MSBuildTestMethod]
        [DataRow("-property:A=1")]
        [DataRow("-target:Build")]
        [DataRow("-verbosity:detailed")]
        [DataRow("-maxcpucount:4")]
        [DataRow("/p:A=1")]
        [DataRow("-restore")]
        [DataRow("-nologo")]
        public void LoggingArgsEnvVarDisallowedSwitches(string switchArg)
        {
            var directory = _env.CreateFolder();
            string content = ObjectModelHelpers.CleanupFileContents("<Project><Target Name='t'><Message Text='Hello'/></Target></Project>");
            var projectPath = directory.CreateFile("my.proj", content).Path;

            _env.SetEnvironmentVariable("MSBUILD_LOGGING_ARGS", switchArg);

            string output = RunnerUtilities.ExecMSBuild($"\"{projectPath}\"", out var successfulExit, _output);
            successfulExit.ShouldBeTrue(output);

            output.ShouldContain("MSB1070");
        }
    }
}
