// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyUninstallCommand : BaseUninstallCommand
    {
        public LegacyUninstallCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(hostBuilder, "--uninstall")
        {
            this.IsHidden = true;
            this.AddAlias("-u");

            parentCommand.AddNoLegacyUsageValidators(this);
        }

        protected override Task<NewCommandStatus> ExecuteAsync(UninstallCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, InvocationContext context)
        {
            PrintDeprecationMessage<LegacyUninstallCommand, UninstallCommand>(args.ParseResult);
            return base.ExecuteAsync(args, environmentSettings, templatePackageManager, context);
        }
    }
}
