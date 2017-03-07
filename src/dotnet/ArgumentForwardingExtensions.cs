using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public static class ArgumentForwardingExtensions
    {
        public static ArgumentsRule ForwardAs(
            this ArgumentsRule rule,
            string template) =>
            rule.MaterializeAs(o =>
                new ForwardedArgument(string.Format(template, o.Arguments.Single())));

        public static ArgumentsRule ForwardAs(
            this ArgumentsRule rule,
            Func<AppliedOption, string> format) =>
            rule.MaterializeAs(o =>
                new ForwardedArgument(format(o)));

        public static IEnumerable<string> ArgsToBeForwarded(
            this AppliedOption command) =>
            command.AppliedOptions
                   .Select(o => o.Value())
                   .OfType<ForwardedArgument>()
                   .Select(o => o.ToString());

        private class ForwardedArgument
        {
            private readonly string _value;

            public ForwardedArgument(string value)
            {
                _value = value;
            }

            public override string ToString() => _value;
        }
    }
}