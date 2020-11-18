// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Cli
{
    internal class CommandParsingException : Exception
    {
        public CommandParsingException(
            string message, 
            string helpText = null) : base(message)
        {
            HelpText = helpText ?? "";
            Data.Add("CLI_User_Displayed_Exception", true);
        }

        public string HelpText { get; } = "";
    }
}
