// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseAliasAddCommand : BaseCommand<AliasAddCommandArgs>
    {
        internal BaseAliasAddCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName)
            : base(hostBuilder, commandName, SymbolStrings.Command_AliasAdd_Description) { }

        protected override Task<NewCommandStatus> ExecuteAsync(
            AliasAddCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context) => throw new NotImplementedException();

        protected override AliasAddCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
