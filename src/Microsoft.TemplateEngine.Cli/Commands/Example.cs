// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class Example
    {
        private List<string> _commandParts = new List<string>();
        private Command _currentCommand;

        private Example(Command currentCommand, params string[] commandParts)
        {
            _commandParts.AddRange(commandParts);
            _currentCommand = currentCommand;
        }

        public static implicit operator string(Example e) => e.ToString();

        public override string ToString()
        {
            return string.Join(" ", _commandParts);
        }

        internal static Example For<T>(ParseResult parseResult) where T : Command
        {
            var commandResult = parseResult.CommandResult;

            while (commandResult?.Command != null && commandResult.Command is not T)
            {
                commandResult = (commandResult.Parent as CommandResult);
            }
            if (commandResult == null || commandResult.Command is not T typedCommand)
            {
                throw new Exception($"Command structure is not correct: {nameof(T)} is not found.");
            }
            List<string> parentCommands = new List<string>();
            while (commandResult?.Command != null)
            {
                parentCommands.Add(commandResult.Command.Name);
                commandResult = (commandResult.Parent as CommandResult);
            }
            parentCommands.Reverse();
            return new Example(typedCommand, parentCommands.ToArray());
        }

        internal Example WithOption(Option option, params string[] args)
        {
            if (!_currentCommand.Options.Contains(option) && !_currentCommand.Options.Any(o => o.Name == option.Name))
            {
                throw new ArgumentException($"Command {_currentCommand.Name} does not have option {option.Name}");
            }

            _commandParts.Add(option.Aliases.First());
            if (args.Any())
            {
                _commandParts.AddRange(args.Select(a => a.Any(char.IsWhiteSpace) ? $"'{a}'" : a));
                return this;
            }

            _commandParts.Add(CommandLineUtils.FormatArgumentUsage(option));
            return this;
        }

        internal Example WithArgument(Argument argument, params string[] args)
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

        internal Example WithSubcommand(Command command)
        {
            if (!_currentCommand.Children.OfType<Command>().Contains(command))
            {
                throw new ArgumentException($"Command {_currentCommand.Name} does not have subcommand {command.Name}");
            }

            _commandParts.Add(command.Aliases.First());
            _currentCommand = command;
            return this;
        }

        internal Example WithSubcommand<T>() where T : Command
        {
            if (!_currentCommand.Children.OfType<Command>().Any( c => c is T))
            {
                throw new ArgumentException($"Command {_currentCommand.Name} does not have subcommand {typeof(T).Name}");
            }
            _currentCommand = _currentCommand.Children.OfType<Command>().First(c => c is T);
            _commandParts.Add(_currentCommand.Aliases.First());

            return this;
        }

        internal Example WithHelpOption()
        {
            _commandParts.Add(Constants.KnownHelpAliases.First());
            return this;
        }
    }

}
