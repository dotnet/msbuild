using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemoveProjectToProjectReferenceParser
    {
        public static Command RemoveReference() =>
            Create.Command(
                "reference",
                LocalizableStrings.AppFullName,
                Accept
                    .OneOrMoreArguments()
                    .WithSuggestionsFrom(_ => Suggest.ProjectReferencesFromProjectFile())
                    .With(name: LocalizableStrings.ProjectPathArgumentName,
                          description: LocalizableStrings.ProjectPathArgumentDescription),
                CommonOptions.HelpOption(),
                Create.Option(
                    "-f|--framework",
                    LocalizableStrings.CmdFrameworkDescription,
                    Accept.ExactlyOneArgument()
                          .With(name: CommonLocalizableStrings.CmdFramework)));
    }
}
