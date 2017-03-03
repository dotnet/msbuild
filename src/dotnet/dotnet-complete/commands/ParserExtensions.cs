using System;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Tools
{
    public static class ParserExtensions
    {
        public static void ShowHelp(this ParseResult parseResult) => 
            Console.WriteLine(parseResult.Command().HelpView());
    }
}