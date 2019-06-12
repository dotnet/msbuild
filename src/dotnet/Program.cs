// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.Tools.Help;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using Command = Microsoft.DotNet.Cli.Utils.Command;
using RuntimeEnvironment = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment;
using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Cli
{
    public class Program
    {
        private static readonly string ToolPathSentinelFileName = $"{Product.Version}.toolpath.sentinel";

        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            new MulticoreJitActivator().TryActivateMulticoreJit();

            if (Env.GetEnvironmentVariableAsBool("DOTNET_CLI_CAPTURE_TIMING", false))
            {
                PerfTrace.Enabled = true;
            }

            InitializeProcess();

            try
            {
                using (PerfTrace.Current.CaptureTiming())
                {
                    return ProcessArgs(args);
                }
            }
            catch (HelpException e)
            {
                Reporter.Output.WriteLine(e.Message);
                return 0;
            }
            catch (Exception e) when (e.ShouldBeDisplayedAsError())
            {
                Reporter.Error.WriteLine(CommandContext.IsVerbose()
                    ? e.ToString().Red().Bold()
                    : e.Message.Red().Bold());

                var commandParsingException = e as CommandParsingException;
                if (commandParsingException != null)
                {
                    Reporter.Output.WriteLine(commandParsingException.HelpText);
                }

                return 1;
            }
            catch (Exception e) when (!e.ShouldBeDisplayedAsError())
            {
                // If telemetry object has not been initialized yet. It cannot be collected
                TelemetryEventEntry.SendFiltered(e);
                Reporter.Error.WriteLine(e.ToString().Red().Bold());

                return 1;
            }
            finally
            {
                if (PerfTrace.Enabled)
                {
                    Reporter.Output.WriteLine("Performance Summary:");
                    PerfTraceOutput.Print(Reporter.Output, PerfTrace.GetEvents());
                }
            }
        }

        internal static int ProcessArgs(string[] args, ITelemetry telemetryClient = null)
        {
            // CommandLineApplication is a bit restrictive, so we parse things ourselves here. Individual apps should use CLA.

            var success = true;
            var command = string.Empty;
            var lastArg = 0;
            TopLevelCommandParserResult topLevelCommandParserResult = TopLevelCommandParserResult.Empty;

            using (IFirstTimeUseNoticeSentinel disposableFirstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel())
            {
                IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel = disposableFirstTimeUseNoticeSentinel;
                IAspNetCertificateSentinel aspNetCertificateSentinel = new AspNetCertificateSentinel();
                IFileSentinel toolPathSentinel = new FileSentinel(
                    new FilePath(
                        Path.Combine(
                            CliFolderPathCalculator.DotnetUserProfileFolderPath,
                            ToolPathSentinelFileName)));

                for (; lastArg < args.Length; lastArg++)
                {
                    if (IsArg(args[lastArg], "d", "diagnostics"))
                    {
                        Environment.SetEnvironmentVariable(CommandContext.Variables.Verbose, bool.TrueString);
                        CommandContext.SetVerbose(true);
                    }
                    else if (IsArg(args[lastArg], "version"))
                    {
                        PrintVersion();
                        return 0;
                    }
                    else if (IsArg(args[lastArg], "info"))
                    {
                        PrintInfo();
                        return 0;
                    }
                    else if (IsArg(args[lastArg], "h", "help") ||
                             args[lastArg] == "-?" ||
                             args[lastArg] == "/?")
                    {
                        HelpCommand.PrintHelp();
                        return 0;
                    }
                    else if (args[lastArg].StartsWith("-", StringComparison.OrdinalIgnoreCase))
                    {
                        Reporter.Error.WriteLine($"Unknown option: {args[lastArg]}");
                        success = false;
                    }
                    else
                    {
                        // It's the command, and we're done!
                        command = args[lastArg];
                        if (string.IsNullOrEmpty(command))
                        {
                            command = "help";
                        }

                        var environmentProvider = new EnvironmentProvider();

                        bool generateAspNetCertificate =
                            environmentProvider.GetEnvironmentVariableAsBool("DOTNET_GENERATE_ASPNET_CERTIFICATE", defaultValue: true);
                        bool telemetryOptout =
                          environmentProvider.GetEnvironmentVariableAsBool("DOTNET_CLI_TELEMETRY_OPTOUT", defaultValue: false);
                        bool addGlobalToolsToPath =
                            environmentProvider.GetEnvironmentVariableAsBool("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", defaultValue: true);

                        ReportDotnetHomeUsage(environmentProvider);

                        topLevelCommandParserResult = new TopLevelCommandParserResult(command);
                        var hasSuperUserAccess = false;
                        if (IsDotnetBeingInvokedFromNativeInstaller(topLevelCommandParserResult))
                        {
                            aspNetCertificateSentinel = new NoOpAspNetCertificateSentinel();
                            firstTimeUseNoticeSentinel = new NoOpFirstTimeUseNoticeSentinel();
                            toolPathSentinel = new NoOpFileSentinel(exists: false);
                            hasSuperUserAccess = true;
                        }

                        var dotnetFirstRunConfiguration = new DotnetFirstRunConfiguration(
                            generateAspNetCertificate: generateAspNetCertificate,
                            telemetryOptout: telemetryOptout,
                            addGlobalToolsToPath: addGlobalToolsToPath);

                        ConfigureDotNetForFirstTimeUse(
                            firstTimeUseNoticeSentinel,
                            aspNetCertificateSentinel,
                            toolPathSentinel,
                            hasSuperUserAccess,
                            dotnetFirstRunConfiguration,
                            environmentProvider);

                        break;
                    }
                }
                if (!success)
                {
                    HelpCommand.PrintHelp();
                    return 1;
                }

                if (telemetryClient == null)
                {
                    telemetryClient = new Telemetry.Telemetry(firstTimeUseNoticeSentinel);
                }
                TelemetryEventEntry.Subscribe(telemetryClient.TrackEvent);
                TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
            }

            IEnumerable<string> appArgs =
                (lastArg + 1) >= args.Length
                ? Enumerable.Empty<string>()
                : args.Skip(lastArg + 1).ToArray();

            if (CommandContext.IsVerbose())
            {
                Console.WriteLine($"Telemetry is: {(telemetryClient.Enabled ? "Enabled" : "Disabled")}");
            }

            TelemetryEventEntry.SendFiltered(topLevelCommandParserResult);

            int exitCode;
            if (BuiltInCommandsCatalog.Commands.TryGetValue(topLevelCommandParserResult.Command, out var builtIn))
            {
                var parseResult = Parser.Instance.ParseFrom($"dotnet {topLevelCommandParserResult.Command}", appArgs.ToArray());
                if (!parseResult.Errors.Any())
                {
                    TelemetryEventEntry.SendFiltered(parseResult);
                }

                exitCode = builtIn.Command(appArgs.ToArray());
            }
            else
            {
                CommandResult result = CommandFactoryUsingResolver.Create(
                        "dotnet-" + topLevelCommandParserResult.Command,
                        appArgs,
                        FrameworkConstants.CommonFrameworks.NetStandardApp15)
                    .Execute();
                exitCode = result.ExitCode;
            }

            telemetryClient.Flush();
            return exitCode;
        }

        private static void ReportDotnetHomeUsage(IEnvironmentProvider provider)
        {
            var home = provider.GetEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName);
            if (string.IsNullOrEmpty(home))
            {
                return;
            }

            Reporter.Verbose.WriteLine(
                string.Format(
                    LocalizableStrings.DotnetCliHomeUsed,
                    home,
                    CliFolderPathCalculator.DotnetHomeVariableName));
        }

        private static bool IsDotnetBeingInvokedFromNativeInstaller(TopLevelCommandParserResult parseResult)
        {
            return parseResult.Command == "internal-reportinstallsuccess";
        }

        private static void ConfigureDotNetForFirstTimeUse(
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel,
            IAspNetCertificateSentinel aspNetCertificateSentinel,
            IFileSentinel toolPathSentinel,
            bool hasSuperUserAccess,
            DotnetFirstRunConfiguration dotnetFirstRunConfiguration,
            IEnvironmentProvider environmentProvider)
        {
            using (PerfTrace.Current.CaptureTiming())
            {
                var environmentPath = EnvironmentPathFactory.CreateEnvironmentPath(hasSuperUserAccess, environmentProvider);
                var commandFactory = new DotNetCommandFactory(alwaysRunOutOfProc: true);
                var aspnetCertificateGenerator = new AspNetCoreCertificateGenerator();
                var dotnetConfigurer = new DotnetFirstTimeUseConfigurer(
                    firstTimeUseNoticeSentinel,
                    aspNetCertificateSentinel,
                    aspnetCertificateGenerator,
                    toolPathSentinel,
                    dotnetFirstRunConfiguration,
                    Reporter.Output,
                    CliFolderPathCalculator.CliFallbackFolderPath,
                    environmentPath);

                dotnetConfigurer.Configure();
            }
        }

        private static void InitializeProcess()
        {
            // by default, .NET Core doesn't have all code pages needed for Console apps.
            // see the .NET Core Notes in https://msdn.microsoft.com/en-us/library/system.diagnostics.process(v=vs.110).aspx
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            UILanguageOverride.Setup();
        }

        internal static bool TryGetBuiltInCommand(string commandName, out BuiltInCommandMetadata builtInCommand)
        {
            return BuiltInCommandsCatalog.Commands.TryGetValue(commandName, out builtInCommand);
        }

        private static void PrintVersion()
        {
            Reporter.Output.WriteLine(Product.Version);
        }

        private static void PrintInfo()
        {
            DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
            var commitSha = versionFile.CommitSha ?? "N/A";
            Reporter.Output.WriteLine($"{LocalizableStrings.DotNetSdkInfoLabel}");
            Reporter.Output.WriteLine($" Version:   {Product.Version}");
            Reporter.Output.WriteLine($" Commit:    {commitSha}");
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine($"{LocalizableStrings.DotNetRuntimeInfoLabel}");
            Reporter.Output.WriteLine($" OS Name:     {RuntimeEnvironment.OperatingSystem}");
            Reporter.Output.WriteLine($" OS Version:  {RuntimeEnvironment.OperatingSystemVersion}");
            Reporter.Output.WriteLine($" OS Platform: {RuntimeEnvironment.OperatingSystemPlatform}");
            Reporter.Output.WriteLine($" RID:         {GetDisplayRid(versionFile)}");
            Reporter.Output.WriteLine($" Base Path:   {ApplicationEnvironment.ApplicationBasePath}");
        }

        private static bool IsArg(string candidate, string longName)
        {
            return IsArg(candidate, shortName: null, longName: longName);
        }

        private static bool IsArg(string candidate, string shortName, string longName)
        {
            return (shortName != null && candidate.Equals("-" + shortName, StringComparison.OrdinalIgnoreCase)) ||
                   (longName != null && candidate.Equals("--" + longName, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetDisplayRid(DotnetVersionFile versionFile)
        {
            FrameworkDependencyFile fxDepsFile = new FrameworkDependencyFile();

            string currentRid = RuntimeEnvironment.GetRuntimeIdentifier();

            // if the current RID isn't supported by the shared framework, display the RID the CLI was
            // built with instead, so the user knows which RID they should put in their "runtimes" section.
            return fxDepsFile.IsRuntimeSupported(currentRid) ?
                currentRid :
                versionFile.BuildRid;
        }
    }
}
