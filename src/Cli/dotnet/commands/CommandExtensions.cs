using System;
using System.CommandLine.Parsing;
using System.CommandLine;

namespace Microsoft.DotNet.Cli {

    public static class CommandExtensions {
        public static Command SetHandler(this Command command, Func<ParseResult, int> func) {
            command.Handler = new ParseResultCommandHandler(func);
            return command;
        }
    }

}