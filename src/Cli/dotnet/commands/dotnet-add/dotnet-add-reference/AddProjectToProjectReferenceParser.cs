// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.Linq;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddProjectToProjectReferenceParser
    {
        public static readonly Argument ProjectPathArgument = new Argument(LocalizableStrings.ProjectPathArgumentName)
        {
            Description = LocalizableStrings.ProjectPathArgumentDescription,
            Arity = ArgumentArity.OneOrMore
        };

        public static readonly Option FrameworkOption = new Option(new string[] { "-f", "--framework" }, LocalizableStrings.CmdFrameworkDescription)
        {
            Argument = new Argument(Tools.Add.PackageReference.LocalizableStrings.CmdFramework) { Arity = ArgumentArity.ExactlyOne }
                    .AddSuggestions(Suggest.TargetFrameworksFromProjectFile().ToArray())
        };

        public static readonly Option InteractiveOption = CommonOptions.InteractiveOption();

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
