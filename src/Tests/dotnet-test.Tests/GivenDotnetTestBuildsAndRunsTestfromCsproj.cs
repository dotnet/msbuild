// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestFromCsproj : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestFromCsproj(ITestOutputHelper log) : base(log)
        {
        }

        private readonly string[] ConsoleLoggerOutputNormal = new[] { "--logger", "console;verbosity=normal" };

        [Fact]
        public void MSTestSingleTFM()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("3");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal);

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().Contain("Passed VSTestPassTest");
                result.StdOut.Should().Contain("Failed VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenTesting()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().Contain("Passed VSTestPassTest");
                result.StdOut.Should().Contain("Failed VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenTestingWithTheNoRestoreOption()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            new DotnetTestCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(ConsoleLoggerOutputNormal.Concat(new[] { "--no-restore", "/p:IsTestProject=true" }))
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void ItDoesNotRunTestsIfThereIsNoIsTestProject()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            new DotnetTestCommand(Log, ConsoleLoggerOutputNormal)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--no-restore", "/p:IsTestProject=''")
                .Should().Pass();
        }

        [Fact]
        public void XunitSingleTFM()
        {
            // Copy XunitCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "XunitCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, identifier: "4")
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            // Restore project XunitCore
            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal);

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().Contain("Passed TestNamespace.VSTestXunitTests.VSTestXunitPassTest");
                result.StdOut.Should().Contain("Failed TestNamespace.VSTestXunitTests.VSTestXunitFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void GivenAFailingTestItDisplaysFailureDetails()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("XunitCore")
                .WithSource()
                .WithVersionVariables();

            var result = new DotnetTestCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            result.ExitCode.Should().Be(1);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Failed TestNamespace.VSTestXunitTests.VSTestXunitFailTest");
                result.StdOut.Should().Contain("Total:     2");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().Contain("Failed:     1");
            }
        }

        [Fact]
        public void ItAcceptsMultipleLoggersAsCliArguments()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("10");
            var trxFileNamePattern = "custom*.trx";
            string trxLoggerDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }

            // Call test with logger enable
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--logger", "trx;logfilename=custom.trx", "--logger",
                                            "console;verbosity=normal", "--", "RunConfiguration.ResultsDirectory=" + trxLoggerDirectory);

            // Verify
            if (!TestContext.IsLocalized())
            {
                // We append current date time to trx file name, hence modifying this check
                Assert.True(Directory.EnumerateFiles(trxLoggerDirectory, trxFileNamePattern).Any());

                result.StdOut.Should().Contain("Passed VSTestPassTest");
                result.StdOut.Should().Contain("Failed VSTestFailTest");
            }

            // Cleanup trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }
        }

        [Fact]
        public void TestWillNotBuildTheProjectIfNoBuildArgsIsGiven()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("5");
            string configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            string expectedError = Path.Combine(testProjectDirectory, "bin",
                                   configuration, ToolsetInfo.CurrentTargetFramework, "VSTestCore.dll");
            expectedError = "The test source file " + "\"" + expectedError + "\"" + " provided was not found.";

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--no-build", "-v:m");

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().NotContain("Restore");
                //  https://github.com/dotnet/sdk/issues/3684
                //  Disable expected error check, it is sometimes giving the following error:
                //  The argument /opt/code/artifacts-ubuntu.18.04/tmp/Debug/bin/5/VSTestCore/bin/Debug/netcoreapp3.0/VSTestCore.dll is invalid. Please use the /help option to check the list of valid arguments
                //result.StdErr.Should().Contain(expectedError);
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void TestWillCreateTrxLoggerInTheSpecifiedResultsDirectoryBySwitch()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("6");

            string trxLoggerDirectory = Path.Combine(testProjectDirectory, "TR", "x.y");

            // Delete trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }

            // Call test with trx logger enabled and results directory explicitly specified.
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--logger", "trx", "--results-directory", trxLoggerDirectory);

            // Verify
            string[] trxFiles = Directory.GetFiles(trxLoggerDirectory, "*.trx");
            Assert.Single(trxFiles);
            result.StdOut.Should().Contain(trxFiles[0]);

            // Cleanup trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }
        }

        [Fact]
        public void ItCreatesTrxReportInTheSpecifiedResultsDirectoryByArgs()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("7");
            var trxFileNamePattern = "custom*.trx";
            string trxLoggerDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }

            // Call test with logger enable
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--logger", "trx;logfilename=custom.trx", "--",
                                                "RunConfiguration.ResultsDirectory=" + trxLoggerDirectory);

            // Verify
            // We append current date time to trx file name, hence modifying this check
            Assert.True(Directory.EnumerateFiles(trxLoggerDirectory, trxFileNamePattern).Any());

            // Cleanup trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }
        }

        [Fact]
        public void ItBuildsAndTestsAppWhenRestoringToSpecificDirectory()
        {
            // Creating folder with name short name "RestoreTest" to avoid PathTooLongException
            var rootPath = _testAssetsManager.CopyTestAsset("VSTestCore", identifier: "8")
                .WithSource()
                .WithVersionVariables()
                .Path;

            string pkgDir;
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //{
            //    // Moving pkgs folder on top to avoid PathTooLongException
            //    pkgDir = Path.Combine(RepoDirectoriesProvider.TestWorkingFolder, "pkgs");
            //}
            //else
            {
                pkgDir = _testAssetsManager.CreateTestDirectory(identifier: "pkgs").Path;
                Log.WriteLine("pkgDir, package restored path is: " + pkgDir);
            }

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--packages", pkgDir)
                .Should()
                .Pass();

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-restore")
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            CommandResult result = new DotnetTestCommand(Log, ConsoleLoggerOutputNormal)
                                        .WithWorkingDirectory(rootPath)
                                        .Execute("--no-restore");

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().Contain("Passed VSTestPassTest");
                result.StdOut.Should().Contain("Failed VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Theory]
        [InlineData("q", false)]
        [InlineData("m", false)]
        [InlineData("n", true)]
        [InlineData("d", true)]
        [InlineData("diag", true)]
        public void ItUsesVerbosityPassedToDefineVerbosityOfConsoleLoggerOfTheTests(string verbosity, bool shouldShowPassedTests)
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp($"9_{verbosity}");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute("-v", verbosity);

            // Verify
            if (!TestContext.IsLocalized())
            {
                if (shouldShowPassedTests)
                {
                    result.StdOut.Should().Contain("Total tests: 2");
                    result.StdOut.Should().Contain("Passed: 1");
                    result.StdOut.Should().Contain("Failed: 1");

                    result.StdOut.Should().Contain("Passed VSTestPassTest");
                }
                else
                {
                    result.StdOut.Should().Contain("Total:     2");
                    result.StdOut.Should().Contain("Passed:     1");
                    result.StdOut.Should().Contain("Failed:     1");

                    result.StdOut.Should().NotContain("Passed VSTestPassTest");
                }
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItTestsWithTheSpecifiedRuntimeOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("XunitCore")
                            .WithSource()
                            .WithVersionVariables();

            var rootPath = testInstance.Path;
            var rid = EnvironmentInfo.GetCompatibleRid();

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--runtime", rid)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            var result = new DotnetTestCommand(Log, ConsoleLoggerOutputNormal)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-build", "--runtime", rid);

            result
                .Should()
                .NotHaveStdErrContaining("MSB1001")
                .And
                .HaveStdOutContaining(rid);

            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total tests: 2");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 1");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItAcceptsNoLogoAsCliArguments()
        {
            // Copy and restore VSTestCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("14");

            // Call test with logger enable
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--nologo");

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().NotContain("Microsoft (R) Test Execution Command Line Tool Version");
                result.StdOut.Should().Contain("Total:     2");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().Contain("Failed:     1");
            }
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        public void ItCreatesCoverageFileWhenCodeCoverageEnabledByRunsettings()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("11");

            string resultsDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete resultsDirectory if it exist
            if (Directory.Exists(resultsDirectory))
            {
                Directory.Delete(resultsDirectory, true);
            }

            var settingsPath = Path.Combine(AppContext.BaseDirectory, "CollectCodeCoverage.runsettings");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(
                                            "--settings", settingsPath,
                                            "--results-directory", resultsDirectory);

            File.WriteAllText(Path.Combine(testProjectDirectory, "output.txt"),
                                result.StdOut + Environment.NewLine + result.StdErr);

            // Verify test results
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total:     2");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().Contain("Failed:     1");
            }

            // Verify coverage file.
            DirectoryInfo d = new(resultsDirectory);
            FileInfo[] coverageFileInfos = d.GetFiles("*.coverage", SearchOption.AllDirectories);
            Assert.Single(coverageFileInfos);

            result.ExitCode.Should().Be(1);
        }

        [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.OSX | TestPlatforms.Linux)]
        public void ItCreatesCoverageFileInResultsDirectory()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("12");

            string resultsDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete resultsDirectory if it exist
            if (Directory.Exists(resultsDirectory))
            {
                Directory.Delete(resultsDirectory, true);
            }

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(
                                            "--collect", "Code Coverage",
                                            "--results-directory", resultsDirectory);

            // Verify test results
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total:     2");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().Contain("Failed:     1");
            }

            // Verify coverage file.
            DirectoryInfo d = new(resultsDirectory);
            FileInfo[] coverageFileInfos = d.GetFiles("*.coverage", SearchOption.AllDirectories);
            Assert.Single(coverageFileInfos);

            result.ExitCode.Should().Be(1);
        }

        [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.OSX | TestPlatforms.Linux)]
        public void ItCreatesCoberturaFileProvidedByCommandInResultsDirectory()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("15");

            string resultsDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete resultsDirectory if it exist
            if (Directory.Exists(resultsDirectory))
            {
                Directory.Delete(resultsDirectory, true);
            }

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(
                                            "--collect", "Code Coverage;Format=Cobertura",
                                            "--results-directory", resultsDirectory);

            // Verify test results
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total:     2");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().Contain("Failed:     1");
            }

            // Verify coverage file.
            DirectoryInfo d = new(resultsDirectory);
            FileInfo[] coverageFileInfos = d.GetFiles("*.cobertura.xml", SearchOption.AllDirectories);
            Assert.Single(coverageFileInfos);

            result.ExitCode.Should().Be(1);
        }

        [PlatformSpecificFact(TestPlatforms.Windows)]
        public void ItHandlesMultipleCollectCommandInResultsDirectory()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("16");

            string resultsDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete resultsDirectory if it exist
            if (Directory.Exists(resultsDirectory))
            {
                Directory.Delete(resultsDirectory, true);
            }

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(
                                            "--collect", "XPlat Code Coverage;arg1=val1",
                                            "--collect", "Another Coverage Collector;arg1=val1",
                                            "--results-directory", resultsDirectory);

            // Verify test results
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total:     2");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().Contain("Failed:     1");
                result.StdOut.Should().Contain("Unable to find a datacollector with friendly name 'XPlat Code Coverage'");
                result.StdOut.Should().Contain("Could not find data collector 'XPlat Code Coverage'");
                result.StdOut.Should().Contain("Unable to find a datacollector with friendly name 'Another Coverage Collector'");
                result.StdOut.Should().Contain("Could not find data collector 'Another Coverage Collector'");
            }

            // Verify coverage file.
            DirectoryInfo d = new(resultsDirectory);
            FileInfo[] coverageFileInfos = d.GetFiles("*.coverage", SearchOption.AllDirectories);
            Assert.Empty(coverageFileInfos);

            result.ExitCode.Should().Be(1);
        }

        [PlatformSpecificFact(TestPlatforms.FreeBSD)]
        public void ItShouldShowWarningMessageOnCollectCodeCoverage()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("13");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(
                                            "--collect", "Code Coverage",
                                            "--filter", "VSTestPassTest");

            // Verify test results
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("No code coverage data available. Code coverage is currently supported only on Windows and Linux x64.");
                result.StdOut.Should().Contain("Total:     1");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().NotContain("Failed!");
            }

            result.ExitCode.Should().Be(0);
        }

        [PlatformSpecificFact(TestPlatforms.Linux, Skip = "https://github.com/dotnet/sdk/issues/22865")]
        public void ItShouldShowWarningMessageOnCollectCodeCoverageThatProfilerWasNotInitialized()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("13");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(
                                            "--collect", "Code Coverage",
                                            "--filter", "VSTestPassTest");

            // Verify test results
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("No code coverage data available. Code coverage is currently supported only on Windows, Linux x64 and macOS x64.");
                result.StdOut.Should().Contain("Total:     1");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().NotContain("Failed!");
            }

            result.ExitCode.Should().Be(0);
        }

        [Fact]
        public void ItShouldShowImportantMessage()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithVersionVariables()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeVsTestToProject);

            var testProjectDirectory = testInstance.Path;

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute();

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Important text");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItSetsDotnetRootToTheLocationOfDotnetExecutableWhenRunningDotnetTestWithProject()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal);

            result.ExitCode.Should().Be(1);
            var dotnet = result.StartInfo.FileName;
            Path.GetFileNameWithoutExtension(dotnet).Should().Be("dotnet");
            string dotnetRoot = Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)";
            result.StartInfo.EnvironmentVariables.ContainsKey(dotnetRoot).Should().BeTrue($"because {dotnetRoot} should be set");
            result.StartInfo.EnvironmentVariables[dotnetRoot].Should().Be(Path.GetDirectoryName(dotnet));
        }

        [Fact]
        public void TestsFromCsprojAndArchSwitchShouldFlowToMsBuild()
        {
            string testAppName = "VSTestCore";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithVersionVariables()
                .WithProjectChanges(ProjectModification.AddDisplayMessageBeforeVsTestToProject);

            var testProjectDirectory = testInstance.Path;

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--arch", "wrongArchitecture");

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("error NETSDK1083: The specified RuntimeIdentifier");
                result.StdOut.Should().Contain("wrongArchitecture");
            }

            result.ExitCode.Should().Be(1);
        }

        [Theory] // See issue https://github.com/dotnet/sdk/issues/10423
        [InlineData("TestCategory=CategoryA,CategoryB", "_comma")]
        [InlineData("TestCategory=CategoryA%2cCategoryB", "_comma_encoded")]
        [InlineData("\"TestCategory=CategoryA,CategoryB\"", "_already_escaped")]
        public void FilterPropertyCorrectlyHandlesComma(string filter, string folderSuffix)
        {
            string testAppName = "TestCategoryWithComma";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, folderSuffix)
                .WithSource()
                .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--filter", filter);

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total:     1");
                result.StdOut.Should().Contain("Passed:     1");
            }
        }

        [Theory]
        [InlineData("--output")]
        [InlineData("--diag")]
        [InlineData("--results-directory")]
        public void EnsureOutputPathEscaped(string flag)
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp(flag);

            var pathWithComma = Path.Combine(AppContext.BaseDirectory, "a,b");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(flag, pathWithComma);

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total:     2");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().Contain("Failed:     1");
            }
        }

        [Theory]
        // Even count of slash/backslash
        [InlineData("--output", "\\\\")]
        [InlineData("--output", "\\\\\\\\")]
        [InlineData("--output", "//")]
        [InlineData("--output", "////")]
        [InlineData("--diag", "\\\\")]
        [InlineData("--diag", "\\\\\\\\")]
        [InlineData("--diag", "//")]
        [InlineData("--diag", "////")]
        [InlineData("--results-directory", "\\\\")]
        [InlineData("--results-directory", "\\\\\\\\")]
        [InlineData("--results-directory", "//")]
        [InlineData("--results-directory", "////")]
        // Odd count of slash/backslash
        [InlineData("--output", "\\")]
        [InlineData("--output", "\\\\\\")]
        [InlineData("--output", "/")]
        [InlineData("--output", "///")]
        [InlineData("--diag", "\\")]
        [InlineData("--diag", "\\\\\\")]
        [InlineData("--diag", "/")]
        [InlineData("--diag", "///")]
        [InlineData("--results-directory", "\\")]
        [InlineData("--results-directory", "\\\\\\")]
        [InlineData("--results-directory", "/")]
        [InlineData("--results-directory", "///")]
        public void PathEndsWithSlashOrBackslash(string flag, string slashesOrBackslashes)
        {
            // NOTE: We also want to test with forward slashes because on Windows they
            // are converted to backslashes and so need to be handled correctly.
            string testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp(Guid.NewGuid().ToString());
            string flagDirectory = Path.Combine(testProjectDirectory, "flag-dir");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(flag, flagDirectory + slashesOrBackslashes);

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total:     2");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().Contain("Failed:     1");
            }

            Directory.Exists(flagDirectory).Should().BeTrue("folder '{0}' should exist.", flagDirectory);
            Directory.EnumerateFileSystemEntries(flagDirectory).Should().NotBeEmpty();
        }

        [Theory]
        [InlineData("-e:foo=bardll")]
        [InlineData("-e:foo=barexe")]
        public void ArgumentsEndWithDllOrExeShouldNotFail(string arg)
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp();

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                .Execute(testProjectDirectory, arg);

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().Contain("Total:     2");
                result.StdOut.Should().Contain("Passed:     1");
                result.StdOut.Should().Contain("Failed:     1");
                result.StdOut.Should().Contain("Failed VSTestFailTest");
            }
        }

        private string CopyAndRestoreVSTestDotNetCoreTestApp([CallerMemberName] string callingMethod = "")
        {
            // Copy VSTestCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestCore";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, callingMethod: callingMethod)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            // Restore project VSTestCore
            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            return testProjectDirectory;
        }
    }
}
