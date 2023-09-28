// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public class ParseCommand
    {
        public static int Run(ParseResult result)
        {
            result.HandleDebugSwitch();

            var tokens = result.Tokens.Skip(1).Select(t => t.Value).ToArray();
            var reparsed = Parser.Instance.Parse(tokens);
            Console.WriteLine(reparsed.ToString());


            if (reparsed.UnmatchedTokens.Any())
            {
                Console.WriteLine("Unmatched Tokens: ");
                Console.WriteLine(string.Join(" ", reparsed.UnmatchedTokens));
            }

            var optionValuesToBeForwarded = reparsed.OptionValuesToBeForwarded(ParseCommandParser.GetCommand());
            if (optionValuesToBeForwarded.Any())
            {
                Console.WriteLine("Option values to be forwarded: ");
                Console.WriteLine(string.Join(" ", optionValuesToBeForwarded));
            }
            if (reparsed.Errors.Any())
            {
                Console.WriteLine();
                Console.WriteLine("ERRORS");
                Console.WriteLine();
                foreach (var error in reparsed.Errors)
                {
                    Console.WriteLine(error?.Message);
                }
            }

            return 0;
        }
    }
}
