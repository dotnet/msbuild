// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    public class CompleteCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            // get the parser for the current subcommand
            var completeCommandParser = Parser.DotnetCommand["complete"];

            // parse the arguments
            var result = completeCommandParser.Parse(args);

            var complete = result["complete"];

            var suggestions = Suggestions(complete);

#if DEBUG
            var log = new StringBuilder();
            log.AppendLine($"args: {string.Join(" ", args.Select(a => $"\"{a}\""))}");
            log.AppendLine("diagram: " + result.Diagram());
            File.WriteAllText("parse.log", log.ToString());
#endif

            foreach (var suggestion in suggestions)
            {
                Console.WriteLine(suggestion);
            }

            return 0;
        }

        private static string[] Suggestions(AppliedOption complete)
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

            var result = Parser.DotnetCommand.Parse(input);

            return result.Suggestions()
                         .ToArray();
        }
    }
}