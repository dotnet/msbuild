using System;

namespace Microsoft.TemplateEngine.Cli
{
    public class CommandParserException : Exception
    {
        public CommandParserException(string message, string argument)
            : base(message)
        {
            Argument = argument;
        }

        public CommandParserException(string message, string argument, Exception innerException)
            : base(message, innerException)
        {
            Argument = argument;
        }

        public string Argument { get; }
    }
}