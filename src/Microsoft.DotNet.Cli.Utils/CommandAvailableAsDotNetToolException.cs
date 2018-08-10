// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Cli.Utils
{
    public class CommandAvailableAsDotNetToolException : GracefulException
    {
        public CommandAvailableAsDotNetToolException(string commandName) : base(GetMessage(commandName))
        {
        }

        public CommandAvailableAsDotNetToolException(string commandName, Exception innerException) : base(
            GetMessage(commandName), innerException)
        {
        }

        private static string GetMessage(string commandName)
        {
            var commandRemoveLeadningDotnet = commandName.Replace("dotnet-", string.Empty);
            var packageName = "dotnet-" + commandRemoveLeadningDotnet.ToLower();

            return string.Format(LocalizableStrings.CannotFindCommandAvailableAsTool,
                commandRemoveLeadningDotnet,
                packageName);
        }
    }
}
