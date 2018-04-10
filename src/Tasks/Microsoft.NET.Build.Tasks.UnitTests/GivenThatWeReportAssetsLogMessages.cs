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
        public void ItReportsDiagnosticsWithMinimumData()
        {
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "Sample warning")
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent);

            task.DiagnosticMessages.Should().HaveCount(1);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new object[] { new string[0] })]
        public void ItReportsZeroDiagnosticsWithNoLogs(string [] logsJson)
        {
            string lockFileContent = CreateDefaultLockFileSnippet(logsJson);

            var task = GetExecutedTaskFromContents(lockFileContent);

            task.DiagnosticMessages.Should().BeEmpty();
        }

        [Fact]
        public void ItReportsDiagnosticsMetadataWithLogs()
        {
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

            var task = GetExecutedTaskFromContents(lockFileContent);

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
        [InlineData(null, null, ".NETCoreApp,Version=v1.0", "")]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0" }, null, ".NETCoreApp,Version=v1.0", "")]
        [InlineData(null, "LibA", ".NETCoreApp,Version=v1.0", "LibA/1.2.3")]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0" }, "LibA", ".NETCoreApp,Version=v1.0", "LibA/1.2.3")]
        public void ItReportsDiagnosticsWithAllTargetLibraryCases(string[] targetGraphs, string libraryId, string expectedTarget, string expectedPackage)
        {
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "Sample warning",
                        filePath: "path/to/project.csproj",
                        libraryId: libraryId,
                        targetGraphs: targetGraphs)
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent);

            task.DiagnosticMessages.Should().HaveCount(1);
            var item = task.DiagnosticMessages.First();

            item.GetMetadata(MetadataKeys.ParentTarget).Should().Be(expectedTarget);
            item.GetMetadata(MetadataKeys.ParentPackage).Should().Be(expectedPackage);
        }

        [Fact]
        public void ItHandlesInfoLogLevels()
        {
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Information, "Sample message"),
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Minimal, "Sample message"),
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Verbose, "Sample message"),
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Debug, "Sample message"),
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent);

            task.DiagnosticMessages.Should().HaveCount(4);

            task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.Severity))
                    .Should().OnlyContain(s => s == "Info");
        }

        [Theory]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0", ".NETFramework,Version=v4.6.1" }, "LibA")]
        [InlineData(new string[] { ".NETCoreApp,Version=v1.0" }, "LibA")]
        public void ItHandlesMultiTFMScenarios(string[] targetGraphs, string libraryId)
        {
            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                    CreateTarget(".NETFramework,Version=v4.6.1", TargetLibA, TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] {
                    ProjectGroup, NETCoreGroup, NET461Group
                },
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Warning, "Sample warning",
                        filePath: "path/to/project.csproj",
                        libraryId: libraryId,
                        targetGraphs: targetGraphs)
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent);

            // a diagnostic for each target graph...
            task.DiagnosticMessages.Should().HaveCount(targetGraphs.Length);

            task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.ParentTarget))
                    .Should().Contain(targetGraphs);

            task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.ParentPackage))
                    .Should().OnlyContain(v => v.StartsWith(libraryId));
        }

        [Fact]
        public void ItSkipsInvalidEntries()
        {
            string lockFileContent = CreateDefaultLockFileSnippet(
                logs: new string[] {
                    CreateLog(NuGetLogCode.NU1000, LogLevel.Error, "Sample error that will be invalid"),
                    CreateLog(NuGetLogCode.NU1001, LogLevel.Warning, "Sample warning"),
                }
            );
            lockFileContent = lockFileContent.Replace("NU1000", "CA1000");

            var task = GetExecutedTaskFromContents(lockFileContent);

            task.DiagnosticMessages.Should().HaveCount(1);

            task.DiagnosticMessages
                    .Select(item => item.GetMetadata(MetadataKeys.DiagnosticCode))
                    .Should().OnlyContain(v => v == "NU1001");
        }

        private static string CreateDefaultLockFileSnippet(string[] logs = null) =>
            CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] {
                    ProjectGroup, NETCoreGroup
                },
                logs: logs
            );

        private ReportAssetsLogMessages GetExecutedTaskFromContents(string lockFileContents)
        {
            var lockFile = TestLockFiles.CreateLockFile(lockFileContents);
            return GetExecutedTask(lockFile);
        }

        private ReportAssetsLogMessages GetExecutedTask(LockFile lockFile)
        {
            var task = new ReportAssetsLogMessages(lockFile)
            {
                ProjectAssetsFile = lockFile.Path,
            };

            task.Execute().Should().BeTrue();

            return task;
        }
    }
}
