// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemoveCommandParser
    {
        public static Command Remove() =>
            Create.Command("remove",
                           LocalizableStrings.NetRemoveCommand,
                           Accept.ExactlyOneArgument()
                                 .DefaultToCurrentDirectory()
                                 .With(name: CommonLocalizableStrings.ProjectArgumentName,
                                       description: CommonLocalizableStrings.ProjectArgumentDescription)
                                 .DefaultToCurrentDirectory(),
                           CommonOptions.HelpOption(),
                           RemovePackageParser.RemovePackage(),
                           RemoveProjectToProjectReferenceParser.RemoveReference());
    }
}
