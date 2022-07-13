// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Workloads.Workload.List;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.NET.TestFramework.Utilities;
using System.Collections.Generic;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using ManifestReaderTests;
using System.IO;
using System.Linq;
using ListStrings = Microsoft.DotNet.Workloads.Workload.List.LocalizableStrings;
using Microsoft.DotNet.Workloads.Workload;

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

            // Expected number of lines for table headers
            _reporter.Lines.Count.Should().Be(6);
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
        public void GivenWorkloadsAreInstalledListIsNotEmpty()
        {
            _reporter.Clear();
            var expectedWorkloads = new List<WorkloadId>() { new WorkloadId("mock-workload-1"), new WorkloadId("mock-workload-2"), new WorkloadId("mock-workload-3") };
            var workloadInstaller = new MockWorkloadRecordRepo(expectedWorkloads);
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), Directory.GetCurrentDirectory());
            var command = new WorkloadListCommand(_parseResult, _reporter, workloadInstaller, "6.0.100", workloadResolver: workloadResolver);
            command.Execute();

            foreach (var workload in expectedWorkloads)
            {
                _reporter.Lines.Select(line => line.Trim()).Should().Contain($"{workload}            5.0.0/TestProjects      SDK 6.0.100");
            }
        }

        [Fact]
        public void GivenWorkloadsAreInstalledMachineReadableListIsNotEmpty()
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
            var workloadResolver = WorkloadResolver.CreateForTests(new MockManifestProvider(new[] { _manifestPath }), testDirectory);

            // Lay out fake advertising manifests with pack version update for pack A (in workloads 1 and 3)
            var userProfileDir = Path.Combine(testDirectory, "user-profile");
            var manifestPath = Path.Combine(userProfileDir, "sdk-advertising", "6.0.100", "SampleManifest", "WorkloadManifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            File.Copy(Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "MockListSampleUpdated.json"), manifestPath);

            var command = new WorkloadListCommand(_parseResult, _reporter, workloadInstaller, "6.0.100", workloadResolver: workloadResolver, userProfileDir: userProfileDir);
            command.Execute();

            // Workloads 1 and 3 should have updates
            _reporter.Lines.Should().Contain(string.Format(ListStrings.WorkloadUpdatesAvailable, "mock-workload-1 mock-workload-3"));
        }
    }
}
