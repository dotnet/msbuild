using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class TestCommandParser
    {
        public static Command Test() =>
            Create.Command("test",
                           ".NET Test Driver",
                           Create.Option("-h|--help",
                                         "Show help information"),
                           Create.Option("-s|--settings",
                                         "Settings to use when running tests.",
                                         Accept.ExactlyOneArgument()
                                               .With(name: "SETTINGS_FILE")),
                           Create.Option("-t|--list-tests",
                                         "Lists discovered tests"),
                           Create.Option("--filter",
                                         @"Run tests that match the given expression.
                                        Examples:
                                        Run tests with priority set to 1: --filter ""Priority = 1""
                                        Run a test with the specified full name: --filter ""FullyQualifiedName=Namespace.ClassName.MethodName""
                                        Run tests that contain the specified name: --filter ""FullyQualifiedName~Namespace.Class""
                                        More info on filtering support: https://aka.ms/vstest-filtering",
                                         Accept.ExactlyOneArgument()
                                               .With(name: "EXPRESSION")),
                           Create.Option("-a|--test-adapter-path",
                                         "Use custom adapters from the given path in the test run.\r\n                          Example: --test-adapter-path <PATH_TO_ADAPTER>"),
                           Create.Option("-l|--logger",
                                         "Specify a logger for test results.\r\n                          Example: --logger \"trx[;LogFileName=<Defaults to unique file name>]\"",
                                         Accept.ExactlyOneArgument()
                                               .With(name: "LoggerUri/FriendlyName")),
                           Create.Option("-c|--configuration", "Configuration to use for building the project.  Default for most projects is  \"Debug\".",
                                         Accept.ExactlyOneArgument()
                                               .With(name: "CONFIGURATION")
                                               .WithSuggestionsFrom("DEBUG", "RELEASE")),
                           Create.Option("-f|--framework",
                                         "Looks for test binaries for a specific framework",
                                         Accept.AnyOneOf(Suggest.TargetFrameworksFromProjectFile)
                                               .With(name: "FRAMEWORK")),
                           Create.Option("-o|--output",
                                         "Directory in which to find the binaries to be run",
                                         Accept.ExactlyOneArgument()
                                               .With(name: "OUTPUT_DIR")),
                           Create.Option("-d|--diag",
                                         "Enable verbose logs for test platform.\r\n                          Logs are written to the provided file.",
                                         Accept.ExactlyOneArgument()
                                               .With(name: "PATH_TO_FILE")),
                           Create.Option("--no-build",
                                         "Do not build project before testing."),
                           CommonOptions.VerbosityOption());
    }
}