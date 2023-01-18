// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli
{
    public static class NewCommandFactory
    {
        public static Command Create(string commandName, Func<ParseResult, ICliTemplateEngineHost> hostBuilder)
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
