// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Tools;
using System.CommandLine;
using System.IO;
using Microsoft.DotNet.Tools.Common;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Cli
{
    internal static class CommonOptions
    {
        public static Option<string[]> PropertiesOption() =>
            new ForwardedOption<string[]>(new string[] { "-property", "/p" })
            {
                IsHidden = true
            }.ForwardAsProperty()
            .AllowSingleArgPerToken();

        public static Option<VerbosityOptions> VerbosityOption() =>
            VerbosityOption(o => $"-verbosity:{o}");

        public static Option<VerbosityOptions> VerbosityOption(Func<VerbosityOptions, string> format) =>
            new ForwardedOption<VerbosityOptions>(
                new string[] { "-v", "--verbosity" },
                description: CommonLocalizableStrings.VerbosityOptionDescription)
            {
                ArgumentHelpName = CommonLocalizableStrings.LevelArgumentName
            }.ForwardAsSingle(format);

        public static Option<string> FrameworkOption(string description) =>
            new ForwardedOption<string>(
                new string[] { "-f", "--framework" },
                description)
            {
                ArgumentHelpName = CommonLocalizableStrings.FrameworkArgumentName
                    
            }.ForwardAsSingle(o => $"-property:TargetFramework={o}")
            .AddSuggestions(Suggest.TargetFrameworksFromProjectFile());

        public static Option<string> RuntimeOption(string description, bool withShortOption = true) =>
            new ForwardedOption<string>(
                withShortOption ? new string[] { "-r", "--runtime" } : new string[] { "--runtime" },
                description)
            {
                ArgumentHelpName = CommonLocalizableStrings.RuntimeIdentifierArgumentName
            }.ForwardAsMany(o => new string[] { $"-property:RuntimeIdentifier={o}", "-property:_CommandLineDefinedRuntimeIdentifier=true" })
            .AddSuggestions(Suggest.RunTimesFromProjectFile());

        public static Option<bool> CurrentRuntimeOption(string description) =>
            new ForwardedOption<bool>("--use-current-runtime", description)
                .ForwardAs("-property:UseCurrentRuntimeIdentifier=True");

        public static Option<string> ConfigurationOption(string description) =>
            new ForwardedOption<string>(
                new string[] { "-c", "--configuration" },
                description)
            {
                ArgumentHelpName = CommonLocalizableStrings.ConfigurationArgumentName
            }.ForwardAsSingle(o => $"-property:Configuration={o}")
            .AddSuggestions(Suggest.ConfigurationsFromProjectFileOrDefaults());

        public static Option<string> VersionSuffixOption() =>
            new ForwardedOption<string>(
                "--version-suffix",
                CommonLocalizableStrings.CmdVersionSuffixDescription)
            {
                ArgumentHelpName = CommonLocalizableStrings.VersionSuffixArgumentName
            }.ForwardAsSingle(o => $"-property:VersionSuffix={o}");

        public static Argument<T> DefaultToCurrentDirectory<T>(this Argument<T> arg)
        {
            arg.SetDefaultValue(PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));
            return arg;
        }

        public static Option<bool> NoRestoreOption() =>
            new Option<bool>(
                "--no-restore",
                CommonLocalizableStrings.NoRestoreDescription);

        public static Option<bool> InteractiveMsBuildForwardOption() =>
            new ForwardedOption<bool>(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription)
            .ForwardAs("-property:NuGetInteractive=true");

        public static Option<bool> InteractiveOption() =>
            new Option<bool>(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription);

        public static Option<string> ArchitectureOption(bool includeShortVersion = true) =>
            new ForwardedOption<string>(
                includeShortVersion ? new string[] { "--arch", "-a" } : new string[] { "--arch" },
                CommonLocalizableStrings.ArchitectureOptionDescription)
            .SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

        public static Option<string> OperatingSystemOption() =>
            new ForwardedOption<string>(
                "--os",
                CommonLocalizableStrings.OperatingSystemOptionDescription)
            .SetForwardingFunction(ResolveOsOptionToRuntimeIdentifier);

        public static Option<bool> DebugOption() => new Option<bool>("--debug");

        public static Option<bool> SelfContainedOption() =>
            new ForwardedOption<bool>(
                "--self-contained",
                CommonLocalizableStrings.SelfContainedOptionDescription)
            .ForwardAsMany(o => new string[] { $"-property:SelfContained={o}", "-property:_CommandLineDefinedSelfContained=true" });

        public static Option<bool> NoSelfContainedOption() =>
            new ForwardedOption<bool>(
                "--no-self-contained",
                CommonLocalizableStrings.FrameworkDependentOptionDescription)
            .ForwardAsMany(o => new string[] { "-property:SelfContained=false", "-property:_CommandLineDefinedSelfContained=true" });

        public static bool VerbosityIsDetailedOrDiagnostic(this VerbosityOptions verbosity)
        {
            return verbosity.Equals(VerbosityOptions.diag) ||
                verbosity.Equals(VerbosityOptions.diagnostic) ||
                verbosity.Equals(VerbosityOptions.d) ||
                verbosity.Equals(VerbosityOptions.detailed);
        }

        public static void ValidateSelfContainedOptions(bool hasSelfContainedOption, bool hasNoSelfContainedOption)
        {
            if (hasSelfContainedOption && hasNoSelfContainedOption)
            {
                throw new GracefulException(CommonLocalizableStrings.SelfContainAndNoSelfContainedConflict);
            }
        }

        internal static IEnumerable<string> ResolveArchOptionToRuntimeIdentifier(string arg, ParseResult parseResult)
        {
            if (parseResult.HasOption(RuntimeOption(string.Empty).Aliases.First()))
            {
                throw new GracefulException(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndArchOptions);
            }

            if (parseResult.BothArchAndOsOptionsSpecified())
            {
                // ResolveOsOptionToRuntimeIdentifier handles resolving the RID when both arch and os are specified
                return Array.Empty<string>();
            }

            return ResolveRidShorthandOptions(null, arg);
        }

        internal static IEnumerable<string> ResolveOsOptionToRuntimeIdentifier(string arg, ParseResult parseResult)
        {
            if (parseResult.HasOption(RuntimeOption(string.Empty).Aliases.First()))
            {
                throw new GracefulException(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndOsOptions);
            }

            if (parseResult.BothArchAndOsOptionsSpecified())
            {
                return ResolveRidShorthandOptions(arg, parseResult.ValueForOption<string>(CommonOptions.ArchitectureOption().Aliases.First()));
            }

            return ResolveRidShorthandOptions(arg, null);
        }

        private static IEnumerable<string> ResolveRidShorthandOptions(string os, string arch)
        {
            var properties = new string[] { $"-property:RuntimeIdentifier={ResolveRidShorthandOptionsToRuntimeIdentifier(os, arch)}" };
            return properties;
        }

        internal static string ResolveRidShorthandOptionsToRuntimeIdentifier(string os, string arch)
        {
            var currentRid = GetCurrentRuntimeId();
            os = string.IsNullOrEmpty(os) ? GetOsFromRid(currentRid) : os;
            arch = string.IsNullOrEmpty(arch) ? GetArchFromRid(currentRid) : arch;
            return $"{os}-{arch}";
        }

        private static string GetCurrentRuntimeId()
        {
            var dotnetRootPath = Path.GetDirectoryName(Environment.ProcessPath);
            // When running under test the path does not always contain "dotnet" and Product.Version is empty.
            dotnetRootPath = Path.GetFileName(dotnetRootPath).Contains("dotnet") ? dotnetRootPath : Path.Combine(dotnetRootPath, "dotnet");
            var ridFileName = "NETCoreSdkRuntimeIdentifierChain.txt";
            string runtimeIdentifierChainPath = string.IsNullOrEmpty(Product.Version) ?
                Path.Combine(Directory.GetDirectories(Path.Combine(dotnetRootPath, "sdk"))[0], ridFileName) :
                Path.Combine(dotnetRootPath, "sdk", Product.Version, ridFileName);
            string[] currentRuntimeIdentifiers = File.Exists(runtimeIdentifierChainPath) ?
                File.ReadAllLines(runtimeIdentifierChainPath).Where(l => !string.IsNullOrEmpty(l)).ToArray() :
                new string[] { };
            if (currentRuntimeIdentifiers == null || !currentRuntimeIdentifiers.Any() || !currentRuntimeIdentifiers[0].Contains("-"))
            {
                throw new GracefulException(CommonLocalizableStrings.CannotResolveRuntimeIdentifier);
            }
            return currentRuntimeIdentifiers[0]; // First rid is the most specific (ex win-x64)
        }

        private static string GetOsFromRid(string rid) => rid.Substring(0, rid.LastIndexOf("-"));

        private static string GetArchFromRid(string rid) => rid.Substring(rid.LastIndexOf("-") + 1, rid.Length - rid.LastIndexOf("-") - 1);
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
