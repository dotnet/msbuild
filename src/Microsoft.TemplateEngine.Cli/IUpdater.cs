// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Cli
{
    public interface IUpdater : IIdentifiedComponent
    {
        Guid DescriptorFactoryId { get; }

        Task<IReadOnlyList<IUpdateUnitDescriptor>> CheckForUpdatesAsync(IReadOnlyList<IInstallUnitDescriptor> installDescriptorsToCheck);

        string DisplayIdentifier { get; }

        void ApplyUpdates(IInstaller installer, IReadOnlyList<IUpdateUnitDescriptor> updatesToApply);
    }
}
