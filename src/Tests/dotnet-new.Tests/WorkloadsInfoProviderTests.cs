// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.DotNet.Workloads.Workload.List;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Moq;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.Abstractions.Components;

namespace Microsoft.DotNet.New.Tests
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

            IWorkloadsRepositoryEnumerator workloadsEnumerator = new WorkloadInfoHelper(
                currentSdkVersion: "1.2.3",
                workloadRecordRepo: repoMock.Object,
                workloadResolver: resolverMock.Object);
            IWorkloadsInfoProvider wp = new WorkloadsInfoProvider(workloadsEnumerator);

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
