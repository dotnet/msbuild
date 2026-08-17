// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class PerfLogTests
    {
        private readonly ITestOutputHelper _output;

        public PerfLogTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
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
