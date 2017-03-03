// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Common;
using Newtonsoft.Json.Linq;
using static Microsoft.DotNet.Cli.CommandLine.Accept;
using static Microsoft.DotNet.Cli.CommandLine.Create;
using Command = Microsoft.DotNet.Cli.CommandLine.Command;

namespace Microsoft.DotNet.Tools
{
    public static class ParserFor
    {
        private static readonly Command _dotnetCommand =  Command("dotnet",
                    ".NET Command Line Tools",
                    NoArguments,
                    New(),
                    Restore(),
                    Build(),
                    Publish(),
                    Run(),
                    Test(),
                    Pack(),
                    Migrate(),
                    Clean(),
                    Sln(),
                    Add(),
                    Remove(),
                    List(),
                    NuGet(),
                    Command("msbuild", ""),
                    Command("vstest", ""),
                    Complete(),
                    HelpOption(),
                    Option("--info", ""),
                    VerbosityOption(),
                    Option("-d", ""));

        public static Command DotnetCommand { get; } = _dotnetCommand;

        private static Command Add() =>
            Command("add", 
                    ".NET Add Command",
                    ExactlyOneArgument.DefaultToCurrentDirectory(),
                    Command("package",
                            ".NET Add Package reference Command",
                            ExactlyOneArgument
                                .WithSuggestionsFrom(QueryNuGet),
                            HelpOption(),
                            Option("-v|--version",
                                   "Version for the package to be added.",
                                   ExactlyOneArgument
                                       .With(name: "VERSION")),
                            Option("-f|--framework",
                                   "Add reference only when targetting a specific framework",
                                   ExactlyOneArgument
                                       .With(name: "FRAMEWORK")),
                            Option("-n|--no-restore ",
                                   "Add reference without performing restore preview and compatibility check."),
                            Option("-s|--source",
                                   "Use specific NuGet package sources to use during the restore."),
                            Option("--package-directory",
                                   "Restore the packages to this Directory .",
                                   ExactlyOneArgument
                                       .With(name: "PACKAGE_DIRECTORY"))),
                    Command("reference",
                            "Command to add project to project reference",
                            OneOrMoreArguments,
                            HelpOption(),
                            Option("-f|--framework",
                                   "Add reference only when targetting a specific framework",
                                   AnyOneOf(TargetFrameworksFromProjectFile)
                                       .With(name: "FRAMEWORK"))),
                    HelpOption());

        private static Command Complete() =>
            Command("complete", "",
                    ExactlyOneArgument
                        .With(name: "path"),
                    Option("--position", "",
                           ExactlyOneArgument
                               .With(name: "command"),
                           o => int.Parse(o.Arguments.Single())));

        private static Command Build() =>
            Command("build",
                    ".NET Builder",
                    HelpOption(),
                    Option("-o|--output",
                           "Output directory in which to place built artifacts.",
                           ExactlyOneArgument
                               .With(name: "OUTPUT_DIR")),
                    Option("-f|--framework",
                           "Target framework to build for. The target framework has to be specified in the project file.",
                           AnyOneOf(TargetFrameworksFromProjectFile)),
                    Option("-r|--runtime",
                           "Target runtime to build for. The default is to build a portable application.",
                           AnyOneOf(RunTimesFromProjectFile)),
                    Option("-c|--configuration",
                           "Configuration to use for building the project. Default for most projects is  \"Debug\".",
                           ExactlyOneArgument
                               .With(name: "CONFIGURATION")
                               .WithSuggestionsFrom(_ => new[] { "DEBUG", "RELEASE" })),
                    Option("--version-suffix", "Defines the value for the $(VersionSuffix) property in the project",
                           ExactlyOneArgument
                               .With(name: "VERSION_SUFFIX")),
                    Option("--no-incremental", "Disables incremental build."),
                    Option("--no-dependencies", "Set this flag to ignore project-to-project references and only build the root project"),
                    VerbosityOption());

