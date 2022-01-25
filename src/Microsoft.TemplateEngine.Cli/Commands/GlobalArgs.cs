// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

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
            RootCommand = parseResult.GetNewCommandFromParseResult();
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

        protected static (bool, IReadOnlyList<string>?) ParseTabularOutputSettings(ITabularOutputCommand command, ParseResult parseResult)
        {
            return (parseResult.GetValueForOption(command.ColumnsAllOption), parseResult.GetValueForOption(command.ColumnsOption));
        }
    }
}
