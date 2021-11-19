// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;

namespace Dotnet_new3
{
    internal class New3Command : Command
    {
        private static readonly Option<bool> _debugEmitTelemetryOption = new("--debug:emit-telemetry", "Enable telemetry")
        {
            IsHidden = true
        };

        private static readonly Option<bool> _debugDisableBuiltInTemplatesOption = new("--debug:disable-sdk-templates", "Disable built-in templates")
        {
            IsHidden = true
        };

        private static readonly Argument<string[]> _remainingTokensArgument = new Argument<string[]>()
        {
            Arity = new ArgumentArity(0, 999)
        };

        internal New3Command() : base("new3", "Pre-parse command for dotnet new3")
        {
            this.AddOption(DebugEmitTelemetryOption);
            this.AddOption(DebugDisableBuiltInTemplatesOption);
            this.AddArgument(RemainingTokensArgument);
            this.AddCommand(new CompleteCommand());
            this.Handler = CommandHandler.Create<ParseResult>(Run);
        }

        internal static Option<bool> DebugEmitTelemetryOption => _debugEmitTelemetryOption;

        internal static Option<bool> DebugDisableBuiltInTemplatesOption => _debugDisableBuiltInTemplatesOption;

        internal static Argument<string[]> RemainingTokensArgument => _remainingTokensArgument;

        internal Task<int> Run(ParseResult result)
        {
            DefaultTemplateEngineHost host = HostFactory.CreateHost(result.GetValueForOption(DebugDisableBuiltInTemplatesOption));
            ITelemetryLogger telemetryLogger = new TelemetryLogger(null, result.GetValueForOption(DebugEmitTelemetryOption));
            string[] remainingArgs = result.GetValueForArgument(RemainingTokensArgument) ?? Array.Empty<string>();

            Command newCommand = NewCommandFactory.Create(Name, host, telemetryLogger, new NewCommandCallbacks());

            return ParserFactory.CreateParser(newCommand).Parse(remainingArgs).InvokeAsync();
        }
    }
}
