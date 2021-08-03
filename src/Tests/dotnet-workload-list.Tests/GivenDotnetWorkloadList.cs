// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Workloads.Workload.List;
using System.CommandLine.Parsing;
using Microsoft.NET.TestFramework.Utilities;
using System.Collections.Generic;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using ManifestReaderTests;
using System.IO;

namespace Microsoft.DotNet.Cli.Workload.List.Tests
{
    public class GivenDotnetWorkloadList : SdkTest
    {
        private readonly ParseResult _machineReadableParseResult;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _manifestPath;

        public GivenDotnetWorkloadList(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
            _machineReadableParseResult = Parser.Instance.Parse("dotnet workload list --machine-readable");
            _parseResult = Parser.Instance.Parse("dotnet workload list");
            _manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockListSample.json");
        }

        [Fact]
        public void GivenNoWorkloadsAreInstalledListIsEmpty()
        {
            _reporter.Clear();
            var expectedWorkloads = new List<WorkloadId>();
            var workloadInstaller = new MockWorkloadRecordRepo(expectedWorkloads);
            var command = new WorkloadListCommand(_parseResult, _reporter, workloadInstaller, "6.0.100");
            command.Execute();

            // Expect 3 lines for table headers
            _reporter.Lines.Count.Should().Be(3);
        }

        [Fact]
        public void GivenNoWorkloadsAreInstalledMachineReadableListIsEmpty()
        {
            _reporter.Clear();
            var expectedWorkloads = new List<WorkloadId>();
            var workloadInstaller = new MockWorkloadRecordRepo(expectedWorkloads);
            var command = new WorkloadListCommand(_machineReadableParseResult, _reporter, workloadInstaller, "6.0.100");
            command.Execute();

            _reporter.Lines.Should().Contain(l => l.Contains(@"""installed"":[]"));
        }

        [Fact]
        public void GivenNoWorkloadsAreInstalledListIsNotEmpty()
        {
            _reporter.Clear();
            var expectedWorkloads = new List<WorkloadId>() { new WorkloadId("mock-workload-1"), new WorkloadId("mock-workload-2"), new WorkloadId("mock-workload-3") };
            var workloadInstaller = new MockWorkloadRecordRepo(expectedWorkloads);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { Directory.GetCurrentDirectory() });
            var command = new WorkloadListCommand(_parseResult, _reporter, workloadInstaller, "6.0.100", workloadResolver: workloadResolver);
            command.Execute();

            foreach (var workload in expectedWorkloads)
            {
                _reporter.Lines.Should().Contain(workload.ToString());
            }
        }

        [Fact]
        public void GivenNoWorkloadsAreInstalledMachineReadableListIsNotEmpty()
        {
            _reporter.Clear();
            var expectedWorkloads = new List<WorkloadId>() { new WorkloadId("mock-workload-1"), new WorkloadId("mock-workload-2"), new WorkloadId("mock-workload-3") };
            var workloadInstaller = new MockWorkloadRecordRepo(expectedWorkloads);
            var command = new WorkloadListCommand(_machineReadableParseResult, _reporter, workloadInstaller, "6.0.100");
            command.Execute();

            _reporter.Lines.Should().Contain(l => l.Contains("{\"installed\":[\"mock-workload-1\",\"mock-workload-2\",\"mock-workload-3\"]"));
        }

        [Fact]
        public void GivenWorkloadsAreOutOfDateUpdatesAreAdvertised()
        {
            _reporter.Clear();
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var expectedWorkloads = new List<WorkloadId>() { new WorkloadId("mock-workload-1"), new WorkloadId("mock-workload-2"), new WorkloadId("mock-workload-3") };
            var workloadInstaller = new MockWorkloadRecordRepo(expectedWorkloads);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), new string[] { testDirectory });

            // Lay out fake advertising manifests with pack version update for pack A (in workloads 1 and 3)
            var userHome = Path.Combine(testDirectory, "userHome");
            var manifestPath = Path.Combine(userHome, ".dotnet", "sdk-advertising", "6.0.100", "SampleManifest", "WorkloadManifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            File.Copy(Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockListSampleUpdated.json"), manifestPath);

            var command = new WorkloadListCommand(_parseResult, _reporter, workloadInstaller, "6.0.100", workloadResolver: workloadResolver, userHome: userHome);
            command.Execute();

            // Workloads 1 and 3 should have updates
            _reporter.Lines.Should().Contain(string.Format(LocalizableStrings.WorkloadUpdatesAvailable, "mock-workload-1 mock-workload-3"));
        }
    }
}
