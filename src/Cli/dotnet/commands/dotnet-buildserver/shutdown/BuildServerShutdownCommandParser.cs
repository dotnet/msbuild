// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.BuildServer.Shutdown;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.Shutdown.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ServerShutdownCommandParser
    {
        public static readonly CliOption<bool> MSBuildOption = new("--msbuild") { Description = LocalizableStrings.MSBuildOptionDescription };
        public static readonly CliOption<bool> VbcsOption = new("--vbcscompiler") { Description = LocalizableStrings.VBCSCompilerOptionDescription };
        public static readonly CliOption<bool> RazorOption = new("--razor") { Description = LocalizableStrings.RazorOptionDescription };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("shutdown", LocalizableStrings.CommandDescription);

            command.Options.Add(MSBuildOption);
            command.Options.Add(VbcsOption);
            command.Options.Add(RazorOption);

            command.SetAction((parseResult) => new BuildServerShutdownCommand(parseResult).Execute());

            return command;
        }
    }
}
