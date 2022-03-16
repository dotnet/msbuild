// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Collections;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.WorkloadManifestReader.Tests
{
    public class WorkloadPackBundleTests : SdkTest
    {
        public WorkloadPackBundleTests(ITestOutputHelper log) : base(log)
        {
        }


        [Fact]
        public void TestGetManifestDirectories()
        {
            var manifestProvider = CreateManifestProvider();

            var manifestDirectories = manifestProvider.GetManifestDirectories();
            foreach (var manifestDirectory in manifestDirectories)
            {
                Log.WriteLine(manifestDirectory);
            }
        }

        [Fact]
        public void TestGetManifests()
        {
            var manifests = GetManifests();

            foreach (var manifest in manifests)
            {
                Log.WriteLine(manifest.Id + "\t" + manifest.ManifestPath);
            }
        }

        [Fact]
        public void GetPackDefinitionLocations()
        {
            var definitionLocations = GetWorkloadPackDefinitionLocations(GetManifests());

            StringBuilder sb = new StringBuilder(); ;
            foreach (var kvp in definitionLocations)
            {
                sb.Append(kvp.Key + ": ");
                sb.Append(string.Join(", ", kvp.Value));
                Log.WriteLine(sb.ToString());
                sb.Clear();
            }

            foreach (var kvp in definitionLocations)
            {
                kvp.Value.Count.Should().Be(1);
            }
        }

        [Fact]
        public void TestGetPackBundles()
        {
            var packBundles = GetPackBundles();
            foreach (var bundle in packBundles)
            {
                Log.WriteLine(bundle.Workload.Id);
                foreach (var pack in bundle.Packs)
                {
                    if (pack.Id != pack.ResolvedPackageId)
                    {
                        Log.WriteLine($"\t{pack.Id}\t{pack.Version}\t{pack.ResolvedPackageId}");
                    }
                    else
                    {
                        Log.WriteLine($"\t{pack.Id}\t{pack.Version}");
                    }
                }
                foreach (var unavailablePack in bundle.UnavailablePacks)
                {
                    Log.WriteLine($"\tUnavailable: {unavailablePack}");
                }
            }
        }


        SdkDirectoryWorkloadManifestProvider CreateManifestProvider()
        {
            return new(TestContext.Current.ToolsetUnderTest.DotNetRoot, TestContext.Current.ToolsetUnderTest.SdkVersion, userProfileDir: null);
        }

        public IEnumerable<WorkloadManifest> GetManifests(SdkDirectoryWorkloadManifestProvider? manifestProvider = null)
        {
            manifestProvider ??= CreateManifestProvider();
            List<WorkloadManifest> manifests = new List<WorkloadManifest>();
            foreach (var readableManifest in manifestProvider.GetManifests())
            {
                if (readableManifest.ManifestId.Equals("Microsoft.NET.Sdk.TestWorkload"))
                {
                    //  Ignore test workload for this
                    continue;
                }
                using (var stream = readableManifest.OpenManifestStream())
                {
                    var manifest = WorkloadManifestReader.ReadWorkloadManifest(readableManifest.ManifestId, stream, readableManifest.ManifestPath);
                    manifests.Add(manifest);
                }
            }

            return manifests;
        }


        //  Implementation here
        Dictionary<WorkloadPackId, List<WorkloadId>> GetWorkloadPackDefinitionLocations(IEnumerable<WorkloadManifest> manifests)
        {
            var ret = new Dictionary<WorkloadPackId, List<WorkloadId>>();
            foreach (var manifest in manifests)
            {
                foreach (var baseWorkload in manifest.Workloads.Values)
                {
                    if (baseWorkload is WorkloadDefinition workload && workload.Packs != null)
                    {
                        foreach (var pack in workload.Packs)
                        {
                            if (!ret.TryGetValue(pack, out List<WorkloadId>? workloadList))
                            {
                                workloadList = new List<WorkloadId>();
                                ret[pack] = workloadList;
                            }
                            workloadList.Add(workload.Id);
                        }
                    }
                }
            }
            return ret;
        }

        List<WorkloadPackBundle> GetPackBundles()
        {
            List<WorkloadPackBundle> bundles = new List<WorkloadPackBundle>();

            var manifestProvider = CreateManifestProvider();
            var manifests = GetManifests(manifestProvider);
            var workloadResolver = WorkloadResolver.CreateForTests(manifestProvider, TestContext.Current.ToolsetUnderTest.DotNetRoot);

            foreach (var manifest in manifests)
            {
                foreach (var workload in manifest.Workloads.Values.OfType<WorkloadDefinition>())
                {
                    if (workload.Packs == null || !workload.Packs.Any())
                    {
                        continue;
                    }
                    List<WorkloadResolver.PackInfo> packInfos = new List<WorkloadResolver.PackInfo>();
                    List<WorkloadPackId> unavailablePacks = new List<WorkloadPackId>();
                    foreach (var packId in workload.Packs)
                    {
                        var packInfo = workloadResolver.TryGetPackInfo(packId);
                        if (packInfo == null)
                        {
                            unavailablePacks.Add(packId);
                        }
                        else
                        {
                            packInfos.Add(packInfo);
                        }
                    }
                    WorkloadPackBundle bundle = new WorkloadPackBundle(workload, packInfos, unavailablePacks);
                    bundles.Add(bundle);
                }
            }
            

            return bundles;
        }

        class WorkloadPackBundle
        {
            public WorkloadDefinition Workload { get; }
            public List<WorkloadResolver.PackInfo> Packs { get; }
            public List<WorkloadPackId> UnavailablePacks { get; }

            public WorkloadPackBundle(WorkloadDefinition workload, List<WorkloadResolver.PackInfo> packs, List<WorkloadPackId> unavailablePacks)
            {
                Workload = workload;
                Packs = packs;
                UnavailablePacks = unavailablePacks;
            }
        }
    }
}