        private static Command Clean() =>
            Command("clean",
                    ".NET Clean Command",
                    HelpOption(),
                    Option("-o|--output", "Directory in which the build outputs have been placed.",
                           ExactlyOneArgument
                               .With(name: "OUTPUT_DIR")),
                    Option("-f|--framework", "Clean a specific framework.",
                           ExactlyOneArgument
                               .With(name: "FRAMEWORK")
                               .WithSuggestionsFrom(_ => TargetFrameworksFromProjectFile())),
                    Option("-c|--configuration",
                           "Clean a specific configuration.",
                           ExactlyOneArgument
                               .With(name: "CONFIGURATION")
                               .WithSuggestionsFrom(_ => new[] { "DEBUG", "RELEASE" })));

        private static Command List() =>
            Command("list",
                    ".NET List Command",
                    ZeroOrOneArgument
                        .With(name: "PROJECT",
                              description:
                              "The project file to operate on. If a file is not specified, the command will search the current directory for one.")
                    .DefaultToCurrentDirectory(),
                    HelpOption(),
                    Command("reference", "Command to list project to project references",
                            ExactlyOneArgument
                                .With(name: "PROJECT")
                                .With(description: "The project file to operate on. If a file is not specified, the command will search the current directory for one."),
                            HelpOption()));

        private static Command Migrate() =>
            Command("migrate",
                    ".NET Migrate Command",
                    HelpOption(),
                    Option("-t|--template-file",
                           "Base MSBuild template to use for migrated app. The default is the project included in dotnet new."),
                    Option("-v|--sdk-package-version",
                           "The version of the SDK package that will be referenced in the migrated app. The default is the version of the SDK in dotnet new."),
                    Option("-x|--xproj-file",
                           "The path to the xproj file to use. Required when there is more than one xproj in a project directory."),
                    Option("-s|--skip-project-references",
                           "Skip migrating project references. By default, project references are migrated recursively."),
                    Option("-r|--report-file",
                           "Output migration report to the given file in addition to the console."),
                    Option("--format-report-file-json",
                           "Output migration report file as json rather than user messages."),
                    Option("--skip-backup",
                           "Skip moving project.json, global.json, and *.xproj to a `backup` directory after successful migration."));

        private static Command New() =>
            Command("new",
                    "Initialize .NET projects.",
                    WithSuggestionsFrom("console",
                                        "classlib",
                                        "mstest",
                                        "xunit",
                                        "web",
                                        "mvc",
                                        "webapi",
                                        "sln"),
                    Option("-l|--list",
                           "List templates containing the specified name."),
                    Option("-lang|--language",
                           "Specifies the language of the template to create",
                           WithSuggestionsFrom("C#", "F#")
                               .With(defaultValue: () => "C#")),
                    Option("-n|--name",
                           "The name for the output being created. If no name is specified, the name of the current directory is used."),
                    Option("-o|--output",
                           "Location to place the generated output."),
                    Option("-h|--help",
                           "Displays help for this command."),
                    Option("-all|--show-all",
                           "Shows all templates"));

