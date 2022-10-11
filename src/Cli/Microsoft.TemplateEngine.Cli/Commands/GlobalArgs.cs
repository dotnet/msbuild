// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class GlobalArgs : ICommandArgs
    {
        public GlobalArgs(BaseCommand command, ParseResult parseResult)
        {
            DebugCustomSettingsLocation = parseResult.GetValueForOption(NewCommand.DebugCustomSettingsLocationOption);
            DebugVirtualizeSettings = parseResult.GetValueForOption(NewCommand.DebugVirtualizeSettingsOption);
            DebugAttach = parseResult.GetValueForOption(NewCommand.DebugAttachOption);
            DebugReinit = parseResult.GetValueForOption(NewCommand.DebugReinitOption);
            DebugRebuildCache = parseResult.GetValueForOption(NewCommand.DebugRebuildCacheOption);
            DebugShowConfig = parseResult.GetValueForOption(NewCommand.DebugShowConfigOption);
            ParseResult = parseResult;
            Command = command;
            RootCommand = GetNewCommandFromParseResult(parseResult);
            HasHelpOption = parseResult.CommandResult
                .Children
                .OfType<OptionResult>()
                .Select(r => r.Option)
                .Any(o => o.HasAlias(Constants.KnownHelpAliases[0]));
        }

        protected GlobalArgs(GlobalArgs args) : this(args.Command, args.ParseResult) { }

        public NewCommand RootCommand { get; }

        public BaseCommand Command { get; }

        public ParseResult ParseResult { get; }

        Command ICommandArgs.Command => Command;

        internal bool DebugAttach { get; private set; }

        internal bool DebugRebuildCache { get; private set; }

        internal bool DebugVirtualizeSettings { get; private set; }

        internal bool DebugReinit { get; private set; }

        internal bool DebugShowConfig { get; private set; }

        internal string? DebugCustomSettingsLocation { get; private set; }

        internal bool HasHelpOption { get; private set; }

        protected static (bool, IReadOnlyList<string>?) ParseTabularOutputSettings(ITabularOutputCommand command, ParseResult parseResult)
        {
            return (parseResult.GetValueForOption(command.ColumnsAllOption), parseResult.GetValueForOption(command.ColumnsOption));
        }

        /// <summary>
        /// Gets root <see cref="NewCommand"/> from <paramref name="parseResult"/>.
        /// </summary>
        private static NewCommand GetNewCommandFromParseResult(ParseResult parseResult)
        {
            var commandResult = parseResult.CommandResult;

            while (commandResult?.Command != null && commandResult.Command is not NewCommand)
            {
                commandResult = (commandResult.Parent as CommandResult);
            }
            if (commandResult == null || commandResult.Command is not NewCommand newCommand)
            {
                throw new Exception($"Command structure is not correct: {nameof(NewCommand)} is not found as part of parse result.");
            }
            return newCommand;
        }
    }
}
