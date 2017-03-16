// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemoveCommandParser
    {
        public static Command Remove() =>
            Create.Command("remove",
                           ".NET Remove Command",
                           Accept.ExactlyOneArgument()
                                 .DefaultToCurrentDirectory()
                                 .With(name: "PROJECT",
                                       description: CommonLocalizableStrings.ArgumentsProjectDescription)
                                 .DefaultToCurrentDirectory(),
                           CommonOptions.HelpOption(),
                           Create.Command(
                               "package",
                               LocalizableStrings.AppFullName,
                               CommonOptions.HelpOption()),
                           Create.Command(
                               "reference",
                               LocalizableStrings.AppFullName,
                               Accept
                                   .OneOrMoreArguments()
                                   .WithSuggestionsFrom(_ => Suggest.ProjectReferencesFromProjectFile())
                                   .With(name: "args",
                                         description: LocalizableStrings.AppHelpText),
                               CommonOptions.HelpOption(),
                               Create.Option(
                                   "-f|--framework",
                                   "Remove reference only when targeting a specific framework",
                                   Accept.ExactlyOneArgument()
                                         .With(name: "FRAMEWORK"))));
    }
}