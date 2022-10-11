// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyInstallCommand : BaseInstallCommand
    {
        public LegacyInstallCommand(NewCommand parentCommand, Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(parentCommand, hostBuilder, "--install")
        {
            this.IsHidden = true;
            this.AddAlias("-i");

            parentCommand.AddNoLegacyUsageValidators(this, except: new Option[] { InteractiveOption, AddSourceOption });
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<string[]> AddSourceOption => ParentCommand.AddSourceOption;

        protected override Task<NewCommandStatus> ExecuteAsync(InstallCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, InvocationContext context)
        {
            PrintDeprecationMessage<LegacyInstallCommand, InstallCommand>(args.ParseResult);
            return base.ExecuteAsync(args, environmentSettings, templatePackageManager, context);
        }
    }
}
