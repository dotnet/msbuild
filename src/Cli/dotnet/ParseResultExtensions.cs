// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;
using static Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Cli
{
    public static class ParseResultExtensions
    {
        public static void ShowHelp(this ParseResult parseResult)
        {
            DotnetHelpBuilder.Instance.Value.Write(parseResult.CommandResult.Command);
        }

        public static void ShowHelpOrErrorIfAppropriate(this ParseResult parseResult)
        {
            if (parseResult.Errors.Any())
            {
                var unrecognizedTokenErrors = parseResult.Errors.Where(error =>
                    error.Message.Contains(Parser.Instance.Configuration.Resources.UnrecognizedCommandOrArgument(string.Empty).Replace("'", string.Empty)));
                if (parseResult.CommandResult.Command.TreatUnmatchedTokensAsErrors ||
                    parseResult.Errors.Except(unrecognizedTokenErrors).Any())
                {
                    throw new CommandParsingException(
                        message: string.Join(Environment.NewLine,
                                             parseResult.Errors.Select(e => e.Message)), 
                        parseResult: parseResult);
                }
            }
            else if (parseResult.HasOption("--help"))
            {
                parseResult.ShowHelp();
                throw new HelpException(string.Empty);
            }
        }

        public static string RootSubCommandResult(this ParseResult parseResult)
        {
            return parseResult.RootCommandResult.Children?
                .Select(child => GetSymbolResultValue(parseResult, child))
                .FirstOrDefault(subcommand => !string.IsNullOrEmpty(subcommand)) ?? string.Empty;
        }

        public static bool IsDotnetBuiltInCommand(this ParseResult parseResult)
        {
            return string.IsNullOrEmpty(parseResult.RootSubCommandResult()) || BuiltInCommandsCatalog.Commands.ContainsKey(parseResult.RootSubCommandResult());
        }

        public static bool IsTopLevelDotnetCommand(this ParseResult parseResult)
        {
            return parseResult.CommandResult.Command.Equals(RootCommand) && string.IsNullOrEmpty(parseResult.RootSubCommandResult());
        }

        public static string[] GetSubArguments(this string[] args)
        {
            var subargs = args.ToList();

            // Don't remove any arguments that are being passed to the app in dotnet run
            var runArgs = subargs.Contains("--") ? subargs.GetRange(subargs.IndexOf("--"), subargs.Count() - subargs.IndexOf("--")) : new List<string>();
            subargs = subargs.Contains("--") ? subargs.GetRange(0, subargs.IndexOf("--")) : subargs;

            subargs.RemoveAll(arg => DiagOption.Aliases.Contains(arg));
            if (subargs[0].Equals("dotnet"))
            {
                subargs.RemoveAt(0);
            }
            subargs.RemoveAt(0); // remove top level command (ex build or publish)
            return subargs.Concat(runArgs).ToArray();
        }

        private static string GetSymbolResultValue(ParseResult parseResult, SymbolResult symbolResult)
        {
            if (symbolResult.Token() == null)
            {
                return parseResult.FindResultFor(Parser.DotnetSubCommand)?.GetValueOrDefault<string>();
            }
            else if (symbolResult.Token().Type.Equals(TokenType.Command))
            {
                return symbolResult.Symbol.Name;
            }
            else if (symbolResult.Token().Type.Equals(TokenType.Argument))
            {
                return symbolResult.Token().Value;
            }
            else
            {
                return string.Empty;
            }
        }

        public static bool BothArchAndOsOptionsSpecified(this ParseResult parseResult) =>
            parseResult.HasOption(CommonOptions.ArchitectureOption().Aliases.First()) && 
            parseResult.HasOption(CommonOptions.OperatingSystemOption().Aliases.First());

        internal static string GetCommandLineRuntimeIdentifier(this ParseResult parseResult)
        {
            return parseResult.HasOption(RunCommandParser.RuntimeOption) ?
                parseResult.ValueForOption<string>(RunCommandParser.RuntimeOption) :
                parseResult.HasOption(CommonOptions.OperatingSystemOption().Aliases.First()) || parseResult.HasOption(CommonOptions.ArchitectureOption().Aliases.First()) ?
                CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(
                    parseResult.ValueForOption<string>(CommonOptions.OperatingSystemOption().Aliases.First()),
                    parseResult.ValueForOption<string>(CommonOptions.ArchitectureOption().Aliases.First())) :
                null;
        }
    }
}
