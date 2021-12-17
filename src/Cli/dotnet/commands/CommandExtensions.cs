// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
