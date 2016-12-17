using System;

namespace Microsoft.DotNet.Cli.Utils
{
    public class CommandUnknownException : GracefulException
    {
        public CommandUnknownException()
        {
        }

        public CommandUnknownException(string commandName) : base(string.Format(
            LocalizableStrings.NoExecutableFoundMatchingCommand,
            commandName))
        {
        }

        public CommandUnknownException(string commandName, Exception innerException) : base(
            string.Format(
                LocalizableStrings.NoExecutableFoundMatchingCommand,
                commandName),
            innerException)
        {
        }
    }
}