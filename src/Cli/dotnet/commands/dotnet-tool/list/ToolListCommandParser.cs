// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolListCommandParser
    {
        public static Command ToolList()
        {
            return Create.Command(
                "list",
                LocalizableStrings.CommandDescription,
                Create.Option(
                    $"-g|--{ToolAppliedOption.GlobalOption}",
                    LocalizableStrings.GlobalOptionDescription,
                    Accept.NoArguments()),
                Create.Option(
                    $"--{ToolAppliedOption.LocalOption}",
                    LocalizableStrings.ToolPathOptionName,
                    Accept.NoArguments()),
                Create.Option(
                    $"--{ToolAppliedOption.ToolPathOption}",
                    LocalizableStrings.ToolPathOptionDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: LocalizableStrings.ToolPathOptionName)),
                CommonOptions.HelpOption());
        }
    }
}
