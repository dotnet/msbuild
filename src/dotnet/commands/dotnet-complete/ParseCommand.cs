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

            var result =
                Parser.Instance.Parse(
                    args.Single());

            Console.WriteLine(result.Diagram());

            if (result.Errors.Any())
            {
                Console.WriteLine();
                Console.WriteLine("ERRORS");
                Console.WriteLine();
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"[{error?.Option?.Name ?? "???"}] {error.Message}");
                }
            }

            return 0;
        }
    }
}