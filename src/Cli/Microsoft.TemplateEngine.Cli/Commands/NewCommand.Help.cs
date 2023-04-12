// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine.Help;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class NewCommand
    {
        public IEnumerable<HelpSectionDelegate> CustomHelpLayout()
        {
            yield return (context) =>
            {
                if (context.ParseResult.CommandResult.Command is not NewCommand newCommand)
                {
                    throw new ArgumentException($"{nameof(context)} should be for {nameof(NewCommand)}");
                }
                NewCommandArgs args = new NewCommandArgs(newCommand, context.ParseResult);
                using IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(args, context.ParseResult);
                InstantiateCommandArgs instantiateCommandArgs = InstantiateCommandArgs.FromNewCommandArgs(args);
                InstantiateCommand.WriteHelp(context, instantiateCommandArgs, environmentSettings);
            };
        }
    }
}
