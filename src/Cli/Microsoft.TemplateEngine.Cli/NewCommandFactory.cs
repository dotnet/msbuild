// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli
{
    public static class NewCommandFactory
    {
        public static CliCommand Create(string commandName, Func<ParseResult, ICliTemplateEngineHost> hostBuilder)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException($"'{nameof(commandName)}' cannot be null or whitespace.", nameof(commandName));
            }

            _ = hostBuilder ?? throw new ArgumentNullException(nameof(hostBuilder));

            return new NewCommand(commandName, hostBuilder);
        }
    }
}
