// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddProjectToProjectReferenceParser
    {
        public static Command AddProjectReference()
        {
            return Create.Command(
                "reference",
                LocalizableStrings.AppFullName,
                Accept.OneOrMoreArguments()
                      .With(name: LocalizableStrings.ProjectPathArgumentName,
                            description: LocalizableStrings.ProjectPathArgumentDescription),
                CommonOptions.HelpOption(),
                Create.Option("-f|--framework", LocalizableStrings.CmdFrameworkDescription,
                              Accept.ExactlyOneArgument()
                                    .WithSuggestionsFrom(_ => Suggest.TargetFrameworksFromProjectFile())
                                    .With(name: Tools.Add.PackageReference.LocalizableStrings.CmdFramework)),
                CommonOptions.InteractiveOption());
        }
    }
}
