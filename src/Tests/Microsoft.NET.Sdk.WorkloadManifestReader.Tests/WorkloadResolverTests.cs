// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManifestReaderTests;
using System.IO;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.WorkloadManifestReader.Tests
{
    public class WorkloadResolverTests: SdkTest
    {
        private const string fakeRootPath = "fakeRootPath";

        public WorkloadResolverTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void GetExtendedWorkloads_SampleDeduplicatedClosureExpected()
        {
            var manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
            var workloadResolver = WorkloadResolver.CreateForTests(new FakeManifestProvider(manifestPath), fakeRootPath);

            var resultWorkloads = workloadResolver.GetExtendedWorkloads(new List<WorkloadId>()
            {
                new WorkloadId("xamarin-android-build-x86"),
                new WorkloadId("xamarin-empty-mock"),
                new WorkloadId("xamarin-android"),
            }).ToList();

            List<WorkloadResolver.WorkloadInfo> expected = new()
            {
                new(new WorkloadId("xamarin-android-build-x86"), null),
                new(new WorkloadId("xamarin-android-build"), "Build and run Android apps"),
                new(new WorkloadId("xamarin-empty-mock"), "Empty mock workload for testing"),
                new(new WorkloadId("xamarin-android"), "Create, build and run Android apps"),
                new(new WorkloadId("xamarin-android-build-armv7a"), null),
            };

            resultWorkloads.Should().Equal(expected,
                (w1, w2) => w1.Id.Equals(w2.Id) && string.Equals(w1.Description, w2.Description),
                "WorkloadResolver should return expected workload infos based on manifest");
        }

        [Fact]
        public void GetExtendedWorkloads_EmptyInputYieldsEmptyOutput()
        {
            var manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
            var workloadResolver = WorkloadResolver.CreateForTests(new FakeManifestProvider(manifestPath), fakeRootPath);

            var resultWorkloads = workloadResolver.GetExtendedWorkloads(Enumerable.Empty<WorkloadId>()).ToList();

            resultWorkloads.Should().BeEmpty();
        }

        [Fact]
        public void GetExtendedWorkloads_ThrowsOnUnknownWorkload()
        {
            var manifestPath = Path.Combine(_testAssetsManager.GetAndValidateTestProjectDirectory("SampleManifest"), "Sample.json");
            var workloadResolver = WorkloadResolver.CreateForTests(new FakeManifestProvider(manifestPath), fakeRootPath);

            Exception exc = Assert.Throws<WorkloadManifestCompositionException>(() => 
                workloadResolver.GetExtendedWorkloads(new List<WorkloadId>() { new WorkloadId("BAH"), }).ToList());

            exc.Message.Should().StartWith("Could not find workload 'BAH'");
        }
    }
}
