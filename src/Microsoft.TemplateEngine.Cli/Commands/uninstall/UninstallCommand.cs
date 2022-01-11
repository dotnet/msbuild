// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UninstallCommand : BaseUninstallCommand
    {
        public UninstallCommand(
            NewCommand parentCommand,
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks)
            : base(host, logger, callbacks, "uninstall")
        {
            parentCommand.AddNoLegacyUsageValidators(this);
        }
    }
}
