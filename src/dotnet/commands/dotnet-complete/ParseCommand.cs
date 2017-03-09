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

            var resultOfParsingArg =
                Parser.Instance.Parse(
                    args.Single());

            Console.WriteLine(resultOfParsingArg.Diagram());

            return 0;
        }
    }
}