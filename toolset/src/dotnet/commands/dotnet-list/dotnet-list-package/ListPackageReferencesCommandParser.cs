// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.List.PackageReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListPackageReferencesCommandParser
    {
        public static Command ListPackageReferences() => Create.Command(
                "package",
                LocalizableStrings.AppFullName,
                Accept.NoArguments(),
                CommonOptions.HelpOption(),
                Create.Option("--outdated",
                              LocalizableStrings.CmdOutdatedDescription,
                              Accept.NoArguments().ForwardAs("--outdated")),
                Create.Option("--deprecated",
                              LocalizableStrings.CmdDeprecatedDescription,
                              Accept.NoArguments().ForwardAs("--deprecated")),
                Create.Option("--framework",
                              LocalizableStrings.CmdFrameworkDescription,
                              Accept.OneOrMoreArguments()
                                    .With(name: LocalizableStrings.CmdFramework)
                                    .ForwardAsMany(o => ForwardedArguments("--framework", o.Arguments))),
                Create.Option("--include-transitive",
                              LocalizableStrings.CmdTransitiveDescription,
                              Accept.NoArguments().ForwardAs("--include-transitive")),
                Create.Option("--include-prerelease",
                              LocalizableStrings.CmdPrereleaseDescription,
                              Accept.NoArguments().ForwardAs("--include-prerelease")),
                Create.Option("--highest-patch",
                              LocalizableStrings.CmdHighestPatchDescription,
                              Accept.NoArguments().ForwardAs("--highest-patch")),
                Create.Option("--highest-minor",
                              LocalizableStrings.CmdHighestMinorDescription,
                              Accept.NoArguments().ForwardAs("--highest-minor")),
                Create.Option("--config",
                              LocalizableStrings.CmdConfigDescription,
                              Accept.ExactlyOneArgument()
                                    .With(name: LocalizableStrings.CmdConfig)
                                    .ForwardAsMany(o => new [] { "--config", o.Arguments.Single() })),
                Create.Option("--source",
                              LocalizableStrings.CmdSourceDescription,
                              Accept.OneOrMoreArguments()
                                    .With(name: LocalizableStrings.CmdSource)
                                    .ForwardAsMany(o => ForwardedArguments("--source", o.Arguments))),
                Create.Option("--interactive",
                             CommonLocalizableStrings.CommandInteractiveOptionDescription,
                             Accept.NoArguments().ForwardAs("--interactive")));

        private static IEnumerable<string> ForwardedArguments(string token, IEnumerable<string> arguments)
        {
            foreach (var arg in arguments)
            {
                yield return token;
                yield return arg;
            }
        }
    }
}
