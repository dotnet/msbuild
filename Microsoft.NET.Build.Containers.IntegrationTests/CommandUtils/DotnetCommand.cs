// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Abstractions;

namespace Microsoft.DotNet.CommandUtils
{
    internal sealed class DotnetCommand : TestCommand
    {
        private string _executableFilePath = "dotnet";

        internal DotnetCommand(ITestOutputHelper log, string subcommand, params string[] args) : base(log)
        {
            Arguments.Add(subcommand);
            Arguments.AddRange(args);
        }

        internal DotnetCommand WithoutTelemetry()
        {
            WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "true");
            return this;
        }

        internal DotnetCommand WithCustomExecutablePath(string? executableFilePath)
        {
            if (!string.IsNullOrEmpty(executableFilePath))
            {
                _executableFilePath = executableFilePath!;
            }
            return this;
        }

        private protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = _executableFilePath,
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory
            };
            return sdkCommandSpec;
        }
    }
}
