// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class SearchCommand : BaseSearchCommand
    {
        public SearchCommand(
                NewCommand parentCommand,
                Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(parentCommand, hostBuilder, "search")
        {
            parentCommand.AddNoLegacyUsageValidators(this);
        }

        protected override async Task<NewCommandStatus> ExecuteAsync(
            SearchCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context)
        {
            NewCommandStatus status = await base.ExecuteAsync(args, environmentSettings, templatePackageManager, context).ConfigureAwait(false);
            await CheckTemplatesWithSubCommandName(args, templatePackageManager, context.GetCancellationToken()).ConfigureAwait(false);
            return status;
        }
    }
}
