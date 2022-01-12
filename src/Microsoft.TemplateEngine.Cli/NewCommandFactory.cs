// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli
{
    public static class NewCommandFactory
    {
        public static Command Create(string commandName, Func<ParseResult, ITemplateEngineHost> hostBuilder, Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder, NewCommandCallbacks callbacks)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException($"'{nameof(commandName)}' cannot be null or whitespace.", nameof(commandName));
            }

            _ = hostBuilder ?? throw new ArgumentNullException(nameof(hostBuilder));
            _ = telemetryLoggerBuilder ?? throw new ArgumentNullException(nameof(telemetryLoggerBuilder));
            _ = callbacks ?? throw new ArgumentNullException(nameof(callbacks));

            return new NewCommand(commandName, hostBuilder, telemetryLoggerBuilder, callbacks);
        }
    }
}
