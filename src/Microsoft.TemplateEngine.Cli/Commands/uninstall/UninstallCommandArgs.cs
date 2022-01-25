// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class UninstallCommandArgs : GlobalArgs
    {
        public UninstallCommandArgs(BaseUninstallCommand uninstallCommand, ParseResult parseResult) : base(uninstallCommand, parseResult)
        {
            TemplatePackages = parseResult.GetValueForArgument(BaseUninstallCommand.NameArgument) ?? Array.Empty<string>();

            //workaround for --install source1 --install source2 case
            if (uninstallCommand is LegacyUninstallCommand && uninstallCommand.Aliases.Any(alias => TemplatePackages.Contains(alias)))
            {
                TemplatePackages = TemplatePackages.Where(package => !uninstallCommand.Aliases.Contains(package)).ToList();
            }
        }

        public IReadOnlyList<string> TemplatePackages { get; }
    }
}
