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

        private static Option<string?> debugCustomSettingsLocationOption = new("--debug:custom-hive", "Sets custom settings location")
        {
            IsHidden = true
        };

        private static Option<bool> debugVirtualizeSettingsOption = new("--debug:ephemeral-hive", "Use virtual settings")
        {
            IsHidden = true
        };

        private static Option<bool> debugAttachOption = new("--debug:attach", "Allows to pause execution in order to attach to the process for debug purposes")
        {
            IsHidden = true
        };

        private static Option<bool> debugReinitOption = new("--debug:reinit", "Resets the settings")
        {
            IsHidden = true
        };

        private static Option<bool> debugRebuildCacheOption = new(new[] { "--debug:rebuild-cache", "--debug:rebuildcache" }, "Resets template cache")
        {
            IsHidden = true
        };

        private static Option<bool> debugShowConfigOption = new(new[] { "--debug:show-config", "--debug:showconfig" }, "Shows the template engine config")
        {
            IsHidden = true
        };

        public GlobalArgs(ParseResult parseResult)
        {
            Quiet = parseResult.ValueForOption(quietOption);
            DebugCustomSettingsLocation = parseResult.ValueForOption(debugCustomSettingsLocationOption);
            DebugVirtualizeSettings = parseResult.ValueForOption(debugVirtualizeSettingsOption);
            DebugAttach = parseResult.ValueForOption(debugAttachOption);
            DebugReinit = parseResult.ValueForOption(debugReinitOption);
            DebugRebuildCache = parseResult.ValueForOption(debugRebuildCacheOption);
            DebugShowConfig = parseResult.ValueForOption(debugShowConfigOption);
            //TODO: check if it gets the command name correctly.
            CommandName = parseResult.CommandResult.Command.Name;
            ParseResult = parseResult;
        }

        internal ParseResult ParseResult { get; }

        internal string CommandName { get; private set; }

        internal bool Quiet { get; private set; }

        internal bool DebugAttach { get; private set; }

        internal bool DebugRebuildCache { get; private set; }

        internal bool DebugVirtualizeSettings { get; private set; }

        internal bool DebugReinit { get; private set; }

        internal bool DebugShowConfig { get; private set; }

        internal string? DebugCustomSettingsLocation { get; private set; }

        internal static void AddGlobalsToCommand(Command command)
        {
            command.AddOption(quietOption);
            command.AddOption(debugCustomSettingsLocationOption);
            command.AddOption(debugVirtualizeSettingsOption);
            command.AddOption(debugAttachOption);
            command.AddOption(debugReinitOption);
            command.AddOption(debugRebuildCacheOption);
            command.AddOption(debugShowConfigOption);
        }
    }
}
