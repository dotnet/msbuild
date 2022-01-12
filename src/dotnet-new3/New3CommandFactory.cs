// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli;

namespace Dotnet_new3
{
    internal static class New3CommandFactory
    {
        private const string CommandName = "new3";

        private static readonly Option<bool> _debugEmitTelemetryOption = new("--debug:emit-telemetry", "Enable telemetry")
        {
            IsHidden = true
        };

        private static readonly Option<bool> _debugDisableBuiltInTemplatesOption = new("--debug:disable-sdk-templates", "Disable built-in templates")
        {
            IsHidden = true
        };

        internal static Command Create()
        {
            Command newCommand = NewCommandFactory.Create(
                CommandName,
                (ParseResult parseResult) => HostFactory.CreateHost(parseResult.GetValueForOption(_debugDisableBuiltInTemplatesOption)),
                (ParseResult parseResult) => new TelemetryLogger(null, parseResult.GetValueForOption(_debugEmitTelemetryOption)),
                new NewCommandCallbacks());

            newCommand.AddGlobalOption(_debugEmitTelemetryOption);
            newCommand.AddGlobalOption(_debugDisableBuiltInTemplatesOption);
            newCommand.AddCommand(new CompleteCommand());
            return newCommand;
        }
    }
}
