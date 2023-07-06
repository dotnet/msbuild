// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal static class SymbolResultExtensions
    {
        internal static CliToken Token(this SymbolResult symbolResult)
        {
            return symbolResult switch
            {
                CommandResult commandResult => commandResult.IdentifierToken,
                OptionResult optionResult => optionResult.IdentifierToken is null ?
                                             new CliToken($"--{optionResult.Option.Name}", CliTokenType.Option, optionResult.Option)
                                             : optionResult.IdentifierToken,
                ArgumentResult argResult => new CliToken(argResult.GetValueOrDefault<string>(), CliTokenType.Argument, argResult.Argument),
                _ => default
            };
        }
    }
}
