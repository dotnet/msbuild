// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class CommandLineUtils
    {
        // This code is from System.CommandLine, HelpBuilder class.
        // Ideally those methods are exposed, we may switch to use them.
        internal static string FormatArgumentUsage(IReadOnlyList<Argument> arguments)
        {
            var sb = new StringBuilder();
            var end = default(Stack<char>);

            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument.IsHidden)
                {
                    continue;
                }

                var arityIndicator =
                    argument.Arity.MaximumNumberOfValues > 1
                        ? "..."
                        : "";

                var isOptional = IsOptional(argument);

                if (isOptional)
                {
                    sb.Append($"[<{argument.Name}>{arityIndicator}");
                    (end ??= new Stack<char>()).Push(']');
                }
                else
                {
                    sb.Append($"<{argument.Name}>{arityIndicator}");
                }

                sb.Append(' ');
            }

            if (sb.Length > 0)
            {
                sb.Length--;

                if (end is { })
                {
                    while (end.Count > 0)
                    {
                        sb.Append(end.Pop());
                    }
                }
            }

            return sb.ToString();
            bool IsMultiParented(Argument argument) =>
                argument.Parents.Count() > 1;

            bool IsOptional(Argument argument) =>
                IsMultiParented(argument) ||
                argument.Arity.MinimumNumberOfValues == 0;
        }

        internal static string FormatArgumentUsage(Argument argument) => FormatArgumentUsage(new[] { argument });

        internal static string FormatArgumentUsage(Option option) => FormatArgumentUsage(new[] { option });

        // separate instance as Option.Argument is internal.
        internal static string FormatArgumentUsage(IReadOnlyList<Option> options)
        {
            var sb = new StringBuilder();
            var end = default(Stack<char>);

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option.IsHidden)
                {
                    continue;
                }

                var arityIndicator =
                    option.Arity.MaximumNumberOfValues > 1
                        ? "..."
                        : "";

                var isOptional = IsOptional(option);

                if (isOptional)
                {
                    sb.Append($"[<{option.Name}>{arityIndicator}");
                    (end ??= new Stack<char>()).Push(']');
                }
                else
                {
                    sb.Append($"<{option.Name}>{arityIndicator}");
                }

                sb.Append(' ');
            }

            if (sb.Length > 0)
            {
                sb.Length--;

                if (end is { })
                {
                    while (end.Count > 0)
                    {
                        sb.Append(end.Pop());
                    }
                }
            }

            return sb.ToString();
            bool IsMultiParented(Option option) =>
                option.Parents.Count() > 1;

            bool IsOptional(Option option) =>
                IsMultiParented(option) ||
                option.Arity.MinimumNumberOfValues == 0;
        }
    }
}
