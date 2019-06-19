// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateUpdateTests
{
    public class MockNupkgUpdater : IUpdater
    {
        // a mapping from install descriptor identifiers to the update that should be emitted.
        [ThreadStatic]
        private static IReadOnlyDictionary<string, IUpdateUnitDescriptor> _mockUpdates = new Dictionary<string, IUpdateUnitDescriptor>();

        public void Configure(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IInstallUnitDescriptor> existingInstallDescriptors)
        {
            // do nothing, this mock isn't trying to do real matches, so no need to store / use the existingInstallDescriptors
        }

        // Pass in the update descriptors that should be emitted.
        // The checker matches them based on the IInstallUnitDescriptor.Identifier
        public static void SetMockUpdates(IReadOnlyList<IUpdateUnitDescriptor> mockUpdates)
        {
            _mockUpdates = mockUpdates.ToDictionary(x => x.InstallUnitDescriptor.Identifier, x => x);
        }

        public Guid Id { get; } = new Guid("6E890524-D98B-4A0E-BE91-10794A5B50D0");

        public Guid DescriptorFactoryId => NupkgInstallUnitDescriptorFactory.FactoryId;

        public string DisplayIdentifier { get; } = "MockUpdater";

        public Task<IReadOnlyList<IUpdateUnitDescriptor>> CheckForUpdatesAsync(IReadOnlyList<IInstallUnitDescriptor> installDescriptorsToCheck)
        {
            List<IUpdateUnitDescriptor> updateDescriptorList = new List<IUpdateUnitDescriptor>();

            foreach (IInstallUnitDescriptor installDescriptor in installDescriptorsToCheck)
            {
                if (_mockUpdates.TryGetValue(installDescriptor.Identifier, out IUpdateUnitDescriptor updateDescriptor))
                {
                    updateDescriptorList.Add(updateDescriptor);
                }
            }

            IReadOnlyList<IUpdateUnitDescriptor> resultList = updateDescriptorList;
            return Task.FromResult(resultList);
        }

        public void ApplyUpdates(IInstallerBase installer, IReadOnlyList<IUpdateUnitDescriptor> updatesToApply)
        {
            if (updatesToApply.Any(x => x.InstallUnitDescriptor.FactoryId != DescriptorFactoryId))
            {
                throw new Exception("Incorrect descriptor type");
            }

            installer.InstallPackages(updatesToApply.Select(x => x.InstallString));
        }
    }
}
