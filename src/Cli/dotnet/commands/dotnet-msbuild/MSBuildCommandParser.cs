// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;
using LocalizableStrings = Microsoft.DotNet.Tools.Build.LocalizableStrings;

namespace Microsoft.DotNet.Tools.MSBuild
{
    internal static class MSBuildCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-msbuild";

        public static readonly Argument<string[]> Arguments = new Argument<string[]>();

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("msbuild", DocsLink, LocalizableStrings.AppFullName)
            {
                Arguments
            };

            command.AddOption(CommonOptions.DisableBuildServersOption);

            command.SetHandler(MSBuildCommand.Run);

            return command;
        }
    }
}
