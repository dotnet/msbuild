// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.Build.CommandLine;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Xunit;
using Xunit.Abstractions;
using Shouldly;
using System.IO.Compression;
using System.Reflection;

namespace Microsoft.Build.UnitTests
{
    public class PerfLogTests
    {
#if USE_MSBUILD_DLL_EXTN
        private const string MSBuildExeName = "MSBuild.dll";
#else
        private const string MSBuildExeName = "MSBuild.exe";
#endif

        private readonly ITestOutputHelper _output;

        public PerfLogTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "mono-osx-failing")] // Disable on Mono OSX, since Mono doesn't implement EventSource.
        public void TestPerfLogEnabledProducedLogFile()
        {
            // Setup perf log.
            string perfLogDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            environmentVariables["DOTNET_PERFLOG_DIR"] = perfLogDir;

            // Setup project directory.
            string projectDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(projectDir, "my.proj");

            try
            {
                Directory.CreateDirectory(perfLogDir);
                Directory.CreateDirectory(projectDir);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string msbuildParameters = "\"" + projectPath + "\"";

                bool successfulExit;
                RunnerUtilities.ExecMSBuild(RunnerUtilities.PathToCurrentlyRunningMsBuildExe, msbuildParameters, out successfulExit, environmentVariables: environmentVariables);
                successfulExit.ShouldBeTrue();

                // Look for the file.
                // NOTE: We don't explicitly look for one file because it's possible that more components will add files that will show up here.
                // It's most important to ensure that at least one file shows up because any others that show up will be there because MSBuild properly
                // enabled this functionality.
                string[] files = Directory.GetFiles(perfLogDir, "perf-*.log");
                Assert.NotEmpty(files);
                Assert.All(files, f => Assert.True(new FileInfo(f).Length > 0));
            }
            finally
            {
                Directory.Delete(perfLogDir, true);
                Directory.Delete(projectDir, true);
            }
        }

        [Fact]
        public void TestPerfLogDirectoryDoesNotExist()
        {
            // Setup perf log.
            string perfLogDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            environmentVariables["DOTNET_PERFLOG_DIR"] = perfLogDir;

            // Setup project directory.
            string projectDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string projectPath = Path.Combine(projectDir, "my.proj");

            try
            {
                Directory.CreateDirectory(projectDir);

                string content = ObjectModelHelpers.CleanupFileContents("<Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'><Target Name='t'><Warning Text='[A=$(A)]'/></Target></Project>");
                File.WriteAllText(projectPath, content);

                string msbuildParameters = "\"" + projectPath + "\"";

                Assert.False(Directory.Exists(perfLogDir));

                bool successfulExit;
                RunnerUtilities.ExecMSBuild(RunnerUtilities.PathToCurrentlyRunningMsBuildExe, msbuildParameters, out successfulExit, environmentVariables: environmentVariables);
                successfulExit.ShouldBeTrue();

                Assert.False(Directory.Exists(perfLogDir));
            }
            finally
            {
                Directory.Delete(projectDir, true);
            }
        }
    }
}
