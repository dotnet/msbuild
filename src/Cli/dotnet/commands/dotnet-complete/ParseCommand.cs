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

            Console.WriteLine(result.Diagram());

            if (result.UnparsedTokens.Any())
            {
                Console.WriteLine("Unparsed Tokens: ");
                Console.WriteLine(string.Join(" ", result.UnparsedTokens));
            }

            var optionValuesToBeForwarded = result.OptionValuesToBeForwarded(ParseCommandParser.GetCommand());
            if (optionValuesToBeForwarded.Any())
            {
                Console.WriteLine("Option values to be forwarded: ");
                Console.WriteLine(string.Join(" ", optionValuesToBeForwarded));
            }
            if (result.Errors.Any())
            {
                Console.WriteLine();
                Console.WriteLine("ERRORS");
                Console.WriteLine();
                foreach (var error in result.Errors)
                {
                    Console.WriteLine(error?.Message);
                }
            }

            return 0;
        }
    }
}
