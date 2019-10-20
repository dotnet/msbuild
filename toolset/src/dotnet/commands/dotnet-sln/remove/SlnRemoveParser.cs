// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class SlnRemoveParser
    {
        public static Command SlnRemove() =>
            Create.Command("remove",
                           LocalizableStrings.RemoveAppFullName,
                           Accept.OneOrMoreArguments(o => CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove)
                                 .With(name: LocalizableStrings.RemoveProjectPathArgumentName,
                                       description: LocalizableStrings.RemoveProjectPathArgumentDescription),
                           CommonOptions.HelpOption());
    }
}