        private static Command NuGet() =>
            Command("nuget",
                    "NuGet Command Line 4.0.0.0",
                    HelpOption(),
                    Option("--version",
                           "Show version information"),
                    Option("-v|--verbosity",
                           "The verbosity of logging to use. Allowed values: Debug, Verbose, Information, Minimal, Warning, Error.",
                           ExactlyOneArgument
                               .With(name: "verbosity")),
                    Command("delete",
                            "Deletes a package from the server.",
                            ExactlyOneArgument
                                .With(name: "root",
                                      description: "The Package Id and version."),
                            HelpOption(),
                            Option("--force-english-output",
                                   "Forces the application to run using an invariant, English-based culture."),
                            Option("-s|--source",
                                   "Specifies the server URL",
                                   ExactlyOneArgument
                                       .With(name: "source")),
                            Option("--non-interactive",
                                   "Do not prompt for user input or confirmations."),
                            Option("-k|--api-key",
                                   "The API key for the server.",
                                   ExactlyOneArgument
                                       .With(name: "apiKey"))),
                    Command("locals",
                            "Clears or lists local NuGet resources such as http requests cache, packages cache or machine-wide global packages folder.",
                            AnyOneOf(@"all", @"http-cache", @"global-packages", @"temp")
                                .With(description: "Cache Location(s)  Specifies the cache location(s) to list or clear."),
                            HelpOption(),
                            Option("--force-english-output",
                                   "Forces the application to run using an invariant, English-based culture."),
                            Option("-c|--clear", "Clear the selected local resources or cache location(s)."),
                            Option("-l|--list", "List the selected local resources or cache location(s).")),
                    Command("push",
                            "Pushes a package to the server and publishes it.",
                            HelpOption(),
                            Option("--force-english-output",
                                   "Forces the application to run using an invariant, English-based culture."),
                            Option("-s|--source",
                                   "Specifies the server URL",
                                   ExactlyOneArgument
                                       .With(name: "source")),
                            Option("-ss|--symbol-source",
                                   "Specifies the symbol server URL. If not specified, nuget.smbsrc.net is used when pushing to nuget.org.",
                                   ExactlyOneArgument
                                       .With(name: "source")),
                            Option("-t|--timeout",
                                   "Specifies the timeout for pushing to a server in seconds. Defaults to 300 seconds (5 minutes).",
                                   ExactlyOneArgument
                                       .With(name: "timeout")),
                            Option("-k|--api-key", "The API key for the server.",
                                   ExactlyOneArgument
                                       .With(name: "apiKey")),
                            Option("-sk|--symbol-api-key", "The API key for the symbol server.",
                                   ExactlyOneArgument
                                       .With(name: "apiKey")),
                            Option("-d|--disable-buffering",
                                   "Disable buffering when pushing to an HTTP(S) server to decrease memory usage."),
                            Option("-n|--no-symbols",
                                   "If a symbols package exists, it will not be pushed to a symbols server.")));

        private static Command Pack() =>
            Command("pack",
                    ".NET Core NuGet Package Packer",
                    HelpOption(),
                    Option("-o|--output",
                           "Directory in which to place built packages.",
                           ExactlyOneArgument
                               .With(name: "OUTPUT_DIR")),
                    Option("--no-build",
                           "Skip building the project prior to packing. By default, the project will be built."),
                    Option("--include-symbols",
                           "Include packages with symbols in addition to regular packages in output directory."),
                    Option("--include-source",
                           "Include PDBs and source files. Source files go into the src folder in the resulting nuget package"),
                    Option("-c|--configuration",
                           "Configuration to use for building the project.  Default for most projects is  \"Debug\".",
                           ExactlyOneArgument
                               .With(name: "CONFIGURATION")
                               .WithSuggestionsFrom(_ => new[] { "DEBUG", "RELEASE" })),
                    Option("--version-suffix",
                           "Defines the value for the $(VersionSuffix) property in the project.",
                           ExactlyOneArgument
                               .With(name: "VERSION_SUFFIX")),
                    Option("-s|--serviceable",
                           "Set the serviceable flag in the package. For more information, please see https://aka.ms/nupkgservicing."),
                    VerbosityOption());

