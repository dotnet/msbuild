using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.ProjectToProjectReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemovePackageParser
    {
        public static Command RemovePackage() =>
            Create.Command(
                "package",
                LocalizableStrings.AppFullName,
                CommonOptions.HelpOption());
    }
}