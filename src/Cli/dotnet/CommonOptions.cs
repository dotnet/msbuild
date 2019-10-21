using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.Cli
{
    internal static class CommonOptions
    {
        public static Option HelpOption() =>
            Create.Option(
                "-h|--help",
                CommonLocalizableStrings.ShowHelpDescription,
                Accept.NoArguments());

        public static Option VerbosityOption() =>
            Create.Option(
                "-v|--verbosity",
                CommonLocalizableStrings.VerbosityOptionDescription,
                Accept.AnyOneOf(
                          "q", "quiet",
                          "m", "minimal",
                          "n", "normal",
                          "d", "detailed",
                          "diag", "diagnostic")
                      .With(name: CommonLocalizableStrings.LevelArgumentName)
                      .ForwardAsSingle(o => $"-verbosity:{o.Arguments.Single()}"));
        
        public static Option FrameworkOption(string description) =>
            Create.Option(
                "-f|--framework",
                description,
                Accept.ExactlyOneArgument()
                    .WithSuggestionsFrom(_ => Suggest.TargetFrameworksFromProjectFile())
                    .With(name: CommonLocalizableStrings.FrameworkArgumentName)
                    .ForwardAsSingle(o => $"-property:TargetFramework={o.Arguments.Single()}"));
        
        public static Option RuntimeOption(string description, bool withShortOption = true) =>
            Create.Option(
                withShortOption ? "-r|--runtime" : "--runtime",
                description,
                Accept.ExactlyOneArgument()
                    .WithSuggestionsFrom(_ => Suggest.RunTimesFromProjectFile())
                    .With(name: CommonLocalizableStrings.RuntimeIdentifierArgumentName)
                    .ForwardAsSingle(o => $"-property:RuntimeIdentifier={o.Arguments.Single()}"));
                
        public static Option ConfigurationOption(string description) =>
            Create.Option(
                "-c|--configuration",
                description,
                Accept.ExactlyOneArgument()
                    .With(name: CommonLocalizableStrings.ConfigurationArgumentName)
                    .WithSuggestionsFrom(_ => Suggest.ConfigurationsFromProjectFileOrDefaults())
                    .ForwardAsSingle(o => $"-property:Configuration={o.Arguments.Single()}"));

        public static Option VersionSuffixOption() =>
            Create.Option(
                "--version-suffix",
                CommonLocalizableStrings.CmdVersionSuffixDescription,
                Accept.ExactlyOneArgument()
                    .With(name: CommonLocalizableStrings.VersionSuffixArgumentName)
                    .ForwardAsSingle(o => $"-property:VersionSuffix={o.Arguments.Single()}"));

        public static ArgumentsRule DefaultToCurrentDirectory(this ArgumentsRule rule) =>
            rule.With(defaultValue: () => PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));

        public static Option NoRestoreOption() =>
            Create.Option(
                "--no-restore",
                CommonLocalizableStrings.NoRestoreDescription,
                Accept.NoArguments());

        public static Option InteractiveMsBuildForwardOption() =>
            Create.Option(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription,
                Accept.NoArguments()
                    .ForwardAs(Utils.Constants.MsBuildInteractiveOption));

        public static Option InteractiveOption() =>
            Create.Option(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription,
                Accept.NoArguments());
    }
}
