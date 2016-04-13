using System;

namespace Microsoft.DotNet.Cli.Utils
{
    public class CommandUnknownException : GracefulException
    {
        public CommandUnknownException()
        {
        }

        public CommandUnknownException(string commandName) : base($"No executable found matching command \"{commandName}\"")
        {
        }

        public CommandUnknownException(string commandName, Exception innerException) : base($"No executable found matching command \"{commandName}\"", innerException)
        {
        }
    }
}