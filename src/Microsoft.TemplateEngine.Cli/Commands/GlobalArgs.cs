// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class GlobalArgs
    {
        private static Option<bool> quietOption = new("--quiet", "sshhhhhhhhhhhh");

        private static Option<string?> customHiveOption = new("--debug:custom-hive", "Sets custom settings location");

        public GlobalArgs(ParseResult parseResult)
        {
            Quiet = parseResult.ValueForOption(quietOption);
            DebugSettingsLocation = parseResult.ValueForOption(customHiveOption);
            //TODO: check if it gets the command name correctly.
            CommandName = parseResult.CommandResult.Command.Name;
        }

        internal string CommandName { get; private set; }

        internal bool Quiet { get; private set; }

        internal bool DebugAttach { get; private set; }

        internal bool DebugRebuildCache { get; private set; }

        internal bool DebugVirtualSettings { get; private set; }

        internal bool DebugReinit { get; private set; }

        internal bool DebugShowConfig { get; private set; }

        internal string? DebugSettingsLocation { get; private set; }

        internal static void AddGlobalsToCommand(Command command)
        {
            command.AddOption(quietOption);
            command.AddOption(customHiveOption);
        }
    }
}
