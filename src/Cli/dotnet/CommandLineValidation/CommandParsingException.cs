// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal class CommandParsingException : Exception
    {
        public CommandParsingException(
            string message, 
            ParseResult parseResult = null) : base(message)
        {
            ParseResult = parseResult;
            Data.Add("CLI_User_Displayed_Exception", true);
        }

        public ParseResult ParseResult;
    }
}
