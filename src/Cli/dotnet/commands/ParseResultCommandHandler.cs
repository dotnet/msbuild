// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli;

internal class ParseResultCommandHandler : ICommandHandler
{
    private readonly Func<ParseResult, int> _action;

    internal ParseResultCommandHandler(Func<ParseResult, int> action)
    {
        _action = action;
    }

    public Task<int> InvokeAsync(InvocationContext context) => Task.FromResult(_action(context.ParseResult));
    public int Invoke(InvocationContext context) => _action(context.ParseResult);
}
