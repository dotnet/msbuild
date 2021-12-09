// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Help;
using System.CommandLine.Parsing;

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
                InstantiateCommand instantiateCommand = InstantiateCommand.FromNewCommand(newCommand);

                //tokens do not contain help option
                ParseResult reparseResult = ParserFactory.CreateParser(instantiateCommand).Parse(args.Tokens);
                instantiateCommand.WriteHelp(context, reparseResult);
            };
        }
    }
}
