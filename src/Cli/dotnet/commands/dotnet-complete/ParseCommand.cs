using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    public class ParseCommand
    {
        public static int Run(ParseResult result)
        {
            result.HandleDebugSwitch();

            var tokens = result.Tokens.Skip(1).Select(t => t.Value).ToArray();
            var reparsed = Microsoft.DotNet.Cli.Parser.Instance.Parse(tokens);
            Console.WriteLine(reparsed.Diagram());


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
