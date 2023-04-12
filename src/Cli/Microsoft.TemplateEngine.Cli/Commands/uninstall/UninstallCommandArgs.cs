// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;

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
