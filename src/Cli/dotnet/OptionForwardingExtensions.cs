// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    public static class OptionForwardingExtensions
    {
        public static ForwardedOption<T> Forward<T>(this ForwardedOption<T> option) => option.SetForwardingFunction((T o) => new string[] { option.Name });

        public static ForwardedOption<T> ForwardAs<T>(this ForwardedOption<T> option, string value) => option.SetForwardingFunction((T o) => new string[] { value });

        public static ForwardedOption<T> ForwardAsSingle<T>(this ForwardedOption<T> option, Func<T, string> format) => option.SetForwardingFunction(format);

        /// <summary>
        /// Set up an option to be forwaded as an output path to MSBuild
        /// </summary>
        /// <param name="option">The command line option</param>
        /// <param name="outputPropertyName">The property name for the output path (such as OutputPath or PublishDir)</param>
        /// <param name="surroundWithDoubleQuotes">Whether the path should be surrounded with double quotes.  This may not be necessary but preserves the provious behavior of "dotnet test"</param>
        /// <returns>The option</returns>
        public static ForwardedOption<string> ForwardAsOutputPath(this ForwardedOption<string> option, string outputPropertyName, bool surroundWithDoubleQuotes = false)
        {
            return option.SetForwardingFunction((string o) =>
            {
                string argVal = CommandDirectoryContext.GetFullPath(o);
                if (surroundWithDoubleQuotes)
                {
                    //  Not sure if this is necessary, but this is what "dotnet test" previously did and so we are
                    //  preserving the behavior here after refactoring
                    argVal = TestCommandParser.SurroundWithDoubleQuotes(argVal);
                }
                return new string[]
                {
                    $"-property:{outputPropertyName}={argVal}",
                    "-property:_CommandLineDefinedOutputPath=true"
                };
            });
        }

        public static ForwardedOption<string[]> ForwardAsProperty(this ForwardedOption<string[]> option) => option
            .SetForwardingFunction((optionVals) =>
                optionVals
                    .SelectMany(Utils.MSBuildPropertyParser.ParseProperties)
                    .Select(keyValue => $"{option.Name}:{keyValue.key}={keyValue.value}")
                );

        public static CliOption<T> ForwardAsMany<T>(this ForwardedOption<T> option, Func<T, IEnumerable<string>> format) => option.SetForwardingFunction(format);

        public static CliOption<IEnumerable<string>> ForwardAsManyArgumentsEachPrefixedByOption(this ForwardedOption<IEnumerable<string>> option, string alias) => option.ForwardAsMany(o => ForwardedArguments(alias, o));

        public static IEnumerable<string> OptionValuesToBeForwarded(this ParseResult parseResult, CliCommand command) =>
            command.Options
                .OfType<IForwardedOption>()
                .SelectMany(o => o.GetForwardingFunction()(parseResult)) ?? Array.Empty<string>();


        public static IEnumerable<string> ForwardedOptionValues<T>(this ParseResult parseResult, CliCommand command, string alias) =>
            command.Options?
                .Where(o => o.Name.Equals(alias) || o.Aliases.Contains(alias))?
                .OfType<IForwardedOption>()?
                .FirstOrDefault()?
                .GetForwardingFunction()(parseResult)
            ?? Array.Empty<string>();

        public static CliOption<T> AllowSingleArgPerToken<T>(this CliOption<T> option)
        {
            option.AllowMultipleArgumentsPerToken = false;
            return option;
        }

        public static CliOption<T> Hide<T>(this CliOption<T> option)
        {
            option.Hidden = true;
            return option;
        }

        public static CliOption<T> WithHelpDescription<T>(this CliOption<T> option, CliCommand command, string helpText)
        {
            if (Parser.HelpDescriptionCustomizations.ContainsKey(option))
            {
                Parser.HelpDescriptionCustomizations[option].Add(command, helpText);
            }
            else
            {
                Parser.HelpDescriptionCustomizations.Add(option, new Dictionary<CliCommand, string>() { { command, helpText } });
            }

            return option;
        }

        private static IEnumerable<string> ForwardedArguments(string alias, IEnumerable<string> arguments)
        {
            foreach (string arg in arguments)
            {
                yield return alias;
                yield return arg;
            }
        }
    }

    public interface IForwardedOption
    {
        Func<ParseResult, IEnumerable<string>> GetForwardingFunction();
    }

    public class ForwardedOption<T> : CliOption<T>, IForwardedOption
    {
        private Func<ParseResult, IEnumerable<string>> ForwardingFunction;

        public ForwardedOption(string name, params string[] aliases) : base(name, aliases) { }

        public ForwardedOption(string name, Func<ArgumentResult, T> parseArgument, string description = null)
            : base(name)
        {
            CustomParser = parseArgument;
            Description = description;
        }

        public ForwardedOption<T> SetForwardingFunction(Func<T, IEnumerable<string>> func)
        {
            ForwardingFunction = GetForwardingFunction(func);
            return this;
        }

        public ForwardedOption<T> SetForwardingFunction(Func<T, string> format)
        {
            ForwardingFunction = GetForwardingFunction((o) => new string[] { format(o) });
            return this;
        }

        public ForwardedOption<T> SetForwardingFunction(Func<T, ParseResult, IEnumerable<string>> func)
        {
            ForwardingFunction = (ParseResult parseResult) => parseResult.GetResult(this) is not null ? func(parseResult.GetValue<T>(this), parseResult) : Array.Empty<string>();
            return this;
        }

        public Func<ParseResult, IEnumerable<string>> GetForwardingFunction(Func<T, IEnumerable<string>> func)
        {
            return (ParseResult parseResult) => parseResult.GetResult(this) is not null ? func(parseResult.GetValue<T>(this)) : Array.Empty<string>();
        }

        public Func<ParseResult, IEnumerable<string>> GetForwardingFunction()
        {
            return ForwardingFunction;
        }
    }
}
