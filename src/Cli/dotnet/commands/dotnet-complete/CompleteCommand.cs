// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Completions;
using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    public class CompleteCommand
    {
        public static int Run(ParseResult parseResult)
        {
            return RunWithReporter(parseResult, Reporter.Output);
        }

        public static int RunWithReporter(string[] args, IReporter reporter)
        {
            var parser = Parser.Instance;
            var result = parser.ParseFrom("dotnet complete", args);
            return RunWithReporter(result, reporter);
        }

        public static int RunWithReporter(ParseResult result, IReporter reporter)
        {
            if (reporter == null)
            {
                throw new ArgumentNullException(nameof(reporter));
            }

            try
            {
                result.HandleDebugSwitch();

                var completions = Completions(result);

                foreach (var completion in completions)
                {
                    reporter.WriteLine(completion.Label);
                }
            }
            catch (Exception)
            {
                return 1;
            }

            return 0;
        }

        private static CompletionItem[] Completions(ParseResult complete)
        {
            var input = complete.GetValue(CompleteCommandParser.PathArgument) ?? string.Empty;

            var position = complete.GetValue(CompleteCommandParser.PositionOption);

            if (position > input.Length)
            {
                input += " ";
            }

            var result = Parser.Instance.Parse(input);

            return result.GetCompletions(position)
                .Distinct()
                .ToArray();
        }
    }
}
