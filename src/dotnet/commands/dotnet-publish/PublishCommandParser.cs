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
                Accept.ZeroOrMoreArguments,
                CommonOptions.HelpOption(),
                CommonOptions.FrameworkOption(),
                CommonOptions.RuntimeOption(),
                Create.Option(
                    "-o|--output",
                    LocalizableStrings.OutputOptionDescription,
                    Accept.ExactlyOneArgument
                        .With(name: LocalizableStrings.OutputOption)
                        .ForwardAs(o => $"/p:PublishDir={o.Arguments.Single()}")),
                CommonOptions.ConfigurationOption(),
                CommonOptions.VersionSuffixOption(),
                Create.Option(
                    "--filter", 
                    LocalizableStrings.FilterProjOptionDescription,
                    Accept.ExactlyOneArgument
                        .With(name: LocalizableStrings.FilterProjOption)
                        .ForwardAs(o => $"/p:FilterProjectFiles={o.Arguments.Single()}")),
                CommonOptions.VerbosityOption());
    }
}