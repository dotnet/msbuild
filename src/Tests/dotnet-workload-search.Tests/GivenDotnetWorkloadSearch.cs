// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.DotNet.Workloads.Workload.Search;
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
        private readonly IEnumerable<WorkloadDefinition> _avaliableWorkloads =
            new List<WorkloadDefinition>()
            {
                new WorkloadDefinition(new WorkloadId("mock-workload-1"), false, null, WorkloadDefinitionKind.Dev, new List<WorkloadId>(), 
                    new List<WorkloadPackId>(), new List<string>()),
                new WorkloadDefinition(new WorkloadId("mock-workload-2"), false, string.Empty, WorkloadDefinitionKind.Build, null, 
                    new List<WorkloadPackId>(), new List<string>() { "platform1", "platform2" }),
                new WorkloadDefinition(new WorkloadId("mock-workload-3"), true, "Fake description 1", WorkloadDefinitionKind.Dev, 
                    new List<WorkloadId>() { new WorkloadId("mock-workload-2") }, new List<WorkloadPackId>(), new List<string>()),
                new WorkloadDefinition(new WorkloadId("fake-workload-1"), true, null, WorkloadDefinitionKind.Build, 
                    new List<WorkloadId>(), new List<WorkloadPackId>(), null),
                new WorkloadDefinition(new WorkloadId("fake-workload-2"), false, "Fake description 2", WorkloadDefinitionKind.Dev, null, 
                    new List<WorkloadPackId>(), new List<string>())
            };

        public GivenDotnetWorkloadSearch(ITestOutputHelper log) : base(log)
        {
            _reporter = new BufferedReporter();
        }

        [Fact]
        public void GivenNoWorkloadsAreInstalledSearchIsEmpty()
        {
            _reporter.Clear();
            var parseResult = Parser.GetWorkloadsInstance.Parse("dotnet workload search");
            var workloadResolver = new MockWorkloadResolver(new List<WorkloadDefinition>());
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            _reporter.Lines.Count.Should().Be(4, because: "Output should have header and no values.");
        }

        [Fact]
        public void GivenNoStubIsProvidedSearchShowsAllWorkloads()
        {
            _reporter.Clear();
            var parseResult = Parser.GetWorkloadsInstance.Parse("dotnet workload search");
            var workloadResolver = new MockWorkloadResolver(_avaliableWorkloads);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            var output = string.Join(" ", _reporter.Lines);
            foreach (var workload in _avaliableWorkloads)
            {
                output.Contains(workload.Id.ToString()).Should().BeTrue();
                if (workload.Description != null)
                {
                    output.Contains(workload.Description).Should().BeTrue();
                }
                if (workload.Platforms != null && workload.Platforms.Any())
                {
                    output.Contains(workload.Platforms.First().ToString()).Should().BeFalse();
                }
            }
        }

        [Fact]
        public void GivenDetailedVerbositySearchShowsAllColumns()
        {
            _reporter.Clear();
            var parseResult = Parser.GetWorkloadsInstance.Parse("dotnet workload search -v d");
            var workloadResolver = new MockWorkloadResolver(_avaliableWorkloads);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            var output = string.Join(" ", _reporter.Lines);
            foreach (var workload in _avaliableWorkloads)
            {
                output.Contains(workload.Id.ToString()).Should().BeTrue();
                if (workload.Description != null)
                {
                    output.Contains(workload.Description).Should().BeTrue();
                }
                if (workload.Platforms != null && workload.Platforms.Any())
                {
                    output.Contains(workload.Platforms.First().ToString()).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void GivenStubIsProvidedSearchShowsAllMatchingWorkloads()
        {
            _reporter.Clear();
            var parseResult = Parser.GetWorkloadsInstance.Parse("dotnet workload search mock");
            var workloadResolver = new MockWorkloadResolver(_avaliableWorkloads);
            var command = new WorkloadSearchCommand(parseResult, _reporter, workloadResolver, "6.0.100");
            command.Execute();

            var output = string.Join(" ", _reporter.Lines);
            var expectedWorkloads = _avaliableWorkloads.Take(3);
            foreach (var workload in expectedWorkloads)
            {
                output.Contains(workload.Id.ToString()).Should().BeTrue();
                if (workload.Description != null)
                {
                    output.Contains(workload.Description).Should().BeTrue();
                }
            }
        }
    }
}
