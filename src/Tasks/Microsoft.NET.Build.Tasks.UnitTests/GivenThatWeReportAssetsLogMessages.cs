// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using NuGet.Common;
using NuGet.ProjectModel;
using System;
using System.Linq;
using Xunit;
using static Microsoft.NET.Build.Tasks.UnitTests.LockFileSnippets;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenThatWeReportAssetsLogMessages
    {
        [Fact]
        public void ItReportsDiagnosticsWithNoPackage()
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "Sample warning")
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            task.DiagnosticMessages.Should().HaveCount(1);
            log.Messages.Should().HaveCount(1);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new object[] { new string[0] })]
        public void ItReportsZeroDiagnosticsWithNoLogs(string [] logsJson)
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(logsJson);

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            task.DiagnosticMessages.Should().BeEmpty();
            log.Messages.Should().BeEmpty();
        }

        [Fact]
        public void ItReportsDiagnosticsMetadataWithLogs()
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Error, "Sample error",
                        filePath: "path/to/project.csproj",
                        libraryId: "LibA",
                        targetGraphs: new string[]{ ".NETCoreApp,Version=v1.0" }),
                    CreateLog(NuGetLogCode.NU1001, LogLevel.Warning, "Sample warning",
                        libraryId: "LibB",
                        targetGraphs: new string[]{ ".NETCoreApp,Version=v1.0" })
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            log.Messages.Should().HaveCount(2);
            task.DiagnosticMessages.Should().HaveCount(2);

            Action<string,string,string> checkMetadata = (key, val1, val2) => {
                task.DiagnosticMessages
                    .Select(item => item.GetMetadata(key))
                    .Should().Contain(new string[] { val1, val2 });
            };

            checkMetadata(MetadataKeys.DiagnosticCode, "NU1000", "NU1001");
            checkMetadata(MetadataKeys.Severity, "Error", "Warning");
            checkMetadata(MetadataKeys.Message, "Sample error", "Sample warning");
            checkMetadata(MetadataKeys.FilePath, "", "path/to/project.csproj");
            checkMetadata(MetadataKeys.ParentTarget, ".NETCoreApp,Version=v1.0", ".NETCoreApp,Version=v1.0");
            checkMetadata(MetadataKeys.ParentPackage, "LibA/1.2.3", "LibB/1.2.3");
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0" }, null)]
        [InlineData(null, "LibA")]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0" }, "LibA")]
        public void ItReportsDiagnosticsWithAllTargetLibraryCases(string[] targetGraphs, string libraryId)
        {
            var log = new MockLog();
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "Sample warning",
                        filePath: "path/to/project.csproj",
                        libraryId: libraryId,
                        targetGraphs: targetGraphs)
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent, log);

            log.Messages.Should().HaveCount(1);
            task.DiagnosticMessages.Should().HaveCount(1);
            var item = task.DiagnosticMessages.First();

            string expectedTarget = targetGraphs != null ? targetGraphs[0] : string.Empty;
            string expectedPackage = libraryId != null && targetGraphs != null ? "LibA/1.2.3" : string.Empty;

            item.GetMetadata(MetadataKeys.ParentTarget).Should().Be(expectedTarget);
            item.GetMetadata(MetadataKeys.ParentPackage).Should().Be(expectedPackage);
        }

        // MultiTFM - Only one logged,
        // Converts LogLevel to Error/Warning/Info

        private static string CreateDefaultLockFileSnippet(string[] logs = null) =>
            CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] {
                    CreateProjectFileDependencyGroup("", "LibA >= 1.2.3"), // ==> Top Level Dependency
                    NETCoreGroup
                },
                logs: logs
            );

        private ReportAssetsLogMessages GetExecutedTaskFromContents(string lockFileContents, MockLog logger)
        {
            var lockFile = TestLockFiles.CreateLockFile(lockFileContents);
            return GetExecutedTask(lockFile, logger);
        }

        private ReportAssetsLogMessages GetExecutedTask(LockFile lockFile, MockLog logger)
        {
            var task = new ReportAssetsLogMessages(lockFile, logger)
            {
                ProjectAssetsFile = lockFile.Path,
            };

            task.Execute().Should().BeTrue();

            return task;
        }
    }
}
