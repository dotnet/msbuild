// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.BuildServer.Shutdown;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.Shutdown.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ServerShutdownCommandParser
    {
        public static readonly Option<bool> MSBuildOption = new Option<bool>("--msbuild", LocalizableStrings.MSBuildOptionDescription);
        public static readonly Option<bool> VbcsOption = new Option<bool>("--vbcscompiler", LocalizableStrings.VBCSCompilerOptionDescription);
        public static readonly Option<bool> RazorOption = new Option<bool>("--razor", LocalizableStrings.RazorOptionDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("shutdown", LocalizableStrings.CommandDescription);

            command.AddOption(MSBuildOption);
            command.AddOption(VbcsOption);
            command.AddOption(RazorOption);

            command.SetHandler((parseResult) => new BuildServerShutdownCommand(parseResult).Execute());

            return command;
        }
    }
}
