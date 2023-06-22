// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli;

public static class CommandExtensions
{
    public static Command SetHandler(this Command command, Func<ParseResult, int> func)
    {
        command.Handler = new ParseResultCommandHandler(func);
        return command;
    }
}