        private static Command Publish() =>
            Command("publish",
                    ".NET Publisher",
                    ExactlyOneArgument,
                    HelpOption(),
                    Option("-f|--framework",
                           "Target framework to publish for. The target framework has to be specified in the project file.",
                           AnyOneOf(TargetFrameworksFromProjectFile)
                               .With(name: "FRAMEWORK")),
                    Option("-r|--runtime",
                           "Publish the project for a given runtime. This is used when creating self-contained deployment. Default is to publish a framework-dependent app.",
                           AnyOneOf(RunTimesFromProjectFile)
                               .With(name: "RUNTIME_IDENTIFIER")),
                    Option("-o|--output",
                           "Output directory in which to place the published artifacts.",
                           ExactlyOneArgument
                               .With(name: "OUTPUT_DIR")),
                    Option("-c|--configuration", "Configuration to use for building the project.  Default for most projects is  \"Debug\".",
                           ExactlyOneArgument
                               .With(name: "CONFIGURATION")
                               .WithSuggestionsFrom(_ => new[] { "DEBUG", "RELEASE" })),
                    Option("--version-suffix", "Defines the value for the $(VersionSuffix) property in the project.",
                           ExactlyOneArgument
                               .With(name: "VERSION_SUFFIX")),
                    VerbosityOption());

        private static Command Remove() =>
            Command("remove",
                    ".NET Remove Command",
                    ZeroOrOneArgument
                        .With(name: "PROJECT")
                        .DefaultToCurrentDirectory(),
                    HelpOption(),
                    Command("package",
                            "Command to remove package reference.",
                            HelpOption()),
                    Command("reference",
                            "Command to remove project to project reference",
                            AnyOneOf(ProjectReferencesFromProjectFile),
                            HelpOption(),
                            Option("-f|--framework",
                                   "Remove reference only when targetting a specific framework",
                                   ExactlyOneArgument
                                       .With(name: "FRAMEWORK"))));

        private static Command Restore() =>
            Command("restore",
                    ".NET dependency restorer",
                    HelpOption(),
                    Option("-s|--source",
                           "Specifies a NuGet package source to use during the restore.",
                           ExactlyOneArgument
                               .With(name: "SOURCE")),
                    Option("-r|--runtime",
                           "Target runtime to restore packages for.",
                           AnyOneOf(RunTimesFromProjectFile)
                               .With(name: "RUNTIME_IDENTIFIER")),
                    Option("--packages",
                           "Directory to install packages in.",
                           ExactlyOneArgument
                               .With(name: "PACKAGES_DIRECTORY")),
                    Option("--disable-parallel",
                           "Disables restoring multiple projects in parallel."),
                    Option("--configfile",
                           "The NuGet configuration file to use.",
                           ExactlyOneArgument
                               .With(name: "FILE")),
                    Option("--no-cache",
                           "Do not cache packages and http requests."),
                    Option("--ignore-failed-sources",
                           "Treat package source failures as warnings."),
                    Option("--no-dependencies",
                           "Set this flag to ignore project to project references and only restore the root project"),
                    VerbosityOption());

        private static Command Run() =>
            Command("run",
                    ".NET Run Command",
                    HelpOption(),
                    Option("-c|--configuration",
                           @"Configuration to use for building the project. Default for most projects is ""Debug"".",
                           ExactlyOneArgument
                               .WithSuggestionsFrom(_ => new[] { "DEBUG", "RELEASE" })),
                    Option("-f|--framework",
                           "Build and run the app using the specified framework. The framework has to be specified in the project file.",
                           AnyOneOf(TargetFrameworksFromProjectFile)),
                    Option("-p|--project",
                           "The path to the project file to run (defaults to the current directory if there is only one project).",
                           ZeroOrOneArgument));

        private static Command Sln() =>
            Command("sln",
                    ".NET modify solution file command",

                    HelpOption(),
                    Command("add",
                            ".NET Add project(s) to a solution file Command",
                            ExactlyOneArgument
                                .With(name: "SLN_FILE"),
                            HelpOption()),
                    Command("list",
                            "List all projects in the solution.",
                            ExactlyOneArgument
                                .With(name: "SLN_FILE"),
                            HelpOption()),
                    Command("remove",
                            "Remove the specified project(s) from the solution. The project is not impacted."));

