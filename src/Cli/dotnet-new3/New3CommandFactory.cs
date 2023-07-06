// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Dotnet_new3
{
    internal static class New3CommandFactory
    {
        private const string CommandName = "new3";

        private static readonly CliOption<bool> _debugEmitTelemetryOption = new("--debug:emit-telemetry", "Enable telemetry")
        {
            Hidden = true,
            Recursive = true,
        };

        private static readonly CliOption<bool> _debugDisableBuiltInTemplatesOption = new("--debug:disable-sdk-templates", "Disable built-in templates")
        {
            Hidden = true,
            Recursive = true
        };

        internal static CliCommand Create()
        {
            CliCommand newCommand = NewCommandFactory.Create(
                CommandName,
                (ParseResult parseResult) =>
                {
                    FileInfo? outputPath = parseResult.GetValue(SharedOptions.OutputOption);
                    return HostFactory.CreateHost(parseResult.GetValue(_debugDisableBuiltInTemplatesOption), outputPath?.FullName);
                });

            newCommand.Options.Add(_debugEmitTelemetryOption);
            newCommand.Options.Add(_debugDisableBuiltInTemplatesOption);
            newCommand.Subcommands.Add(new CompleteCommand());
            return newCommand;
        }
    }
}
