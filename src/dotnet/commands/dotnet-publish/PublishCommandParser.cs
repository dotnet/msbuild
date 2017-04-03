// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Publish.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class PublishCommandParser
    {
        public static Command Publish() =>
            Create.Command(
                "publish",
                LocalizableStrings.AppFullName,
                Accept.ZeroOrMoreArguments(),
                CommonOptions.HelpOption(),
                CommonOptions.FrameworkOption(),
                CommonOptions.RuntimeOption(),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.OutputOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.OutputOption)
                          .ForwardAsSingle(o => $"/p:PublishDir={o.Arguments.Single()}")),
                CommonOptions.ConfigurationOption(),
                CommonOptions.VersionSuffixOption(),
                Create.Option(
                    "--filter",
                    LocalizableStrings.FilterProjOptionDescription,
                    Accept.OneOrMoreArguments()
                          .With(name: LocalizableStrings.FilterProjOption)
                          .ForwardAsSingle(o => $"/p:FilterProjectFiles={string.Join("%3B", o.Arguments)}")),
                CommonOptions.VerbosityOption());
    }
}