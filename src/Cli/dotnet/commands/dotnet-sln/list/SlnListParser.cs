// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools.Sln;

namespace Microsoft.DotNet.Cli
{
    public static class SlnListParser
    {
        public static readonly Option<bool> SolutionFolderOption = new Option<bool>(new string[] { "-s", "--solution-folders" }, LocalizableStrings.ListSolutionFoldersArgumentDescription);

        public static Command GetCommand()
        {
            var command = new Command("list", LocalizableStrings.ListAppFullName);

            command.AddOption(SolutionFolderOption);

            return command;
        }
    }
}
