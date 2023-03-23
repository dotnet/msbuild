// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Workloads.Workload.Search;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.NET.TestFramework.Utilities;
using System.Collections.Generic;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Linq;

namespace Microsoft.DotNet.Cli.Workload.Search.Tests
{
    public class GivenDotnetWorkloadSearch : SdkTest
    {
        private readonly BufferedReporter _reporter;
        private readonly IEnumerable<WorkloadResolver.WorkloadInfo> _availableWorkloads =
            new List<WorkloadResolver.WorkloadInfo>()
            {
                CreateWorkloadInfo("mock-workload-1"),
                CreateWorkloadInfo("mock-workload-2"),
                CreateWorkloadInfo("mock-workload-3"),
                CreateWorkloadInfo("fake-workload-1"),
                CreateWorkloadInfo("fake-workload-2", "Fake description 2")
            };

        static WorkloadResolver.WorkloadInfo CreateWorkloadInfo(string id, string description = null)
            => new WorkloadResolver.WorkloadInfo(new WorkloadId(id), description);

        public GivenDotnetWorkloadSearch(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void GivenNoWorkloadsAreInstalledSearchIsEmpty()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search");
            var workloadResolver = new MockWorkloadResolver(Enumerable.Empty<WorkloadResolver.WorkloadInfo>());
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            _reporter.Lines.Count.Should().Be(4, because: "Output should have header and no values.");
        }

        [Fact]
        public void GivenNoStubIsProvidedSearchShowsAllWorkloads()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            var output = string.Join(" ", _reporter.Lines);
            foreach (var workload in _availableWorkloads)
            {
                output.Contains(workload.Id.ToString()).Should().BeTrue();
                if (workload.Description != null)
                {
                    output.Contains(workload.Description).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void GivenDetailedVerbositySearchShowsAllColumns()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search -v d");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            var output = string.Join(" ", _reporter.Lines);
            foreach (var workload in _availableWorkloads)
            {
                output.Contains(workload.Id.ToString()).Should().BeTrue();
                if (workload.Description != null)
                {
                    output.Contains(workload.Description).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void GivenStubIsProvidedSearchShowsAllMatchingWorkloads()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search mock");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            var output = string.Join(" ", _reporter.Lines);
            var expectedWorkloads = _availableWorkloads.Take(3);
            foreach (var workload in expectedWorkloads)
            {
                output.Contains(workload.Id.ToString()).Should().BeTrue();
                if (workload.Description != null)
                {
                    output.Contains(workload.Description).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void GivenSearchResultsAreOrdered()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            _reporter.Lines[3].Should().Contain("fake-workload-1");
            _reporter.Lines[4].Should().Contain("fake-workload-2");
            _reporter.Lines[5].Should().Contain("mock-workload-1");
            _reporter.Lines[6].Should().Contain("mock-workload-2");
            _reporter.Lines[7].Should().Contain("mock-workload-3");
        }

        [Fact]
        public void GivenWorkloadSearchItSearchesDescription()
        {
            _reporter.Clear();
            var parseResult = Parser.Instance.Parse("dotnet workload search description");
            var workloadResolver = new MockWorkloadResolver(_availableWorkloads);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            _reporter.Lines.Count.Should().Be(5);
            _reporter.Lines[3].Should().Contain("fake-workload-2");
        }
    }
}
