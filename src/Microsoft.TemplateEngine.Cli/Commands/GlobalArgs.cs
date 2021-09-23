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
            DebugCustomSettingsLocation = parseResult.ValueForOption(command.DebugCustomSettingsLocationOption);
            DebugVirtualizeSettings = parseResult.ValueForOption(command.DebugVirtualizeSettingsOption);
            DebugAttach = parseResult.ValueForOption(command.DebugAttachOption);
            DebugReinit = parseResult.ValueForOption(command.DebugReinitOption);
            DebugRebuildCache = parseResult.ValueForOption(command.DebugRebuildCacheOption);
            DebugShowConfig = parseResult.ValueForOption(command.DebugShowConfigOption);
            //TODO: check if it gets the command name correctly.
            CommandName = parseResult.CommandResult.Command.Name;
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
    }
}
