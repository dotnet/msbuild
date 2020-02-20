using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Tools.Help
{
    internal static class HelpCommandParser
    {
        public static Command Help()
        {
            return Create.Command(
                "help",
                LocalizableStrings.AppFullName,
                Accept.ZeroOrOneArgument()
                    .With(
                        LocalizableStrings.CommandArgumentDescription,
                        LocalizableStrings.CommandArgumentName),
                         CommonOptions.HelpOption());
        }
    }
}

