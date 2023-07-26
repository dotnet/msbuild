// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Moq;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.DotNet.Cli.New.Tests
{
    public class WorkloadsInfoProviderTests
    {
        [Fact]
        public void InstalledWorkloads_ShouldReturnExpectedWorkloads()
        {
            // Setup

            var resolverMock = new Mock<IWorkloadResolver>();
            var repoMock = new Mock<IWorkloadInstallationRecordRepository>();

            resolverMock
                .Setup(r => r.GetAvailableWorkloads())
                .Returns(Enumerable.Empty<WorkloadResolver.WorkloadInfo>());

            repoMock
                .Setup(r => r.GetInstalledWorkloads(It.IsAny<SdkFeatureBand>()))
                .Returns((IEnumerable<WorkloadId>)new List<WorkloadId>() { new WorkloadId("A"), new WorkloadId("B") });

            resolverMock
                .Setup(r => r.GetExtendedWorkloads(It.IsAny<IEnumerable<WorkloadId>>()))
                .Returns((IEnumerable<WorkloadId> workloadIds) => workloadIds.Select(w =>
                    new WorkloadResolver.WorkloadInfo(w, $"Description: {w.ToString()}")));

            var parseResult = Parser.Instance.Parse(new string[] { "dotnet" });
            IWorkloadsRepositoryEnumerator workloadsEnumerator = new WorkloadInfoHelper(
                isInteractive: false,
                currentSdkVersion: "1.2.3",
                workloadRecordRepo: repoMock.Object,
                workloadResolver: resolverMock.Object);
            IWorkloadsInfoProvider wp = new WorkloadsInfoProvider(new Lazy<IWorkloadsRepositoryEnumerator>(workloadsEnumerator));

            // Act
            var workloads = wp.GetInstalledWorkloadsAsync(default).Result;

            // Assert
            List<WorkloadInfo> expected = new List<WorkloadInfo>()
            {
                new WorkloadInfo("A", "Description: A"), new WorkloadInfo("B", "Description: B")
            };
            workloads.Should().Equal(expected,
                (w1, w2) => w1.Id.Equals(w2.Id) && w1.Description.Equals(w2.Description),
                "WorkloadsInfoProvider should return expected workload infos based on workload resolver and installation repository");
        }
    }
}
