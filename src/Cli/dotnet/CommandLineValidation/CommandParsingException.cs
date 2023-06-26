// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
