// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class Example
    {
        private List<string> _commandParts = new();
        private CliCommand _currentCommand;

        private Example(CliCommand currentCommand, params string[] commandParts)
        {
            _commandParts.AddRange(commandParts);
            _currentCommand = currentCommand;
        }

        public static implicit operator string(Example e) => e.ToString();

        public override string ToString()
        {
            return string.Join(" ", _commandParts);
        }

        internal static Example For<T>(ParseResult parseResult) where T : CliCommand
        {
            var commandResult = parseResult.CommandResult;

            //check for parent commands first
            while (commandResult?.Command != null && commandResult.Command is not T)
            {
                commandResult = (commandResult.Parent as CommandResult);
            }

            if (commandResult?.Command is T typedCommand)
            {
                List<string> parentCommands = new();
                while (commandResult?.Command != null)
                {
                    parentCommands.Add(commandResult.Command.Name);
                    commandResult = (commandResult.Parent as CommandResult);
                }
                parentCommands.Reverse();
                return new Example(typedCommand, parentCommands.ToArray());
            }

            // if the command is not found in parents of command result, try to search it in the whole command tree
            CliCommand siblingCommand = SearchForSiblingCommand<T>(parseResult.CommandResult.Command);
            List<string> parentCommands2 = new();
            CliCommand? nextCommand = siblingCommand;
            while (nextCommand != null)
            {
                parentCommands2.Add(nextCommand.Name);
                nextCommand = nextCommand.Parents.OfType<CliCommand>().FirstOrDefault();
            }
            parentCommands2.Reverse();
            return new Example(siblingCommand, parentCommands2.ToArray());
        }

        internal static Example FromExistingTokens(ParseResult parseResult)
        {
            // root command name is not part of the tokens
            var commandParts = parseResult.Tokens.Select(t => t.Value).Prepend(parseResult.RootCommandResult.Command.Name);
            return new Example(parseResult.CommandResult.Command, commandParts.ToArray());
        }

        internal Example WithOption(CliOption option, params string[] args)
        {
            if (!_currentCommand.Options.Contains(option) && !_currentCommand.Options.Any(o => o.Name == option.Name))
            {
                throw new ArgumentException($"Command {_currentCommand.Name} does not have option {option.Name}");
            }

            _commandParts.Add(option.Name);
            if (args.Any())
            {
                _commandParts.AddRange(args.Select(a => a.Any(char.IsWhiteSpace) ? $"'{a}'" : a));
                return this;
            }
            if (option.Arity.MinimumNumberOfValues == 0)
            {
                return this;
            }

            _commandParts.Add(CommandLineUtils.FormatArgumentUsage(option));
            return this;
        }

        internal Example WithArgument(CliArgument argument, params string[] args)
        {
            if (!_currentCommand.Arguments.Contains(argument))
            {
                throw new ArgumentException($"Command {_currentCommand.Name} does not have argument {argument.Name}");
            }

            if (args.Any())
            {
                _commandParts.AddRange(args.Select(a => a.Any(char.IsWhiteSpace) ? $"'{a}'" : a));
                return this;
            }
            _commandParts.Add(CommandLineUtils.FormatArgumentUsage(argument));
            return this;
        }

        internal Example WithSubcommand(CliCommand command)
        {
            if (!_currentCommand.Subcommands.Contains(command))
            {
                throw new ArgumentException($"Command {_currentCommand.Name} does not have subcommand {command.Name}");
            }

            _commandParts.Add(command.Name);
            _currentCommand = command;
            return this;
        }

        internal Example WithSubcommand(string token)
        {
            CliCommand? commandToUse = _currentCommand.Subcommands.FirstOrDefault(c => c.Name.Equals(token) || c.Aliases.Contains(token));

            if (commandToUse is null)
            {
                throw new ArgumentException($"Command {_currentCommand.Name} does not have subcommand '{token}'.");
            }

            _commandParts.Add(token);
            _currentCommand = commandToUse;
            return this;
        }

        internal Example WithSubcommand<T>() where T : CliCommand
        {
            if (!_currentCommand.Subcommands.Any(c => c is T))
            {
                throw new ArgumentException($"Command {_currentCommand.Name} does not have subcommand {typeof(T).Name}");
            }
            _currentCommand = _currentCommand.Subcommands.First(c => c is T);
            _commandParts.Add(_currentCommand.Name);

            return this;
        }

        internal Example WithHelpOption()
        {
            _commandParts.Add(Constants.KnownHelpAliases.First());
            return this;
        }

        private static T SearchForSiblingCommand<T>(CliCommand currentCommand) where T : CliCommand
        {
            CliCommand? next = currentCommand;
            CliCommand root = currentCommand;

            while (next != null)
            {
                root = next;
                next = next?.Parents.OfType<CliCommand>().FirstOrDefault();
            }

            Queue<CliCommand> probes = new();
            probes.Enqueue(root);
            while (probes.Count > 0)
            {
                CliCommand current = probes.Dequeue();
                if (current is T typedCommand)
                {
                    return typedCommand;
                }
                foreach (var child in current.Subcommands)
                {
                    probes.Enqueue(child);
                }
            }
            throw new Exception($"Command structure is not correct: {nameof(T)} is not found.");
        }
    }

}
