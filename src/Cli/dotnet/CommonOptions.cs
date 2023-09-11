// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using System.CommandLine;
using System.CommandLine.Completions;

namespace Microsoft.DotNet.Cli
{
    internal static class CommonOptions
    {
        public static CliOption<string[]> PropertiesOption =
            // these are all of the forms that the property switch can be understood by in MSBuild
            new ForwardedOption<string[]>("--property", "-property", "/property", "/p", "-p", "--p")
            {
                Hidden = true
            }.ForwardAsProperty()
            .AllowSingleArgPerToken();

        public static CliOption<VerbosityOptions> VerbosityOption =
            new ForwardedOption<VerbosityOptions>("--verbosity", "-v")
            {
                Description = CommonLocalizableStrings.VerbosityOptionDescription,
                HelpName = CommonLocalizableStrings.LevelArgumentName
            }.ForwardAsSingle(o => $"-verbosity:{o}");

        public static CliOption<VerbosityOptions> HiddenVerbosityOption =
            new ForwardedOption<VerbosityOptions>("--verbosity", "-v")
            {
                Description = CommonLocalizableStrings.VerbosityOptionDescription,
                HelpName = CommonLocalizableStrings.LevelArgumentName,
                Hidden = true
            }.ForwardAsSingle(o => $"-verbosity:{o}");

        public static CliOption<string> FrameworkOption(string description) =>
            new ForwardedOption<string>("--framework", "-f")
            {
                Description = description,
                HelpName = CommonLocalizableStrings.FrameworkArgumentName

            }.ForwardAsSingle(o => $"-property:TargetFramework={o}")
            .AddCompletions(Complete.TargetFrameworksFromProjectFile);

        public static CliOption<string> ArtifactsPathOption =
            new ForwardedOption<string>(
                //  --artifacts-path is pretty verbose, should we use --artifacts instead (or possibly support both)?
                "--artifacts-path")
            {
                Description = CommonLocalizableStrings.ArtifactsPathOptionDescription,
                HelpName = CommonLocalizableStrings.ArtifactsPathArgumentName
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

        public static CliOption<string> RuntimeOption =
            new ForwardedOption<string>("--runtime", "-r")
            {
                HelpName = RuntimeArgName
            }.ForwardAsMany(RuntimeArgFunc)
            .AddCompletions(Complete.RunTimesFromProjectFile);

        public static CliOption<string> LongFormRuntimeOption =
            new ForwardedOption<string>("--runtime")
            {
                HelpName = RuntimeArgName
            }.ForwardAsMany(RuntimeArgFunc)
            .AddCompletions(Complete.RunTimesFromProjectFile);

        public static CliOption<bool> CurrentRuntimeOption(string description) =>
            new ForwardedOption<bool>("--use-current-runtime", "--ucr")
            {
                Description = description
            }.ForwardAs("-property:UseCurrentRuntimeIdentifier=True");

        public static CliOption<string> ConfigurationOption(string description) =>
            new ForwardedOption<string>("--configuration", "-c")
            {
                Description = description,
                HelpName = CommonLocalizableStrings.ConfigurationArgumentName
            }.ForwardAsSingle(o => $"-property:Configuration={o}")
            .AddCompletions(Complete.ConfigurationsFromProjectFileOrDefaults);

        public static CliOption<string> VersionSuffixOption =
            new ForwardedOption<string>("--version-suffix")
            {
                Description = CommonLocalizableStrings.CmdVersionSuffixDescription,
                HelpName = CommonLocalizableStrings.VersionSuffixArgumentName
            }.ForwardAsSingle(o => $"-property:VersionSuffix={o}");

        public static Lazy<string> NormalizedCurrentDirectory = new Lazy<string>(() => PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));

        public static CliArgument<string> DefaultToCurrentDirectory(this CliArgument<string> arg)
        {
            // we set this lazily so that we don't pay the overhead of determining the
            // CWD multiple times, one for each Argument that uses this.
            arg.DefaultValueFactory = _ => NormalizedCurrentDirectory.Value;
            return arg;
        }

        public static CliOption<bool> NoRestoreOption =new("--no-restore")
            {
                Description = CommonLocalizableStrings.NoRestoreDescription
            };

        public static CliOption<bool> InteractiveMsBuildForwardOption =
            new ForwardedOption<bool>("--interactive")
            {
                Description = CommonLocalizableStrings.CommandInteractiveOptionDescription
            }.ForwardAs("-property:NuGetInteractive=true");

        public static CliOption<bool> InteractiveOption =
            new CliOption<bool>("--interactive")
            {
                Description = CommonLocalizableStrings.CommandInteractiveOptionDescription
            };

