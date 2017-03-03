// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

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
