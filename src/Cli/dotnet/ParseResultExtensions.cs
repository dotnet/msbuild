// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using static Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Cli
{
    public static class ParseResultExtensions
    {
        public static void ShowHelp(this ParseResult parseResult)
        {
            Parser.Instance.Parse(parseResult.Tokens.Select(t => t.Value).Append("-h").ToArray()).Invoke();
        }

        public static void ShowHelpOrErrorIfAppropriate(this ParseResult parseResult)
        {
            if (parseResult.Errors.Any())
            {
                var unrecognizedTokenErrors = parseResult.Errors.Where(error =>
                    error.Message.Contains(Parser.Instance.Configuration.LocalizationResources.UnrecognizedCommandOrArgument(string.Empty).Replace("'", string.Empty)));
                if (parseResult.CommandResult.Command.TreatUnmatchedTokensAsErrors ||
                    parseResult.Errors.Except(unrecognizedTokenErrors).Any())
                {
                    throw new CommandParsingException(
                        message: string.Join(Environment.NewLine,
                                             parseResult.Errors.Select(e => e.Message)), 
                        parseResult: parseResult);
                }
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
            return string.IsNullOrEmpty(parseResult.RootSubCommandResult()) || 
                Parser.GetBuiltInCommand(parseResult.RootSubCommandResult()) != null;
        }

        public static bool IsTopLevelDotnetCommand(this ParseResult parseResult)
        {
            return parseResult.CommandResult.Command.Equals(RootCommand) && string.IsNullOrEmpty(parseResult.RootSubCommandResult());
        }

        public static bool CanBeInvoked(this ParseResult parseResult)
        {
            return Parser.GetBuiltInCommand(parseResult.RootSubCommandResult()) != null ||
                parseResult.Directives.Count() > 0 ||
                (parseResult.IsTopLevelDotnetCommand() && string.IsNullOrEmpty(parseResult.GetValueForArgument(Parser.DotnetSubCommand)));
        }

        public static int HandleMissingCommand(this ParseResult parseResult)
        {
            Reporter.Error.WriteLine(Tools.CommonLocalizableStrings.RequiredCommandNotPassed.Red());
            parseResult.ShowHelp();
            return 1;
        }

        public static string[] GetArguments(this ParseResult parseResult)
        {
            return parseResult.Tokens.Select(t => t.Value)
                .ToArray()
                .GetSubArguments();
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
            (parseResult.HasOption(CommonOptions.ArchitectureOption) ||
            parseResult.HasOption(CommonOptions.LongFormArchitectureOption)) && 
            parseResult.HasOption(CommonOptions.OperatingSystemOption);

        internal static string GetCommandLineRuntimeIdentifier(this ParseResult parseResult)
        {
            return parseResult.HasOption(RunCommandParser.RuntimeOption) ?
                parseResult.GetValueForOption(RunCommandParser.RuntimeOption) :
                parseResult.HasOption(CommonOptions.OperatingSystemOption) ||
                parseResult.HasOption(CommonOptions.ArchitectureOption) ||
                parseResult.HasOption(CommonOptions.LongFormArchitectureOption) ?
                CommonOptions.ResolveRidShorthandOptionsToRuntimeIdentifier(
                    parseResult.GetValueForOption(CommonOptions.OperatingSystemOption),
                    CommonOptions.ArchOptionValue(parseResult)) :
                null;
        }

        public static bool UsingRunCommandShorthandProjectOption(this ParseResult parseResult)
        {
            if (parseResult.HasOption(RunCommandParser.PropertyOption) && parseResult.GetValueForOption(RunCommandParser.PropertyOption).Any())
            {
                var projVals = parseResult.GetRunCommandShorthandProjectValues();
                if (projVals.Any())
                {
                    if (projVals.Count() != 1 || parseResult.HasOption(RunCommandParser.ProjectOption))
                    {
                        throw new GracefulException(Tools.Run.LocalizableStrings.OnlyOneProjectAllowed);
                    }
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<string> GetRunCommandShorthandProjectValues(this ParseResult parseResult)
        {
            var properties = GetRunPropertyOptions(parseResult, true);
            return properties.Where(property => !property.Contains("="));
        }

        public static IEnumerable<string> GetRunCommandPropertyValues(this ParseResult parseResult)
        {
            var shorthandProperties = GetRunPropertyOptions(parseResult, true)
                .Where(property => property.Contains("="));
            var longhandProperties = GetRunPropertyOptions(parseResult, false);
            return longhandProperties.Concat(shorthandProperties);
        }

        private static IEnumerable<string> GetRunPropertyOptions(ParseResult parseResult, bool shorthand)
        {
            var optionString = shorthand ? "-p" : "--property";
            var options = parseResult.CommandResult.Children.Where(c => c.Token().Type.Equals(TokenType.Option));
            var propertyOptions = options.Where(o => o.Token().Value.Equals(optionString));
            var propertyValues = propertyOptions.SelectMany(o => o.Children.SelectMany(c => c.Tokens.Select(t=> t.Value))).ToArray();
            return propertyValues;
        }

        [Conditional("DEBUG")]
        public static void HandleDebugSwitch(this ParseResult parseResult)
        {
            if (parseResult.HasOption(CommonOptions.DebugOption))
            {
                DebugHelper.WaitForDebugger();
            }
        }
    }
}
