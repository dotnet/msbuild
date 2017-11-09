// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateUpdateTests
{
    public class UpdateCoordinatorTests : TestBase
    {
        [Fact(DisplayName = nameof(UpdateIsFoundAndApplied))]
        public void UpdateIsFoundAndApplied()
        {
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockNupkgUpdater));

            IInstallUnitDescriptor installDescriptor = new MockInstallUnitDescriptor()
            {
                Details = new Dictionary<string, string>(),
                FactoryId = NupkgInstallUnitDescriptorFactory.FactoryId,
                Identifier = "MockPackage",
                MountPointId = new Guid("C5A4D83F-7005-4B38-BF47-DFF5CB5F5881"),
                UserReadableIdentifier = "Mock Package"
            };
            List<IInstallUnitDescriptor> installsToUpdate = new List<IInstallUnitDescriptor>() { installDescriptor };

            IUpdateUnitDescriptor updateDescriptor = new UpdateUnitDescriptor(installDescriptor, "MockPackageToInstall", "Mock Package To Install");
            MockNupkgUpdater.SetMockUpdates(new List<IUpdateUnitDescriptor>() { updateDescriptor });

            // start with nothing "installed", so checking what was installed can happen.
            MockInstaller installer = new MockInstaller();

            TemplateUpdateCoordinator coordinator = new TemplateUpdateCoordinator(EngineEnvironmentSettings, installer);
            Assert.Empty(installer.Installed);
            coordinator.UpdateTemplates(installsToUpdate, () => Console.ReadLine(), true);
            Assert.Single(installer.Installed);
            Assert.Contains(updateDescriptor.InstallString, installer.Installed);
        }

        [Fact(DisplayName = nameof(NoUpdatesFoundDoesNothing))]
        public void NoUpdatesFoundDoesNothing()
        {
            EngineEnvironmentSettings.SettingsLoader.Components.Register(typeof(MockNupkgUpdater));

            // start with nothing "installed", so checking what was installed can happen.
            MockInstaller installer = new MockInstaller();

            IInstallUnitDescriptor installDescriptor = new MockInstallUnitDescriptor()
            {
                Details = new Dictionary<string, string>(),
                FactoryId = NupkgInstallUnitDescriptorFactory.FactoryId,
                Identifier = "MockPackage",
                MountPointId = new Guid("C5A4D83F-7005-4B38-BF47-DFF5CB5F5881"),
                UserReadableIdentifier = "Mock Package"
            };

            TemplateUpdateCoordinator coordinator = new TemplateUpdateCoordinator(EngineEnvironmentSettings, installer);
            Assert.Empty(installer.Installed);

            List<IInstallUnitDescriptor> installsToUpdate = new List<IInstallUnitDescriptor>();
            coordinator.UpdateTemplates(installsToUpdate, () => Console.ReadLine(), true);
            Assert.Empty(installer.Installed);
        }

        [Fact(DisplayName = nameof(NoUpdatersRegisteredDoesNothing))]
        public void NoUpdatersRegisteredDoesNothing()
        {
            // start with nothing "installed", so checking what was installed can happen.
            MockInstaller installer = new MockInstaller();

            IInstallUnitDescriptor installDescriptor = new MockInstallUnitDescriptor()
            {
                Details = new Dictionary<string, string>(),
                FactoryId = NupkgInstallUnitDescriptorFactory.FactoryId,
                Identifier = "MockPackage",
                MountPointId = new Guid("C5A4D83F-7005-4B38-BF47-DFF5CB5F5881"),
                UserReadableIdentifier = "Mock Package"
            };

            TemplateUpdateCoordinator coordinator = new TemplateUpdateCoordinator(EngineEnvironmentSettings, installer);
            Assert.Empty(installer.Installed);

            List<IInstallUnitDescriptor> installsToUpdate = new List<IInstallUnitDescriptor>();
            coordinator.UpdateTemplates(installsToUpdate, () => Console.ReadLine(), true);
            Assert.Empty(installer.Installed);
        }
    }
}
