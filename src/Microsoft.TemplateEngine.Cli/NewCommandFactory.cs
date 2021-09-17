// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli
{
    public static class NewCommandFactory
    {
        public static Command Create(string commandName, ITemplateEngineHost host, ITelemetryLogger telemetryLogger, NewCommandCallbacks callbacks)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException($"'{nameof(commandName)}' cannot be null or whitespace.", nameof(commandName));
            }

            _ = host ?? throw new ArgumentNullException(nameof(host));
            _ = telemetryLogger ?? throw new ArgumentNullException(nameof(telemetryLogger));
            _ = callbacks ?? throw new ArgumentNullException(nameof(callbacks));

            return new NewCommand(commandName, host, telemetryLogger, callbacks);
        }
    }
}
