using System;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli
{
    public class ParseCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            ParseResult result;
            try
            {
                result = Parser.Instance.Parse(
                    args.Single());
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("The parser threw an exception.", e);
            }

            Console.WriteLine(result.Diagram());

            if (result.Errors.Any())
            {
                Console.WriteLine();
                Console.WriteLine("ERRORS");
                Console.WriteLine();
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"[{error?.Option?.Name ?? "???"}] {error?.Message}");
                }
            }

            return 0;
        }
    }
}