        private static Command Test() =>
            Command("test",
                    ".NET Test Driver",
                    Option("-h|--help",
                           "Show help information"),
                    Option("-s|--settings",
                           "Settings to use when running tests.",
                           ExactlyOneArgument
                               .With(name: "SETTINGS_FILE")),
                    Option("-t|--list-tests",
                           "Lists discovered tests"),
                    Option("--filter",
                           @"Run tests that match the given expression.
                                        Examples:
                                        Run tests with priority set to 1: --filter ""Priority = 1""
                                        Run a test with the specified full name: --filter ""FullyQualifiedName=Namespace.ClassName.MethodName""
                                        Run tests that contain the specified name: --filter ""FullyQualifiedName~Namespace.Class""
                                        More info on filtering support: https://aka.ms/vstest-filtering",
                           ExactlyOneArgument
                               .With(name: "EXPRESSION")),
                    Option("-a|--test-adapter-path",
                           "Use custom adapters from the given path in the test run.\r\n                          Example: --test-adapter-path <PATH_TO_ADAPTER>"),
                    Option("-l|--logger",
                           "Specify a logger for test results.\r\n                          Example: --logger \"trx[;LogFileName=<Defaults to unique file name>]\"",
                           ExactlyOneArgument
                               .With(name: "LoggerUri/FriendlyName")),
                    Option("-c|--configuration", "Configuration to use for building the project.  Default for most projects is  \"Debug\".",
                           ExactlyOneArgument
                               .With(name: "CONFIGURATION")
                               .WithSuggestionsFrom(_ => new[] { "DEBUG", "RELEASE" })),
                    Option("-f|--framework",
                           "Looks for test binaries for a specific framework",
                           AnyOneOf(() => TargetFrameworksFromProjectFile().ToArray())
                               .With(name: "FRAMEWORK")),
                    Option("-o|--output",
                           "Directory in which to find the binaries to be run",
                           ExactlyOneArgument
                               .With(name: "OUTPUT_DIR")),
                    Option("-d|--diag",
                           "Enable verbose logs for test platform.\r\n                          Logs are written to the provided file.",
                           ExactlyOneArgument
                               .With(name: "PATH_TO_FILE")),
                    Option("--no-build",
                           "Do not build project before testing."),
                    VerbosityOption());

        private static ArgumentsRule DefaultToCurrentDirectory(this ArgumentsRule rule) =>
            rule.With(defaultValue: () => PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));

        private static Option HelpOption() =>
            Option("-h|--help",
                   "Show help information",
                   NoArguments,
                   materialize: o => o.Option.Command().HelpView());

        private static Option VerbosityOption() =>
            Option("-v|--verbosity",
                   "Set the verbosity level of the command. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]",
                   AnyOneOf("q[uiet]",
                            "m[inimal]",
                            "n[ormal]",
                            "d[etailed]"));

        private static IEnumerable<string> QueryNuGet(string match)
        {
            var httpClient = new HttpClient();

            string result = null;

            try
            {
                var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = httpClient.GetAsync($"https://api-v2v3search-0.nuget.org/query?q={match}&skip=0&take=100&prerelease=true", cancellation.Token)
                                         .Result;

                result = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception)
            {
                yield break;
            }

            var json = JObject.Parse(result);

            foreach (var id in json["data"])
            {
                yield return id["id"].Value<string>();
            }
        }

        private static IEnumerable<string> TargetFrameworksFromProjectFile()
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(
                new ProjectCollection(),
                Directory.GetCurrentDirectory());

            foreach (var tfm in msbuildProj.GetTargetFrameworks())
            {
                yield return tfm.GetShortFolderName();
            }
        }

        private static IEnumerable<string> RunTimesFromProjectFile()
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(
                new ProjectCollection(),
                Directory.GetCurrentDirectory());

            return msbuildProj.GetRuntimeIdentifiers();
        }

        private static IEnumerable<string> ProjectReferencesFromProjectFile()
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(
                new ProjectCollection(),
                Directory.GetCurrentDirectory());

            return msbuildProj.GetProjectToProjectReferences()
                              .Select(r => r.Include);
        }
    }
}