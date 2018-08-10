// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.BuildServer.Shutdown.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ServerShutdownCommandParser
    {
        public static Command CreateCommand()
        {
            return Create.Command(
                "shutdown",
                LocalizableStrings.CommandDescription,
                Create.Option(
                    "--msbuild",
                    LocalizableStrings.MSBuildOptionDescription,
                    Accept.NoArguments()),
                Create.Option(
                    "--vbcscompiler",
                    LocalizableStrings.VBCSCompilerOptionDescription,
                    Accept.NoArguments()),
                Create.Option(
                    "--razor",
                    LocalizableStrings.RazorOptionDescription,
                    Accept.NoArguments()),
                CommonOptions.HelpOption());
        }
    }
}
