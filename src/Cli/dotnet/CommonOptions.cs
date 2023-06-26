// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public static Option<string> ArtifactsPathOption =
            new ForwardedOption<string>(
                //  --artifacts-path is pretty verbose, should we use --artifacts instead (or possibly support both)?
                new string[] { "--artifacts-path" },
                description: CommonLocalizableStrings.ArtifactsPathOptionDescription)
            {
                ArgumentHelpName = CommonLocalizableStrings.ArtifactsPathArgumentName
            }.ForwardAsSingle(o => $"-property:ArtifactsPath={CommandDirectoryContext.GetFullPath(o)}");            

        private static string RuntimeArgName = CommonLocalizableStrings.RuntimeIdentifierArgumentName;
        public static IEnumerable<string> RuntimeArgFunc(string rid)
        {
            if (GetArchFromRid(rid) == "amd64")
            {
                rid = GetOsFromRid(rid) + "-x64";
            }
            return new string[] { $"-property:RuntimeIdentifier={rid}", "-property:_CommandLineDefinedRuntimeIdentifier=true" };
        }
        private static Func<CompletionContext, IEnumerable<CompletionItem>> RuntimeCompletions = Complete.RunTimesFromProjectFile;

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
            new ForwardedOption<bool>(
                new string[] { "--ucr", "--use-current-runtime" },
                description)
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

        public static Lazy<string> NormalizedCurrentDirectory = new Lazy<string>(() => PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));

        public static Argument<string> DefaultToCurrentDirectory(this Argument<string> arg)
        {
            // we set this lazily so that we don't pay the overhead of determining the
            // CWD multiple times, one for each Argument that uses this.
            arg.SetDefaultValueFactory(() => NormalizedCurrentDirectory.Value);
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
            {
                ArgumentHelpName = CommonLocalizableStrings.ArchArgumentName
            }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

        public static Option<string> LongFormArchitectureOption =
            new ForwardedOption<string>(
                new string[] { "--arch" },
                CommonLocalizableStrings.ArchitectureOptionDescription)
            {
                ArgumentHelpName = CommonLocalizableStrings.ArchArgumentName
            }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

        internal static string ArchOptionValue(ParseResult parseResult) =>
            string.IsNullOrEmpty(parseResult.GetValue(CommonOptions.ArchitectureOption)) ?
                parseResult.GetValue(CommonOptions.LongFormArchitectureOption) :
                parseResult.GetValue(CommonOptions.ArchitectureOption);

        public static Option<string> OperatingSystemOption =
            new ForwardedOption<string>(
                "--os",
                CommonLocalizableStrings.OperatingSystemOptionDescription)
            {
                ArgumentHelpName = CommonLocalizableStrings.OSArgumentName
            }.SetForwardingFunction(ResolveOsOptionToRuntimeIdentifier);

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

        public static readonly Option<string[]> TestLoggerOption = new Option<string[]>("--logger");

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
            arch = arch == "amd64" ? "x64" : arch;
            os = string.IsNullOrEmpty(os) ? GetOsFromRid(currentRid) : os;
            arch = string.IsNullOrEmpty(arch) ? GetArchFromRid(currentRid) : arch;
            return $"{os}-{arch}";
        }

        public static string GetCurrentRuntimeId()
        {
            // Get the dotnet directory, while ignoring custom msbuild resolvers
            string dotnetRootPath = Microsoft.DotNet.NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory(key =>
                key.Equals("DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR", StringComparison.InvariantCultureIgnoreCase)
                    ? null
                    : Environment.GetEnvironmentVariable(key));
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
