// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.Add.ProjectToProjectReference;
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
                
        }.AddCompletions(Complete.TargetFrameworksFromProjectFile);

        public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("reference", LocalizableStrings.AppFullName);

            command.AddArgument(ProjectPathArgument);
            command.AddOption(FrameworkOption);
            command.AddOption(InteractiveOption);

            command.SetHandler((parseResult) => new AddProjectToProjectReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}
