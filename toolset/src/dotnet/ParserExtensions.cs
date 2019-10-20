using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public static class ParserExtensions
    {
        public static ParseResult ParseFrom(
            this CommandLine.Parser parser,
            string context,
            string[] args) =>
            parser.Parse(context.Split(' ').Concat(args).ToArray());
    }
}