        public static CliOption<bool> DisableBuildServersOption =
            new ForwardedOption<bool>("--disable-build-servers")
            {
                Description = CommonLocalizableStrings.DisableBuildServersOptionDescription
            }
            .ForwardAsMany(_ => new string[] { "-p:UseRazorBuildServer=false", "-p:UseSharedCompilation=false", "/nodeReuse:false" });

        public static CliOption<string> ArchitectureOption =
            new ForwardedOption<string>("--arch", "-a")
            {
                Description = CommonLocalizableStrings.ArchitectureOptionDescription,
                HelpName = CommonLocalizableStrings.ArchArgumentName
            }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

        public static CliOption<string> LongFormArchitectureOption =
            new ForwardedOption<string>("--arch")
            {
                Description = CommonLocalizableStrings.ArchitectureOptionDescription,
                HelpName = CommonLocalizableStrings.ArchArgumentName
            }.SetForwardingFunction(ResolveArchOptionToRuntimeIdentifier);

        internal static string ArchOptionValue(ParseResult parseResult) =>
            string.IsNullOrEmpty(parseResult.GetValue(CommonOptions.ArchitectureOption)) ?
                parseResult.GetValue(CommonOptions.LongFormArchitectureOption) :
                parseResult.GetValue(CommonOptions.ArchitectureOption);

        public static CliOption<string> OperatingSystemOption =
            new ForwardedOption<string>("--os")
            {
                Description = CommonLocalizableStrings.OperatingSystemOptionDescription,
                HelpName = CommonLocalizableStrings.OSArgumentName
            }.SetForwardingFunction(ResolveOsOptionToRuntimeIdentifier);

        public static CliOption<bool> DebugOption = new CliOption<bool>("--debug");

        public static CliOption<bool> SelfContainedOption =
            new ForwardedOption<bool>("--self-contained", "--sc")
            {
                Description = CommonLocalizableStrings.SelfContainedOptionDescription
            }
            .SetForwardingFunction(ForwardSelfContainedOptions);

        public static CliOption<bool> NoSelfContainedOption =
            new ForwardedOption<bool>("--no-self-contained")
            {
                Description = CommonLocalizableStrings.FrameworkDependentOptionDescription
            }
            // Flip the argument so that if this option is specified we get selfcontained=false
            .SetForwardingFunction((arg, p) => ForwardSelfContainedOptions(!arg, p));

        public static readonly CliOption<string> TestPlatformOption = new CliOption<string>("--Platform");

        public static readonly CliOption<string> TestFrameworkOption = new CliOption<string>("--Framework");

        public static readonly CliOption<string[]> TestLoggerOption = new("--logger");

        public static void ValidateSelfContainedOptions(bool hasSelfContainedOption, bool hasNoSelfContainedOption)
        {
            if (hasSelfContainedOption && hasNoSelfContainedOption)
            {
                throw new GracefulException(CommonLocalizableStrings.SelfContainAndNoSelfContainedConflict);
            }
        }

        internal static IEnumerable<string> ResolveArchOptionToRuntimeIdentifier(string arg, ParseResult parseResult)
        {
            if ((parseResult.GetResult(RuntimeOption) ?? parseResult.GetResult(LongFormRuntimeOption)) is not null)
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
            if ((parseResult.GetResult(RuntimeOption) ?? parseResult.GetResult(LongFormRuntimeOption)) is not null)
            {
                throw new GracefulException(CommonLocalizableStrings.CannotSpecifyBothRuntimeAndOsOptions);
            }

            var arch = parseResult.BothArchAndOsOptionsSpecified() ? ArchOptionValue(parseResult) : null;
            return ResolveRidShorthandOptions(arg, arch);
        }

        private static IEnumerable<string> ResolveRidShorthandOptions(string os, string arch) =>
            new string[] { $"-property:RuntimeIdentifier={ResolveRidShorthandOptionsToRuntimeIdentifier(os, arch)}" };

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
            (parseResult.GetResult(RuntimeOption) ??
            parseResult.GetResult(LongFormRuntimeOption) ??
            parseResult.GetResult(ArchitectureOption) ??
            parseResult.GetResult(LongFormArchitectureOption) ??
            parseResult.GetResult(OperatingSystemOption)) is not null;

        internal static CliOption<T> AddCompletions<T>(this CliOption<T> option, Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
        {
            option.CompletionSources.Add(completionSource);
            return option;
        }

        internal static CliArgument<T> AddCompletions<T>(this CliArgument<T> argument, Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
        {
            argument.CompletionSources.Add(completionSource);
            return argument;
        }
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
