// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.TemplateEngine.Cli
{
    public static class SymbolExtensions
    {
        public static void EnsureHelpName(this CliOption cliOption)
        {
            // System.CommandLine used to include the option's name without the prefix in the help output:
            // --name, --alias, -a <name> description
            // To keep that behavior, we need to set HelpName in explicit way.
            if (cliOption.HelpName is null
                && cliOption is not CliOption<bool> // that was never a thing for boolean options
                && cliOption.CompletionSources.Count == 0) // and options that have completions
            {
                cliOption.HelpName = cliOption.Name.RemovePrefix();
            }
        }
    }
}
