// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            var commandRemoveLeadingDotnet = commandName.Replace("dotnet-", string.Empty);
            var packageName = "dotnet-" + commandRemoveLeadingDotnet.ToLower();

            return string.Format(LocalizableStrings.CannotFindCommandAvailableAsTool,
                commandRemoveLeadingDotnet,
                packageName);
        }
    }
}
