// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemoveProjectToProjectReferenceParser
    {
        public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new Argument<IEnumerable<string>>(LocalizableStrings.ProjectPathArgumentName)
        {
            Description = LocalizableStrings.ProjectPathArgumentDescription,
            Arity = ArgumentArity.OneOrMore,
        }.AddSuggestions(Suggest.ProjectReferencesFromProjectFile());

        public static readonly Option<string> FrameworkOption = new Option<string>(new string[] { "-f", "--framework" }, LocalizableStrings.CmdFrameworkDescription)
        {
            ArgumentHelpName = CommonLocalizableStrings.CmdFramework
        };

        public static Command GetCommand()
        {
            var command = new Command("reference", LocalizableStrings.AppFullName);

            command.AddArgument(ProjectPathArgument);
            command.AddOption(FrameworkOption);

            return command;
        }
    }
}
