// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Help;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class NewCommand
    {
        public IEnumerable<Action<HelpContext>> CustomHelpLayout()
        {
            yield return (context) =>
            {
                if (context.ParseResult.CommandResult.Command is not NewCommand newCommand)
                {
                    throw new ArgumentException($"{nameof(context)} should be for {nameof(NewCommand)}");
                }
                NewCommandArgs args = new(newCommand, context.ParseResult);
                using IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(args, context.ParseResult);
                InstantiateCommandArgs instantiateCommandArgs = InstantiateCommandArgs.FromNewCommandArgs(args);
                InstantiateCommand.WriteHelp(context, instantiateCommandArgs, environmentSettings);
            };
        }
    }
}
