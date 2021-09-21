// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemoveCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-remove";

        public static readonly Argument<string> ProjectArgument = new Argument<string>(CommonLocalizableStrings.ProjectArgumentName)
        {
            Description = CommonLocalizableStrings.ProjectArgumentDescription
        }.DefaultToCurrentDirectory();

        public static Command GetCommand()
        {
            var command = new DocumentedCommand("remove", DocsLink, LocalizableStrings.NetRemoveCommand);

            command.AddArgument(ProjectArgument);
            command.AddCommand(RemovePackageParser.GetCommand());
            command.AddCommand(RemoveProjectToProjectReferenceParser.GetCommand());

            command.Handler = CommandHandler.Create((Func<int>)(() => throw new Exception("TODO command not found")));

            return command;
        }
    }
}
