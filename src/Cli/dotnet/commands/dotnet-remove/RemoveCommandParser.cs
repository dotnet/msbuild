// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemoveCommandParser
    {
        public static readonly Argument<string> ProjectArgument = new Argument<string>(CommonLocalizableStrings.ProjectArgumentName)
        {
            Description = CommonLocalizableStrings.ProjectArgumentDescription
        }.DefaultToCurrentDirectory();

        public static Command GetCommand()
        {
            var command = new Command("remove", LocalizableStrings.NetRemoveCommand);

            command.AddArgument(ProjectArgument);
            command.AddCommand(RemovePackageParser.GetCommand());
            command.AddCommand(RemoveProjectToProjectReferenceParser.GetCommand());

            return command;
        }
    }
}
