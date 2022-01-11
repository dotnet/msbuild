// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddProjectToProjectReferenceParser
    {
        public static readonly Argument<IEnumerable<string>> ProjectPathArgument = new Argument<IEnumerable<string>>(LocalizableStrings.ProjectPathArgumentName)
        {
            Description = LocalizableStrings.ProjectPathArgumentDescription,
            Arity = ArgumentArity.OneOrMore
        };

        public static readonly Option<string> FrameworkOption = new Option<string>(new string[] { "-f", "--framework" }, LocalizableStrings.CmdFrameworkDescription)
        {
            ArgumentHelpName = Tools.Add.PackageReference.LocalizableStrings.CmdFramework
                
        }.AddSuggestions(Suggest.TargetFrameworksFromProjectFile());

        public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption();

        public static Command GetCommand()
        {
            var command = new Command("reference", LocalizableStrings.AppFullName);

            command.AddArgument(ProjectPathArgument);
            command.AddOption(FrameworkOption);
            command.AddOption(InteractiveOption);

            return command;
        }
    }
}
