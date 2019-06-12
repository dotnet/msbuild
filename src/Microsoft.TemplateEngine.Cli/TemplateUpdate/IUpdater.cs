// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Cli.TemplateUpdater
{
    // This interface has to stay in the CLI because of its reference to IInstaller
    public interface IUpdater : IIdentifiedComponent
    {
        Guid DescriptorFactoryId { get; }

        string DisplayIdentifier { get; }

        void Configure(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IInstallUnitDescriptor> existingInstallDescriptors);

        Task<IReadOnlyList<IUpdateUnitDescriptor>> CheckForUpdatesAsync(IReadOnlyList<IInstallUnitDescriptor> installDescriptorsToCheck);

        void ApplyUpdates(IInstaller installer, IReadOnlyList<IUpdateUnitDescriptor> updatesToApply);
    }
}
