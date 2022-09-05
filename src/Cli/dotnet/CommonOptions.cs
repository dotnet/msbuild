// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Cli
{
    internal static class CommonOptions
    {
        public static Option<string[]> PropertiesOption =
            // these are all of the forms that the property switch can be understood by in MSBuild
            new ForwardedOption<string[]>(new string[] { "--property", "-property", "/property", "/p", "-p", "--p" })
            {
                IsHidden = true
            }.ForwardAsProperty()
            .AllowSingleArgPerToken();
            
        public static Option<VerbosityOptions> VerbosityOption =
            new ForwardedOption<VerbosityOptions>(
                new string[] { "-v", "--verbosity" },
                description: CommonLocalizableStrings.VerbosityOptionDescription)
                {
                    ArgumentHelpName = CommonLocalizableStrings.LevelArgumentName
                }.ForwardAsSingle(o => $"-verbosity:{o}");

        public static Option<VerbosityOptions> HiddenVerbosityOption =
            new ForwardedOption<VerbosityOptions>(
                new string[] { "-v", "--verbosity" },
                description: CommonLocalizableStrings.VerbosityOptionDescription)
            {
                ArgumentHelpName = CommonLocalizableStrings.LevelArgumentName,
                IsHidden = true
            }.ForwardAsSingle(o => $"-verbosity:{o}");

        public static Option<string> FrameworkOption(string description) =>
            new ForwardedOption<string>(
                new string[] { "-f", "--framework" },
                description)
            {
                ArgumentHelpName = CommonLocalizableStrings.FrameworkArgumentName
                    
            }.ForwardAsSingle(o => $"-property:TargetFramework={o}")
            .AddCompletions(Complete.TargetFrameworksFromProjectFile);

        private static string RuntimeArgName = CommonLocalizableStrings.RuntimeIdentifierArgumentName;
        private static Func<string, IEnumerable<string>> RuntimeArgFunc = o => new string[] { $"-property:RuntimeIdentifier={o}", "-property:_CommandLineDefinedRuntimeIdentifier=true" };
        private static CompletionDelegate RuntimeCompletions = Complete.RunTimesFromProjectFile;
        
        public static Option<string> RuntimeOption = 
            new ForwardedOption<string>(
                new string[] { "-r", "--runtime" })
            {
                ArgumentHelpName = RuntimeArgName
            }.ForwardAsMany(RuntimeArgFunc)
            .AddCompletions(RuntimeCompletions);

        public static Option<string> LongFormRuntimeOption =
            new ForwardedOption<string>(
                new string[] { "--runtime" })
            {
                ArgumentHelpName = RuntimeArgName
            }.ForwardAsMany(RuntimeArgFunc)
            .AddCompletions(RuntimeCompletions);

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
            .AddCompletions(Complete.ConfigurationsFromProjectFileOrDefaults);

        public static Option<string> VersionSuffixOption =
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

        public static Option<bool> NoRestoreOption =
            new Option<bool>(
                "--no-restore",
                CommonLocalizableStrings.NoRestoreDescription);

        public static Option<bool> InteractiveMsBuildForwardOption =
            new ForwardedOption<bool>(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription)
            .ForwardAs("-property:NuGetInteractive=true");

        public static Option<bool> InteractiveOption =
            new Option<bool>(
                "--interactive",
                CommonLocalizableStrings.CommandInteractiveOptionDescription);

        public static Option<bool> DisableBuildServersOption =
            new ForwardedOption<bool>(
                "--disable-build-servers",
                CommonLocalizableStrings.DisableBuildServersOptionDescription)
            .ForwardAsMany(_ => new string[] { "-p:UseRazorBuildServer=false", "-p:UseSharedCompilation=false", "/nodeReuse:false" });

        public static Option<string> ArchitectureOption =
            new ForwardedOption<string>(
                new string[] { "--arch", "-a" },
                CommonLocalizableStrings.ArchitectureOptionDescription)
            .SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

        public static Option<string> LongFormArchitectureOption =
            new ForwardedOption<string>(
                new string[] { "--arch" },
                CommonLocalizableStrings.ArchitectureOptionDescription)
            .SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

        internal static string ArchOptionValue(ParseResult parseResult) =>
            string.IsNullOrEmpty(parseResult.GetValueForOption(CommonOptions.ArchitectureOption)) ?
                parseResult.GetValueForOption(CommonOptions.LongFormArchitectureOption) :
                parseResult.GetValueForOption(CommonOptions.ArchitectureOption);

        public static Option<string> OperatingSystemOption =
            new ForwardedOption<string>(
                "--os",
                CommonLocalizableStrings.OperatingSystemOptionDescription)
            .SetForwardingFunction(ResolveOsOptionToRuntimeIdentifier);

        public static Option<bool> DebugOption = new Option<bool>("--debug");

        public static Option<bool> SelfContainedOption =
            new ForwardedOption<bool>(
                new string[] { "--sc", "--self-contained" },
                CommonLocalizableStrings.SelfContainedOptionDescription)
            .SetForwardingFunction(ForwardSelfContainedOptions);

        public static Option<bool> NoSelfContainedOption =
            new ForwardedOption<bool>(
                "--no-self-contained",
                CommonLocalizableStrings.FrameworkDependentOptionDescription)
            // Flip the argument so that if this option is specified we get selfcontained=false
            .SetForwardingFunction((arg, p) => ForwardSelfContainedOptions(!arg, p)); 

        public static readonly Option<string> TestPlatformOption = new Option<string>("--Platform");

        public static readonly Option<string> TestFrameworkOption = new Option<string>("--Framework");

        public static readonly Option<string> TestLoggerOption = new Option<string>("--logger");

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
            if (parseResult.HasOption(RuntimeOption) || parseResult.HasOption(LongFormRuntimeOption))
            {
                throw new GracefulException(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndArchOptions);
            }

            if (parseResult.BothArchAndOsOptionsSpecified())
            {
                // ResolveOsOptionToRuntimeIdentifier handles resolving the RID when both arch and os are specified
                return Array.Empty<string>();
            }
            
            var selfContainedSpecified = parseResult.HasOption(SelfContainedOption) || parseResult.HasOption(NoSelfContainedOption);
            return ResolveRidShorthandOptions(null, arg, selfContainedSpecified);
        }

        internal static IEnumerable<string> ResolveOsOptionToRuntimeIdentifier(string arg, ParseResult parseResult)
        {
            if (parseResult.HasOption(RuntimeOption) || parseResult.HasOption(LongFormRuntimeOption))
            {
                throw new GracefulException(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndOsOptions);
            }

            var selfContainedSpecified = parseResult.HasOption(SelfContainedOption) || parseResult.HasOption(NoSelfContainedOption);
            if (parseResult.BothArchAndOsOptionsSpecified())
            {
                return ResolveRidShorthandOptions(arg, ArchOptionValue(parseResult), selfContainedSpecified);
            }

            return ResolveRidShorthandOptions(arg, null, selfContainedSpecified);
        }

        private static IEnumerable<string> ResolveRidShorthandOptions(string os, string arch, bool userSpecifiedSelfContainedOption)
        {
            var properties = new string[] { $"-property:RuntimeIdentifier={ResolveRidShorthandOptionsToRuntimeIdentifier(os, arch)}" };
            if (!userSpecifiedSelfContainedOption)
            {
                properties = properties.Append("-property:SelfContained=false").ToArray();
            }
            return properties;
        }

        internal static string ResolveRidShorthandOptionsToRuntimeIdentifier(string os, string arch)
        {
            var currentRid = GetCurrentRuntimeId();
            os = string.IsNullOrEmpty(os) ? GetOsFromRid(currentRid) : os;
            arch = string.IsNullOrEmpty(arch) ? GetArchFromRid(currentRid) : arch;
            return $"{os}-{arch}";
        }

        public static string GetDotnetExeDirectory()
        {
            // Alternatively we could use Microsoft.DotNet.NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory here
            //  (while injecting env resolver so that DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR is being returned as null)
            // However - it first looks on PATH - which can be problematic in environment (e.g. dev) where we have installed and xcopy dotnet versions

            var dotnetRootPath = Path.GetDirectoryName(Environment.ProcessPath);
            // When running under test the path does not always contain "dotnet".
			// The sdk folder is /d/ when run on helix because of space issues
            dotnetRootPath = Path.GetFileName(dotnetRootPath).Contains("dotnet") || Path.GetFileName(dotnetRootPath).Contains("x64") || Path.GetFileName(dotnetRootPath).Equals("d") ? dotnetRootPath : Path.Combine(dotnetRootPath, "dotnet");
            return dotnetRootPath;
        }

        public static string GetCurrentRuntimeId()
        {
            var dotnetRootPath = GetDotnetExeDirectory();
            var ridFileName = "NETCoreSdkRuntimeIdentifierChain.txt";
            // When running under test the Product.Version might be empty or point to version not installed in dotnetRootPath.
            string runtimeIdentifierChainPath = string.IsNullOrEmpty(Product.Version) || !Directory.Exists(Path.Combine(dotnetRootPath, "sdk", Product.Version)) ?
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

        private static IEnumerable<string> ForwardSelfContainedOptions(bool isSelfContained, ParseResult parseResult)
        {
            IEnumerable<string> selfContainedProperties = new string[] { $"-property:SelfContained={isSelfContained}", "-property:_CommandLineDefinedSelfContained=true" };
            
            if (!UserSpecifiedRidOption(parseResult) && isSelfContained)
            {
                var ridProperties = RuntimeArgFunc(GetCurrentRuntimeId());
                selfContainedProperties = selfContainedProperties.Concat(ridProperties);
            }
            
            return selfContainedProperties;
        }

        private static bool UserSpecifiedRidOption(ParseResult parseResult) =>
            parseResult.HasOption(RuntimeOption) ||
            parseResult.HasOption(LongFormRuntimeOption) ||
            parseResult.HasOption(ArchitectureOption) ||
            parseResult.HasOption(LongFormArchitectureOption) ||
            parseResult.HasOption(OperatingSystemOption);
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
