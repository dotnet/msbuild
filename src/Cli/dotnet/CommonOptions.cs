// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Tools;
using System.CommandLine;
using System.Linq;
using System.IO;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli
{
    internal static class CommonOptions
    {
        public static Option VerbosityOption() =>
            VerbosityOption(o => $"-verbosity:{o}");

        public static Option VerbosityOption(Func<string, string> format) =>
            new Option(
                new string[] { "-v", "--verbosity" },
                CommonLocalizableStrings.VerbosityOptionDescription)
            {
                Argument = new Argument(CommonLocalizableStrings.LevelArgumentName) { Arity = ArgumentArity.ExactlyOne }
                    .FromAmong(new string[] {"q", "quiet",
                                             "m", "minimal",
                                             "n", "normal",
                                             "d", "detailed",
                                             "diag", "diagnostic" })
            }.ForwardAsSingle(format);

        public static Option FrameworkOption(string description) =>
            new Option(
                new string[] { "-f", "--framework" },
                description)
            {
                Argument = new Argument(CommonLocalizableStrings.FrameworkArgumentName) { Arity = ArgumentArity.ExactlyOne }
                    .AddSuggestions(Suggest.TargetFrameworksFromProjectFile().ToArray())
            }.ForwardAsSingle<string>(o => $"-property:TargetFramework={o}");

        public static Option RuntimeOption(string description, bool withShortOption = true) =>
            new Option(
                withShortOption ? new string[] { "-r", "--runtime" } : new string[] { "--runtime" },
                description)
            {
                Argument = new Argument(CommonLocalizableStrings.RuntimeIdentifierArgumentName) { Arity = ArgumentArity.ExactlyOne }
                    //.AddSuggestions(Suggest.RunTimesFromProjectFile().ToArray()) TODO
            }.ForwardAsSingle<string>(o => $"-property:RuntimeIdentifier={o}");

        public static Option ConfigurationOption(string description) =>
            new Option(
                new string[] { "-c", "--configuration" },
                description)
            {
                Argument = new Argument(CommonLocalizableStrings.ConfigurationArgumentName) { Arity = ArgumentArity.ExactlyOne }
                    .AddSuggestions(Suggest.ConfigurationsFromProjectFileOrDefaults().ToArray())
            }.ForwardAsSingle<string>(o => $"-property:Configuration={o}");

        public static Option VersionSuffixOption() =>
            new Option(
                "--version-suffix",
                CommonLocalizableStrings.CmdVersionSuffixDescription)
            {
                Argument = new Argument(CommonLocalizableStrings.VersionSuffixArgumentName) { Arity = ArgumentArity.ExactlyOne }
            }.ForwardAsSingle<string>(o => $"-property:VersionSuffix={o}");

        public static Argument DefaultToCurrentDirectory(this Argument arg)
        {
            arg.SetDefaultValue(PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));
            return arg;
        }

        public static Option NoRestoreOption() =>
            new Option(
                "--no-restore",
                CommonLocalizableStrings.NoRestoreDescription);

        public static Option InteractiveMsBuildForwardOption() =>
            new Option(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription)
            .ForwardAs(Utils.Constants.MsBuildInteractiveOption);

        public static Option InteractiveOption() =>
            new Option(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription);

        public static Option DebugOption() => new Option("--debug");
    }
}
