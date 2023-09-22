// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;

namespace Dotnet_new3
{
    /// <summary>
    /// Represents completion action to the command.
    /// </summary>
    /// <remark>
    /// this implementation is for test purpose only.
    /// Keep in sync with https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/commands/dotnet-complete/CompleteCommand.cs.
    /// </remark>
    internal class CompleteCommand : CliCommand
    {
        private static readonly CliArgument<string> PathArgument = new("path");

        private static readonly CliOption<int?> PositionOption = new("--position");

        internal CompleteCommand() : base("complete", "tab completion")
        {
            Arguments.Add(PathArgument);
            Options.Add(PositionOption);

            SetAction(Run);
            Hidden = true;
        }

        public Task<int> Run(ParseResult result, CancellationToken cancellationToken)
        {
            try
            {
                var input = result.GetValue(PathArgument) ?? string.Empty;
                var position = result.GetValue(PositionOption);

                if (position > input.Length)
                {
                    input += " ";
                }

                CliCommand newCommand = New3CommandFactory.Create();
                ParseResult newCommandResult = ParserFactory.CreateParser(newCommand).Parse(input);
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
