using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public static class ArgumentForwardingExtensions
    {
        public static ArgumentsRule Forward(
            this ArgumentsRule rule) =>
            rule.MaterializeAs(o => new ForwardedArgument(o.Arguments.SingleOrDefault()));

        public static ArgumentsRule ForwardAs(
            this ArgumentsRule rule,
            string value) =>
            rule.MaterializeAs(o => new ForwardedArgument(value));

        public static ArgumentsRule ForwardAsSingle(
            this ArgumentsRule rule,
            Func<AppliedOption, string> format) =>
            rule.MaterializeAs(o =>
                                   new ForwardedArgument(format(o)));

        public static ArgumentsRule ForwardAsMany(
            this ArgumentsRule rule,
            Func<AppliedOption, IEnumerable<string>> format) =>
            rule.MaterializeAs(o =>
                                   new ForwardedArgument(format(o).ToArray()));

        public static IEnumerable<string> OptionValuesToBeForwarded(
            this AppliedOption command) =>
            command.AppliedOptions
                   .Select(o => o.Value())
                   .OfType<ForwardedArgument>()
                   .SelectMany(o => o.Values);

        public static IEnumerable<string> ForwardedOptionValues(this AppliedOption command, string alias) =>
            (command.ValueOrDefault<ForwardedArgument>(alias)?.Values ?? Array.Empty<string>());

        private class ForwardedArgument
        {
            public ForwardedArgument(params string[] values)
            {
                Values = values;
            }

            public string[] Values { get; }
        }
    }
}