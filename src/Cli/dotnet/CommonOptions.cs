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
        public static Option PropertiesOption() =>
            new Option<string[]>(new string[] { "-property", "/p" })
            {
                IsHidden = true
            }.ForwardAsProperty()
            .AllowSingleArgPerToken();

        public static Option VerbosityOption() =>
            VerbosityOption(o => $"-verbosity:{o}");

        public static Option VerbosityOption(Func<VerbosityOptions, string> format) =>
            new Option<VerbosityOptions>(
                new string[] { "-v", "--verbosity" },
                description: CommonLocalizableStrings.VerbosityOptionDescription)
            {
                Argument = new Argument<VerbosityOptions>(CommonLocalizableStrings.LevelArgumentName)
            }.ForwardAsSingle(format);

        public static Option FrameworkOption(string description) =>
            new Option<string>(
                new string[] { "-f", "--framework" },
                description)
            {
                Argument = new Argument<string>(CommonLocalizableStrings.FrameworkArgumentName)
                    .AddSuggestions(Suggest.TargetFrameworksFromProjectFile())
            }.ForwardAsSingle(o => $"-property:TargetFramework={o}");

        public static Option RuntimeOption(string description, bool withShortOption = true) =>
            new Option<string>(
                withShortOption ? new string[] { "-r", "--runtime" } : new string[] { "--runtime" },
                description)
            {
                Argument = new Argument<string>(CommonLocalizableStrings.RuntimeIdentifierArgumentName)
                    .AddSuggestions(Suggest.RunTimesFromProjectFile())
            }.ForwardAsSingle(o => $"-property:RuntimeIdentifier={o}");

        public static Option CurrentRuntimeOption(string description) =>
            new Option<bool>("--use-current-runtime", description)
                .ForwardAs("-property:UseCurrentRuntimeIdentifier=True");

        public static Option ConfigurationOption(string description) =>
            new Option<string>(
                new string[] { "-c", "--configuration" },
                description)
            {
                Argument = new Argument<string>(CommonLocalizableStrings.ConfigurationArgumentName)
                    .AddSuggestions(Suggest.ConfigurationsFromProjectFileOrDefaults())
            }.ForwardAsSingle(o => $"-property:Configuration={o}");

        public static Option VersionSuffixOption() =>
            new Option<string>(
                "--version-suffix",
                CommonLocalizableStrings.CmdVersionSuffixDescription)
            {
                Argument = new Argument<string>(CommonLocalizableStrings.VersionSuffixArgumentName)
            }.ForwardAsSingle(o => $"-property:VersionSuffix={o}");

        public static Argument DefaultToCurrentDirectory(this Argument arg)
        {
            arg.SetDefaultValue(PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));
            return arg;
        }

        public static Option NoRestoreOption() =>
            new Option<bool>(
                "--no-restore",
                CommonLocalizableStrings.NoRestoreDescription);

        public static Option InteractiveMsBuildForwardOption() =>
            new Option<bool>(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription)
            .ForwardAs(Utils.Constants.MsBuildInteractiveOption);

        public static Option InteractiveOption() =>
            new Option<bool>(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription);

        public static Option DebugOption() => new Option<bool>("--debug");
    }

    public enum VerbosityOptions
    {
        quiet,
        q,
        minimal,
        m,
        normal,
        n,
        detailed,
        d,
        diagnostic,
        diag
    }
}
