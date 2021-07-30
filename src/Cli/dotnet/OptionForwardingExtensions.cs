// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;

namespace Microsoft.DotNet.Cli
{
    public static class OptionForwardingExtensions
    {
        public static ForwardedOption<T> Forward<T>(this ForwardedOption<T> option) => option.SetForwardingFunction((T o) => new string[] { option.Name });

        public static ForwardedOption<T> ForwardAs<T>(this ForwardedOption<T> option, string value) => option.SetForwardingFunction((T o) => new string[] { value });

        public static ForwardedOption<T> ForwardAsSingle<T>(this ForwardedOption<T> option, Func<T, string> format) => option.SetForwardingFunction(format);

        public static ForwardedOption<string[]> ForwardAsProperty(this ForwardedOption<string[]> option) => option
            .SetForwardingFunction((optionVals) => optionVals.SelectMany(optionVal => new string[] { $"{option.Aliases.FirstOrDefault()}:{optionVal.Replace("roperty:", string.Empty)}" }));

        public static Option<T> ForwardAsMany<T>(this ForwardedOption<T> option, Func<T, IEnumerable<string>> format) => option.SetForwardingFunction(format);

        public static Option<IEnumerable<string>> ForwardAsManyArgumentsEachPrefixedByOption(this ForwardedOption<IEnumerable<string>> option, string alias) => option.ForwardAsMany(o => ForwardedArguments(alias, o));

        public static IEnumerable<string> OptionValuesToBeForwarded(this ParseResult parseResult, Command command) =>
            command.Options
                .OfType<IForwardedOption>()
                .SelectMany(o => o.GetForwardingFunction()(parseResult)) ?? Array.Empty<string>();


        public static IEnumerable<string> ForwardedOptionValues<T>(this ParseResult parseResult, Command command, string alias) =>
            command.Options?
                .Where(o => o.Aliases.Contains(alias))?
                .OfType<IForwardedOption>()?
                .FirstOrDefault()?
                .GetForwardingFunction()(parseResult)
            ?? Array.Empty<string>();

        public static Option<T> AllowSingleArgPerToken<T>(this Option<T> option)
        {
            option.AllowMultipleArgumentsPerToken = false;
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

    public class ForwardedOption<T> : Option<T>, IForwardedOption
    {
        private Func<ParseResult, IEnumerable<string>> ForwardingFunction;

        public ForwardedOption(string[] aliases, string description) : base(aliases, description) { }

        public ForwardedOption(string[] aliases) : base(aliases) { }

        public ForwardedOption(string alias, string description = null) : base(alias, description) { }

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
            ForwardingFunction = (ParseResult parseResult) => parseResult.HasOption(Aliases.First()) ? func(parseResult.ValueForOption<T>(Aliases.First()), parseResult) : Array.Empty<string>();
            return this;
        }

        public Func<ParseResult, IEnumerable<string>> GetForwardingFunction(Func<T, IEnumerable<string>> func)
        {
            return (ParseResult parseResult) => parseResult.HasOption(Aliases.First()) ? func(parseResult.ValueForOption<T>(Aliases.First())) : Array.Empty<string>();
        }

        public Func<ParseResult, IEnumerable<string>> GetForwardingFunction()
        {
            return ForwardingFunction;
        }
    }
}
