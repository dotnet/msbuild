using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.PackageReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemovePackageParser
    {
        public static Command RemovePackage()
        {
            return Create.Command(
                "package",
                LocalizableStrings.AppFullName,
                Accept.ExactlyOneArgument()
                      .With(name: Tools.Add.PackageReference.LocalizableStrings.CmdPackage,
                            description: LocalizableStrings.AppHelpText),
                CommonOptions.HelpOption(),
                Create.Option("--interactive",
                                 Tools.Add.PackageReference.LocalizableStrings.CmdInteractiveRestoreDescription,
                                 Accept.NoArguments()
                                   .ForwardAs("--interactive")));
        }
    }
}
