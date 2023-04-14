// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.Versioning;
using System.IO;
using Microsoft.DotNet.Workloads.Workload.Install.InstallRecord;
using Microsoft.NET.TestFramework;
using Microsoft.Win32;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Xunit;

namespace Microsoft.DotNet.Cli.Workload.List.Tests
{
    [Collection("MsiWorkloadRecords")]
    [SupportedOSPlatform("windows")]
    public class GivenAnMsiInstallation : IDisposable
    {
        // Override HKLM to HKCU so we can run tests without needing elevation
        private static RegistryWorkloadInstallationRecordRepository RecordManager = new RegistryWorkloadInstallationRecordRepository(
            new TestElevationContext(),
            null,
            Registry.CurrentUser,
            @"SOFTWARE\Microsoft\dotnet-test\InstalledWorkloads\Standalone");

        [WindowsOnlyFact]
        public void GivenExistingRecordsItCanDetermineInstalledWorkloads()
        {
            CreateWorkloadRecord("6.0.100", "workload.A");
            CreateWorkloadRecord("6.0.100", "workload.B");
            CreateWorkloadRecord("6.0.200", "workload.C");

            var records = RecordManager.GetInstalledWorkloads(new SdkFeatureBand("6.0.200"));

            Assert.Contains(new WorkloadId("workload.C"), records);
        }

        [WindowsOnlyFact]
        public void GivenExistingRecordsItCanDeleteRecords()
        {
            CreateWorkloadRecord("6.0.100", "workload.A");
            CreateWorkloadRecord("6.0.100", "workload.B");
            CreateWorkloadRecord("6.0.200", "workload.C");

            var records = RecordManager.GetInstalledWorkloads(new SdkFeatureBand("6.0.100"));

            Assert.Contains(new WorkloadId("workload.B"), records);
            RecordManager.DeleteWorkloadInstallationRecord(new WorkloadId("workload.B"), new SdkFeatureBand("6.0.100"));

            records = RecordManager.GetInstalledWorkloads(new SdkFeatureBand("6.0.100"));
            Assert.DoesNotContain(new WorkloadId("workload.B"), records);
        }

        [WindowsOnlyFact]
        public void GivenExistingRecordsItOnlyEnumeratesFeatureBandsWithWorkloads()
        {
            CreateWorkloadRecord("6.0.100", "workload.A");
            CreateWorkloadRecord("6.0.100", "workload.B");
            CreateWorkloadRecord("6.0.200", "workload.C");

            Registry.CurrentUser.CreateSubKey(Path.Combine(RecordManager.BasePath, "6.0.300"));

            var records = RecordManager.GetFeatureBandsWithInstallationRecords();

            Assert.DoesNotContain(new SdkFeatureBand("6.0.300"), records);
        }

        public void Dispose()
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\Microsoft\dotnet-test", throwOnMissingSubKey: false);
        }

        private void CreateWorkloadRecord(string sdkFeatureBand, string workloadId)
        {
            RecordManager.WriteWorkloadInstallationRecord(new WorkloadId(workloadId), new SdkFeatureBand(sdkFeatureBand));
        }
    }

    internal class TestElevationContext : InstallElevationContextBase
    {
        public override bool IsClient => true;

        public override bool IsElevated => true;

        public override void Elevate()
        {
        }            
    }
}
