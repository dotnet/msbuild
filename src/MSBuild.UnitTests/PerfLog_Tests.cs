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
            using (TestEnvironment testEnv = TestEnvironment.Create(_output))
            {
                // Setup perf log.
                TransientTestFolder perfLogFolder = testEnv.CreateFolder(createFolder: true);
                testEnv.SetEnvironmentVariable("DOTNET_PERFLOG_DIR", perfLogFolder.Path);

                // Setup project directory.
                TransientTestFolder projectFolder = testEnv.CreateFolder(createFolder: true);
                TransientTestFile classLibrary = testEnv.CreateFile(projectFolder, "ClassLibrary.csproj",
                    @"<Project>
                  <Target Name=""ClassLibraryTarget"">
                      <Message Text=""ClassLibraryBuilt""/>
                  </Target>
                  </Project>
                    ");

                string projectPath = Path.Combine(projectFolder.Path, "ClassLibrary.csproj");
                string msbuildParameters = "\"" + projectPath + "\"";

                RunnerUtilities.ExecMSBuild(msbuildParameters, out bool successfulExit);
                successfulExit.ShouldBeTrue();

                // Look for the file.
                // NOTE: We don't explicitly look for one file because it's possible that more components will add files that will show up here.
                // It's most important to ensure that at least one file shows up because any others that show up will be there because MSBuild properly
                // enabled this functionality.
                string[] files = Directory.GetFiles(perfLogFolder.Path, "perf-*.log");
                files.ShouldNotBeEmpty();
                files.ShouldAllBe(f => new FileInfo(f).Length > 0);
            }
        }

        [Fact]
        public void TestPerfLogDirectoryGetsCreated()
        {
            using (TestEnvironment testEnv = TestEnvironment.Create(_output))
            {
                // Setup invalid perf log directory.
                TransientTestFolder perfLogFolder = testEnv.CreateFolder(createFolder: true);
                string perfLogPath = Path.Combine(perfLogFolder.Path, "logs");
                testEnv.SetEnvironmentVariable("DOTNET_PERFLOG_DIR", perfLogPath);

                // Setup project directory.
                TransientTestFolder projectFolder = testEnv.CreateFolder(createFolder: true);
                TransientTestFile classLibrary = testEnv.CreateFile(projectFolder, "ClassLibrary.csproj",
                    @"<Project>
                  <Target Name=""ClassLibraryTarget"">
                      <Message Text=""ClassLibraryBuilt""/>
                  </Target>
                  </Project>
                    ");

                string projectPath = Path.Combine(projectFolder.Path, "ClassLibrary.csproj");
                string msbuildParameters = "\"" + projectPath + "\"";

                Directory.Exists(perfLogPath).ShouldBeFalse();

                RunnerUtilities.ExecMSBuild(msbuildParameters, out bool successfulExit);
                successfulExit.ShouldBeTrue();

                Directory.Exists(perfLogPath).ShouldBeTrue();
            }
        }
    }
}
