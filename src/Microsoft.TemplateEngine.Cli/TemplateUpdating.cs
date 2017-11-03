// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Cli
{
    public class TemplateUpdating
    {
        public TemplateUpdating(IEngineEnvironmentSettings environmentSettings, IInstaller installer, Func<string> inputGetter)
        {
            _environmentSettings = environmentSettings;
            _installer = installer;
            _inputGetter = inputGetter;
        }

        private IEngineEnvironmentSettings _environmentSettings;
        private IInstaller _installer;
        private Func<string> _inputGetter;

        public void Update(IReadOnlyList<IInstallUnitDescriptor> installUnitsToCheck)
        {
            BaseUpdate(installUnitsToCheck, false);
        }

        public void UpdateWithoutPrompting(IReadOnlyList<IInstallUnitDescriptor> installUnitsToCheck)
        {
            BaseUpdate(installUnitsToCheck, true);
        }

        private void BaseUpdate(IReadOnlyList<IInstallUnitDescriptor> installUnitsToCheck, bool applyWithoutPrompting)
        {
            TemplateUpdateCoordinator coordinator = new TemplateUpdateCoordinator(_environmentSettings, _installer);
            coordinator.UpdateTemplates(installUnitsToCheck, _inputGetter, applyWithoutPrompting);
        }
    }
}
