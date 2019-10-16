// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnAddParser
    {
        public static Command SlnAdd() =>
            Create.Command("add",
                           LocalizableStrings.AddAppFullName,
                           Accept.OneOrMoreArguments(o => CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd)
                                 .With(name: LocalizableStrings.AddProjectPathArgumentName,
                                       description: LocalizableStrings.AddProjectPathArgumentDescription),
                           Create.Option(
                               "--in-root",
                               LocalizableStrings.InRoot,
                               Accept.NoArguments()),
                           Create.Option("-s|--solution-folder", LocalizableStrings.AddProjectSolutionFolderArgumentDescription, Accept.ExactlyOneArgument()),
                           CommonOptions.HelpOption());
    }
}
