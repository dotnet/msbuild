using System;

namespace Microsoft.TemplateEngine.Cli
{
    internal class CommandParserException : Exception
    {
        internal CommandParserException(string message, string argument)
            : base(message)
        {
            Argument = argument;
        }

        internal CommandParserException(string message, string argument, Exception innerException)
            : base(message, innerException)
        {
            Argument = argument;
        }

        internal string Argument { get; }
    }
}
