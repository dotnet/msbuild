// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;

namespace Dotnet_new3
{
    /// <remark>
    /// this implementation is for test purpose only.
    /// Keep in sync with https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/commands/dotnet-complete/CompleteCommand.cs.
    /// </remark>
    internal class CompleteCommand : Command
    {
        private static readonly Argument<string> PathArgument = new Argument<string>("path");

        private static readonly Option<int?> PositionOption = new Option<int?>("--position");

        internal CompleteCommand() : base("complete", "tab completion")
        {
            this.AddArgument(PathArgument);
            this.AddOption(PositionOption);

            this.SetHandler((ParseResult parseResult) => Run(parseResult));
            this.IsHidden = true;
        }

        public Task<int> Run(ParseResult result)
        {
            try
            {
                var input = result.GetValueForArgument(PathArgument) ?? string.Empty;
                var position = result.GetValueForOption(PositionOption);

                if (position > input.Length)
                {
                    input += " ";
                }

                Command new3Command = new New3Command();

                //help is disabled for pre-parsing and should be handled by NewCommand instead.
                ParseResult preParseResult = ParserFactory
                    .CreateParser(new3Command, disableHelp: true)
                    .Parse(input);

                DefaultTemplateEngineHost host = HostFactory.CreateHost(preParseResult.GetValueForOption(New3Command.DebugDisableBuiltInTemplatesOption));
                ITelemetryLogger telemetryLogger = new TelemetryLogger(null, preParseResult.GetValueForOption(New3Command.DebugEmitTelemetryOption));
                string[] remainingArgs = preParseResult.GetValueForArgument(New3Command.RemainingTokensArgument) ?? Array.Empty<string>();

                Command newCommand = NewCommandFactory.Create(new3Command.Name, host, telemetryLogger, new NewCommandCallbacks());
                ParseResult newCommandResult = ParserFactory.CreateParser(newCommand).Parse(remainingArgs, rawInput: input);
                foreach (CompletionItem suggestion in newCommandResult.GetCompletions(position).Distinct())
                {
                    Console.WriteLine(suggestion.Label);
                }
            }
            catch (Exception)
            {
                return Task.FromResult(1);
            }
            return Task.FromResult(0);
        }
    }
}
