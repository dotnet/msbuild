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
            string template) =>
            rule.MaterializeAs(o => new ForwardedArgument(template));

        public static ArgumentsRule ForwardAs(
            this ArgumentsRule rule,
            Func<AppliedOption, string> format) =>
            rule.MaterializeAs(o =>
                new ForwardedArgument(format(o)));

        public static IEnumerable<string> OptionValuesToBeForwarded(
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
            
            public static explicit operator string(ForwardedArgument argument)
            {
                return argument.ToString();
            }
        }
    }
}
