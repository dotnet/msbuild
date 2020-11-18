// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    public class CompleteCommand
    {
        public static int Run(string[] args)
        {
            return RunWithReporter(args, Reporter.Output);
        }

        public static int RunWithReporter(string [] args, IReporter reporter)
        {
            if (reporter == null)
            {
                throw new ArgumentNullException(nameof(reporter));
            }

            try
            {
                DebugHelper.HandleDebugSwitch(ref args);

                // get the parser for the current subcommand
                var parser = Parser.Instance;

                // parse the arguments
                var result = parser.ParseFrom("dotnet complete", args);

                var suggestions = Suggestions(result);

                foreach (var suggestion in suggestions)
                {
                    reporter.WriteLine(suggestion);
                }
            }
            catch (Exception)
            {
                return 1;
            }

            return 0;
        }

        private static string[] Suggestions(ParseResult complete)
        {
            var input = complete.ValueForArgument<string>(CompleteCommandParser.PathArgument) ?? string.Empty;

            var position = complete.ValueForOption<int>(CompleteCommandParser.PositionOption);

            if (position > input.Length)
            {
                input += " ";
            }

            var result = Parser.Instance.Parse(input);

            return result.GetSuggestions()
                .Distinct()
                .ToArray();
        }
    }
}
