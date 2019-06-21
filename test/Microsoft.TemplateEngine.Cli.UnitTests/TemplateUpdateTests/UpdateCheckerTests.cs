// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateSearch.Common.TemplateUpdate;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateUpdateTests
{
    public class UpdateCheckerTests : TestBase
    {
        [Fact(DisplayName = nameof(UpdateCheckerCorrectlyFindsUpdate))]
        public async Task UpdateCheckerCorrectlyFindsUpdate()
        {
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockNupkgUpdater));

            // an updater that isn't related
            Guid unrelatedMountPointId = new Guid("44E776BF-0E75-43E3-97B0-1807B5207D90");
            IInstallUnitDescriptor unrelatedInstallDescriptor = new NupkgInstallUnitDescriptor(Guid.NewGuid(), unrelatedMountPointId, "unrelatedPackage", "2.0.0", "TestAuthor");
            IUpdateUnitDescriptor unrelatedUpdateDescriptor = new UpdateUnitDescriptor(unrelatedInstallDescriptor, "unrelatedPackage::2.1.0", "Unrelated Package Version 2.1.0");

            // the updater that should be found
            Guid mockMountPointId = new Guid("1EB31CA7-28C2-4AAD-B994-32A96A2EACB7");
            IInstallUnitDescriptor testInstallDescriptor = new NupkgInstallUnitDescriptor(Guid.NewGuid(), mockMountPointId, "testPackage", "1.0.0", "TestAuthor");
            IUpdateUnitDescriptor mockUpdateDescriptor = new UpdateUnitDescriptor(testInstallDescriptor, "testPackage::1.1.0", "Test Package Version 1.0.0");

            MockNupkgUpdater.SetMockUpdates(new List<IUpdateUnitDescriptor>() { mockUpdateDescriptor, unrelatedUpdateDescriptor });

            TemplateUpdateChecker updateChecker = new TemplateUpdateChecker(EngineEnvironmentSettings);

            IUpdateCheckResult updateCheckResult = await updateChecker.CheckForUpdatesAsync(new List<IInstallUnitDescriptor>() { testInstallDescriptor });
            Assert.Equal(1, updateCheckResult.Updates.Count);
            Assert.Equal(mockMountPointId, updateCheckResult.Updates[0].InstallUnitDescriptor.MountPointId);
        }

        [Fact(DisplayName = nameof(UpdateCheckerIgnoresInstallUnitWithUnknownDescriptorFactoryId))]
        public async Task UpdateCheckerIgnoresInstallUnitWithUnknownDescriptorFactoryId()
        {
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockNupkgUpdater));

            IInstallUnitDescriptor installDescriptor = new MockInstallUnitDescriptor()
            {
                Details = new Dictionary<string, string>(),
                FactoryId = new Guid("AB7803DC-5EC2-49D2-B3C4-EBC2F8B6322B"),   // no factory is registered with this Id
                Identifier = "Fake descriptor",
                MountPointId = new Guid("40BC6026-D593-4AFE-A79C-F86319FB3BD0"),
            };

            IUpdateUnitDescriptor updateDescriptor = new UpdateUnitDescriptor(installDescriptor, "FakeDescriptor::1.0.0", "Fake Descriptor Version 1.0.0");
            MockNupkgUpdater.SetMockUpdates(new List<IUpdateUnitDescriptor>() { updateDescriptor });

            TemplateUpdateChecker updateChecker = new TemplateUpdateChecker(EngineEnvironmentSettings);

            IUpdateCheckResult updateCheckResult = await updateChecker.CheckForUpdatesAsync(new List<IInstallUnitDescriptor>() { installDescriptor });
            Assert.Equal(0, updateCheckResult.Updates.Count);
        }
    }
}
