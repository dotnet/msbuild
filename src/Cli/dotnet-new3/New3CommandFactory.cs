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
                (ParseResult parseResult) => new TelemetryLogger(null, parseResult.GetValueForOption(_debugEmitTelemetryOption)));

            newCommand.AddGlobalOption(_debugEmitTelemetryOption);
            newCommand.AddGlobalOption(_debugDisableBuiltInTemplatesOption);
            newCommand.AddCommand(new CompleteCommand());
            return newCommand;
        }

        private static bool ExecuteDotnetCommand(Dotnet command)
        {
            command.CaptureStdOut();
            command.CaptureStdErr();
            Console.WriteLine($"Running '{command.Command}':");
            var result = command.Execute();
            Console.WriteLine(result.ExitCode == 0 ? "Command succeeded." : "Command failed.");
            WriteCommandOutput(result);
            return result.ExitCode == 0;
        }

        /// <summary>
        /// Writes formatted command output from <paramref name="process"/>.
        /// </summary>
        private static void WriteCommandOutput(Dotnet.Result process)
        {
            Console.WriteLine("Output from command:");
            Console.WriteLine("StdOut:");
            Console.WriteLine(string.IsNullOrWhiteSpace(process.StdOut) ? "(empty)" : process.StdOut.Trim('\n', '\r'));
            Console.WriteLine("StdErr:");
            Console.WriteLine(string.IsNullOrWhiteSpace(process.StdErr) ? "(empty)" : process.StdErr.Trim('\n', '\r'));
        }
    }
}
