// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public static class Parser
    {
        public static CommandLine.Parser Instance { get; } = new CommandLine.Parser(
            delimiters: Array.Empty<char>(),
            options: Create.Command("dotnet",
                                    ".NET Command Line Tools",
                                    Accept.NoArguments(),
                                    NewCommandParser.New(),
                                    RestoreCommandParser.Restore(),
                                    BuildCommandParser.Build(),
                                    PublishCommandParser.Publish(),
                                    RunCommandParser.Run(),
                                    TestCommandParser.Test(),
                                    PackCommandParser.Pack(),
                                    MigrateCommandParser.Migrate(),
                                    CleanCommandParser.Clean(),
                                    SlnCommandParser.Sln(),
                                    AddCommandParser.Add(),
                                    RemoveCommandParser.Remove(),
                                    ListCommandParser.List(),
                                    NuGetCommandParser.NuGet(),
                                    CacheCommandParser.Cache(),
                                    Create.Command("msbuild", ""),
                                    Create.Command("vstest", ""), 
                                    CompleteCommandParser.Complete(),
                                    CommonOptions.HelpOption(),
                                    Create.Option("--info", ""),
                                    Create.Option("-d", ""),
                                    Create.Option("--debug", "")));
    }
}