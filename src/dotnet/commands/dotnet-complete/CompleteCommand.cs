// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Complete
{
    public class CompleteCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var log = new StringBuilder();
            log.AppendLine($"args: {string.Join(" ", args.Select(a => $"\"{a}\""))}");

            // get the parser for the current subcommand
            var completeCommandParser = ParserFor.DotnetCommand["complete"];

            // parse the arguments
            var result = completeCommandParser.Parse(args);

            log.AppendLine("diagram (1): " + result.Diagram());

            var complete = result["complete"];

            var suggestions = Suggestions(complete, log);

            log.AppendLine($"suggestions: {Environment.NewLine}{string.Join(Environment.NewLine, suggestions)}");

            File.WriteAllText("parse.log", log.ToString());

            foreach (var suggestion in suggestions)
            {
                Console.WriteLine(suggestion);
            }

            return 0;
        }

        private static string[] Suggestions(AppliedOption complete, StringBuilder log)
        {
            var input = complete.Arguments.SingleOrDefault() ?? "";

            var positionOption = complete.AppliedOptions.SingleOrDefault(a => a.Name == "position");
            if (positionOption != null)
            {
                var position = positionOption.Value<int>();

                if (position > input.Length)
                {
                    input += " ";
                }
            }

            var result = ParserFor.DotnetCommand.Parse(input);

            log.AppendLine("diagram (2): " + result.Diagram());

            return result.Suggestions()
                         .ToArray();
        }
    }
}