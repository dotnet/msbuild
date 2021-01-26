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
        public static Option Forward<T>(this Option<T> option) => new ForwardedOption<T>(option).SetForwardingFunction((o) => new string[] { option.Name });

        public static Option ForwardAs<T>(this Option<T> option, string value) => new ForwardedOption<T>(option).SetForwardingFunction((o) => new string[] { value });

        public static Option ForwardAsSingle<T>(this Option<T> option, Func<T, string> format) => new ForwardedOption<T>(option).SetForwardingFunction(format);

        public static Option ForwardAsProperty(this Option<string[]> option) => new ForwardedOption<string[]>(option)
            .SetForwardingFunction((optionVals) => optionVals.SelectMany(optionVal => new string[] { $"{option.Aliases.FirstOrDefault()}:{optionVal.Replace("roperty:", string.Empty)}" }));

        public static Option ForwardAsMany<T>(this Option<T> option, Func<T, IEnumerable<string>> format) => new ForwardedOption<T>(option).SetForwardingFunction(format);

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

        public static Option AllowSingleArgPerToken(this Option option)
        {
            option.AllowMultipleArgumentsPerToken = false;
            return option;
        }

        private interface IForwardedOption
        {
            Func<ParseResult, IEnumerable<string>> GetForwardingFunction();
        }

        private class ForwardedOption<T> : Option<T>, IForwardedOption
        {
            private Func<ParseResult, IEnumerable<string>> ForwardingFunction;

            public ForwardedOption(Option option) : base(option.Aliases.ToArray(), option.Description)
            {
                IsRequired = option.IsRequired;
                IsHidden = option.IsHidden;
                if (option.Argument != null && !string.IsNullOrEmpty(option.Argument.Name))
                {
                    Argument = option.Argument;
                }
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
}
