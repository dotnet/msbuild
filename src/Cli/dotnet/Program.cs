// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ShellShim;
using Microsoft.Extensions.EnvironmentAbstractions;
using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;
using NuGet.Frameworks;
using System.Linq;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Cli
{
    public class Program
    {
        private static readonly string ToolPathSentinelFileName = $"{Product.Version}.toolpath.sentinel";

        public static int Main(string[] args)
        {
            //setting output encoding is not available on those platforms
            if (!OperatingSystem.IsIOS() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsTvOS())
            {
                //if output is redirected, force encoding to utf-8;
                //otherwise the caller may not decode it correctly
                if (Console.IsOutputRedirected)
                {
                    Console.OutputEncoding = Encoding.UTF8;
                }
            }

            DebugHelper.HandleDebugSwitch(ref args);

            // Capture the current timestamp to calculate the host overhead.
            DateTime mainTimeStamp = DateTime.Now;
            TimeSpan startupTime = mainTimeStamp - Process.GetCurrentProcess().StartTime;

            bool perfLogEnabled = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_PERF_LOG", false);

            // Avoid create temp directory with root permission and later prevent access in non sudo
            if (SudoEnvironmentDirectoryOverride.IsRunningUnderSudo())
            {
                perfLogEnabled = false;
            }

            PerformanceLogStartupInformation startupInfo = null;
            if (perfLogEnabled)
            {
                startupInfo = new PerformanceLogStartupInformation(mainTimeStamp);
                PerformanceLogManager.InitializeAndStartCleanup(FileSystemWrapper.Default);
            }

            PerformanceLogEventListener perLogEventListener = null;
            try
            {
                if (perfLogEnabled)
                {
                    perLogEventListener = PerformanceLogEventListener.Create(FileSystemWrapper.Default, PerformanceLogManager.Instance.CurrentLogDirectory);
                }

                PerformanceLogEventSource.Log.LogStartUpInformation(startupInfo);
                PerformanceLogEventSource.Log.CLIStart();

                InitializeProcess();

                try
                {
                    return ProcessArgs(args, startupTime);
                }
                catch (Exception e) when (e.ShouldBeDisplayedAsError())
                {
                    Reporter.Error.WriteLine(CommandContext.IsVerbose()
                        ? e.ToString().Red().Bold()
                        : e.Message.Red().Bold());

                    var commandParsingException = e as CommandParsingException;
                    if (commandParsingException != null && commandParsingException.ParseResult != null)
                    {
                        commandParsingException.ParseResult.ShowHelp();
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
                    PerformanceLogEventSource.Log.CLIStop();
                }
            }
            finally
            {
                if(perLogEventListener != null)
                {
                    perLogEventListener.Dispose();
                }
            }
        }

        internal static int ProcessArgs(string[] args, ITelemetry telemetryClient = null )
        {
            return ProcessArgs(args, new TimeSpan(0), telemetryClient);
        }

        internal static int ProcessArgs(string[] args, TimeSpan startupTime, ITelemetry telemetryClient = null )
        {
            Dictionary<string, double> performanceData = new Dictionary<string, double>();

            PerformanceLogEventSource.Log.BuiltInCommandParserStart();
            Stopwatch parseStartTime = Stopwatch.StartNew();
            var parseResult = Parser.Instance.Parse(args);

            // Avoid create temp directory with root permission and later prevent access in non sudo
            // This method need to be run very early before temp folder get created
            // https://github.com/dotnet/sdk/issues/20195
            SudoEnvironmentDirectoryOverride.OverrideEnvironmentVariableToTmp(parseResult);

            performanceData.Add("Parse Time", parseStartTime.Elapsed.TotalMilliseconds);
            PerformanceLogEventSource.Log.BuiltInCommandParserStop();

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
                if (parseResult.GetValueForOption(Parser.DiagOption) && parseResult.IsDotnetBuiltInCommand())
                {
                    Environment.SetEnvironmentVariable(CommandContext.Variables.Verbose, bool.TrueString);
                    CommandContext.SetVerbose(true);
                    Reporter.Reset();
                }
                if (parseResult.HasOption(Parser.VersionOption) && parseResult.IsTopLevelDotnetCommand())
                {
                    CommandLineInfo.PrintVersion();
                    return 0;
                }
                else if (parseResult.HasOption(Parser.InfoOption) && parseResult.IsTopLevelDotnetCommand())
                {
                    CommandLineInfo.PrintInfo();
                    return 0;
                }
                else
                {
                    PerformanceLogEventSource.Log.FirstTimeConfigurationStart();

                    var environmentProvider = new EnvironmentProvider();

                    bool generateAspNetCertificate =
                        environmentProvider.GetEnvironmentVariableAsBool("DOTNET_GENERATE_ASPNET_CERTIFICATE", defaultValue: true);
                    bool telemetryOptout =
                      environmentProvider.GetEnvironmentVariableAsBool("DOTNET_CLI_TELEMETRY_OPTOUT", defaultValue: false);
                    bool addGlobalToolsToPath =
                        environmentProvider.GetEnvironmentVariableAsBool("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", defaultValue: true);
                    bool nologo =
                        environmentProvider.GetEnvironmentVariableAsBool("DOTNET_NOLOGO", defaultValue: false);

                    ReportDotnetHomeUsage(environmentProvider);

                    var isDotnetBeingInvokedFromNativeInstaller = false;
                    if (parseResult.CommandResult.Command.Name.Equals(Parser.InstallSuccessCommand.Name))
                    {
                        aspNetCertificateSentinel = new NoOpAspNetCertificateSentinel();
                        firstTimeUseNoticeSentinel = new NoOpFirstTimeUseNoticeSentinel();
                        toolPathSentinel = new NoOpFileSentinel(exists: false);
                        isDotnetBeingInvokedFromNativeInstaller = true;
                    }

                    var dotnetFirstRunConfiguration = new DotnetFirstRunConfiguration(
                        generateAspNetCertificate: generateAspNetCertificate,
                        telemetryOptout: telemetryOptout,
                        addGlobalToolsToPath: addGlobalToolsToPath,
                        nologo: nologo);

                    ConfigureDotNetForFirstTimeUse(
                        firstTimeUseNoticeSentinel,
                        aspNetCertificateSentinel,
                        toolPathSentinel,
                        isDotnetBeingInvokedFromNativeInstaller,
                        dotnetFirstRunConfiguration,
                        environmentProvider,
                        performanceData);
                    PerformanceLogEventSource.Log.FirstTimeConfigurationStop();
                }

                PerformanceLogEventSource.Log.TelemetryRegistrationStart();

                if (telemetryClient == null)
                {
                    telemetryClient = new Telemetry.Telemetry(firstTimeUseNoticeSentinel);
                }
                TelemetryEventEntry.Subscribe(telemetryClient.TrackEvent);
                TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);

                PerformanceLogEventSource.Log.TelemetryRegistrationStop();
            }

            if (CommandContext.IsVerbose())
            {
                Console.WriteLine($"Telemetry is: {(telemetryClient.Enabled ? "Enabled" : "Disabled")}");
            }
            PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStart();
            performanceData.Add("Startup Time", startupTime.TotalMilliseconds);
            TelemetryEventEntry.SendFiltered(Tuple.Create(parseResult, performanceData));
            PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStop();

            int exitCode;
            if (parseResult.CanBeInvoked())
            {
                PerformanceLogEventSource.Log.BuiltInCommandStart();
                exitCode = parseResult.Invoke();
                PerformanceLogEventSource.Log.BuiltInCommandStop();
            }
            else
            {
                PerformanceLogEventSource.Log.ExtensibleCommandResolverStart();
                var resolvedCommand = CommandFactoryUsingResolver.Create(
                        "dotnet-" + parseResult.GetValueForArgument(Parser.DotnetSubCommand),
                        args.GetSubArguments(),
                        FrameworkConstants.CommonFrameworks.NetStandardApp15);
                PerformanceLogEventSource.Log.ExtensibleCommandResolverStop();

                PerformanceLogEventSource.Log.ExtensibleCommandStart();
                var result = resolvedCommand.Execute();
                PerformanceLogEventSource.Log.ExtensibleCommandStop();

                exitCode = result.ExitCode;
            }

            PerformanceLogEventSource.Log.TelemetryClientFlushStart();
            telemetryClient.Flush();
            PerformanceLogEventSource.Log.TelemetryClientFlushStop();

            telemetryClient.Dispose();

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

        private static void ConfigureDotNetForFirstTimeUse(
           IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel,
           IAspNetCertificateSentinel aspNetCertificateSentinel,
           IFileSentinel toolPathSentinel,
           bool isDotnetBeingInvokedFromNativeInstaller,
           DotnetFirstRunConfiguration dotnetFirstRunConfiguration,
           IEnvironmentProvider environmentProvider,
           Dictionary<string, double> performanceMeasurements)
        {
            var environmentPath = EnvironmentPathFactory.CreateEnvironmentPath(isDotnetBeingInvokedFromNativeInstaller, environmentProvider);
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
                environmentPath,
                performanceMeasurements);

            dotnetConfigurer.Configure();

            if (isDotnetBeingInvokedFromNativeInstaller && OperatingSystem.IsWindows())
            {
                DotDefaultPathCorrector.Correct();
            }
        }

        private static void InitializeProcess()
        {
            // by default, .NET Core doesn't have all code pages needed for Console apps.
            // see the .NET Core Notes in https://docs.microsoft.com/dotnet/api/system.diagnostics.process#-notes
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            UILanguageOverride.Setup();
        }
    }
}
