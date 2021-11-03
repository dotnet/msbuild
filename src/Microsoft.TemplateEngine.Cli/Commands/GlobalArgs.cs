// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class GlobalArgs
    {
        public GlobalArgs(BaseCommand command, ParseResult parseResult)
        {
            DebugCustomSettingsLocation = parseResult.GetValueForOption(command.DebugCustomSettingsLocationOption);
            DebugVirtualizeSettings = parseResult.GetValueForOption(command.DebugVirtualizeSettingsOption);
            DebugAttach = parseResult.GetValueForOption(command.DebugAttachOption);
            DebugReinit = parseResult.GetValueForOption(command.DebugReinitOption);
            DebugRebuildCache = parseResult.GetValueForOption(command.DebugRebuildCacheOption);
            DebugShowConfig = parseResult.GetValueForOption(command.DebugShowConfigOption);
            CommandName = parseResult.GetNewCommandName();
            ParseResult = parseResult;
        }

        internal ParseResult ParseResult { get; }

        internal string CommandName { get; private set; }

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
