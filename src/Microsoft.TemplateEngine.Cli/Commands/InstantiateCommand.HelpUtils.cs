// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Help;
using System.Text;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class InstantiateCommand
    {
        // This code is from System.CommandLine, HelpBuilder class.
        // Ideally those methods are exposed, we may switch to use them.
        private static string FormatArgumentUsage(IReadOnlyList<IArgument> arguments)
        {
            var sb = new StringBuilder();
            var end = default(Stack<char>);

            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument.IsHidden)
                {
                    continue;
                }

                var arityIndicator =
                    argument.Arity.MaximumNumberOfValues > 1
                        ? "..."
                        : "";

                var isOptional = IsOptional(argument);

                if (isOptional)
                {
                    sb.Append($"[<{argument.Name}>{arityIndicator}");
                    (end ??= new Stack<char>()).Push(']');
                }
                else
                {
                    sb.Append($"<{argument.Name}>{arityIndicator}");
                }

                sb.Append(' ');
            }

            if (sb.Length > 0)
            {
                sb.Length--;

                if (end is { })
                {
                    while (end.Count > 0)
                    {
                        sb.Append(end.Pop());
                    }
                }
            }

            return sb.ToString();
            bool IsMultiParented(IArgument argument) =>
                argument is Argument a &&
                a.Parents.Count > 1;

            bool IsOptional(IArgument argument) =>
                IsMultiParented(argument) ||
                argument.Arity.MinimumNumberOfValues == 0;
        }

        private static IEnumerable<string> GetUsageParts(
            HelpContext context,
            ICommand command,
            bool showSubcommands = true,
            bool showParentArguments = true,
            bool showArguments = true,
            bool showOptions = true,
            bool showAdditionalOptions = true)
        {
            List<ICommand> parentCommands = new List<ICommand>();
            ICommand? nextCommand = command;
            while (nextCommand is not null)
            {
                parentCommands.Add(nextCommand);
                nextCommand = nextCommand.Parents.FirstOrDefault(c => c is ICommand) as ICommand;
            }
            parentCommands.Reverse();

            foreach (ICommand parentCommand in parentCommands)
            {
                yield return parentCommand.Name;
                if (showParentArguments)
                {
                    yield return FormatArgumentUsage(parentCommand.Arguments);
                }
            }
            if (!showParentArguments && showArguments)
            {
                yield return FormatArgumentUsage(command.Arguments);
            }

            if (showSubcommands)
            {
                var hasCommandWithHelp = command.Children
                    .OfType<ICommand>()
                    .Any(x => !x.IsHidden);

                if (hasCommandWithHelp)
                {
                    yield return context.HelpBuilder.LocalizationResources.HelpUsageCommand();
                }
            }

            if (showOptions)
            {
                var displayOptionTitle = command.Options.Any(x => !x.IsHidden);
                if (displayOptionTitle)
                {
                    yield return context.HelpBuilder.LocalizationResources.HelpUsageOptions();
                }
            }

            if (showAdditionalOptions)
            {
                if (!command.TreatUnmatchedTokensAsErrors)
                {
                    yield return context.HelpBuilder.LocalizationResources.HelpUsageAdditionalArguments();
                }
            }
        }
    }
}
