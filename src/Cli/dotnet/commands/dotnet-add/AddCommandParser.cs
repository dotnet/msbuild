// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddCommandParser
    {
        public static readonly Argument<string> ProjectArgument = new Argument<string>(CommonLocalizableStrings.ProjectArgumentName)
        {
            Description = CommonLocalizableStrings.ProjectArgumentDescription
        }.DefaultToCurrentDirectory();

        public static Command GetCommand()
        {
            var command = new Command("add", LocalizableStrings.NetAddCommand);

            command.AddArgument(ProjectArgument);
            command.AddCommand(AddPackageParser.GetCommand());
            command.AddCommand(AddProjectToProjectReferenceParser.GetCommand());

            return command;
        }
    }
}
