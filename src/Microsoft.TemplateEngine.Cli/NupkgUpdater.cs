// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;

namespace Microsoft.TemplateEngine.Cli
{
    internal class NupkgUpdater : IUpdater
    {
        public Guid Id { get; } = new Guid("DB5BF8D8-6181-496A-97DA-58616E135701");

        public Guid DescriptorFactoryId { get; } = NupkgInstallUnitDescriptorFactory.FactoryId;

        public string DisplayIdentifier { get; } = "Nupkg";

        public Task<IReadOnlyList<IUpdateUnitDescriptor>> CheckForUpdatesAsync(IReadOnlyList<IInstallUnitDescriptor> descriptorsToCheck)
        {
            throw new NotImplementedException();
            //IReadOnlyList<IUpdateUnitDescriptor> updatesFound = new List<IUpdateUnitDescriptor>();
            //return Task.FromResult(updatesFound);
        }

        public void ApplyUpdates(IInstaller installer, IReadOnlyList<IUpdateUnitDescriptor> updatesToApply)
        {
            IReadOnlyList<IUpdateUnitDescriptor> filteredUpdateToApply = updatesToApply.Where(x => x.InstallUnitDescriptor.FactoryId == DescriptorFactoryId).ToList();
            installer.InstallPackages(filteredUpdateToApply.Select(x => x.InstallString));
        }
    }
}
