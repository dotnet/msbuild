// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    public class CommandUnknownException : GracefulException
    {
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
