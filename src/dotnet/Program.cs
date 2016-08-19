// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.Tools.Build;
using Microsoft.DotNet.Tools.Compiler;
using Microsoft.DotNet.Tools.Compiler.Csc;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.Pack3;
using Microsoft.DotNet.Tools.Publish;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.DotNet.Tools.Restore3;
using Microsoft.DotNet.Tools.Run;
using Microsoft.DotNet.Tools.Test;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli
{
    public class Program
    {
        private static Dictionary<string, Func<string[], int>> s_builtIns = new Dictionary<string, Func<string[], int>>
        {
            ["build"] = BuildCommand.Run,
            ["compile-csc"] = CompileCscCommand.Run,
            ["help"] = HelpCommand.Run,
            ["new"] = NewCommand.Run,
            ["nuget"] = NuGetCommand.Run,
            ["pack"] = PackCommand.Run,
            ["publish"] = PublishCommand.Run,
            ["restore"] = RestoreCommand.Run,
            ["run"] = RunCommand.Run,
            ["test"] = TestCommand.Run,
            ["build3"] = Build3Command.Run,
            ["run3"] = Run3Command.Run,
            ["restore3"] = Restore3Command.Run,
            ["pack3"] = Pack3Command.Run,
        };

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
            catch (GracefulException e)
            {
                Reporter.Error.WriteLine(CommandContext.IsVerbose() ? e.ToString().Red().Bold() : e.Message.Red().Bold());

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

            bool? verbose = null;
            var success = true;
            var command = string.Empty;
            var lastArg = 0;
            using (INuGetCacheSentinel nugetCacheSentinel = new NuGetCacheSentinel())
            {
                for (; lastArg < args.Length; lastArg++)
                {
                    if (IsArg(args[lastArg], "v", "verbose"))
                    {
                        verbose = true;
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
                    else if (IsArg(args[lastArg], "h", "help"))
                    {
                        HelpCommand.PrintHelp();
                        return 0;
                    }
                    else if (args[lastArg].StartsWith("-"))
                    {
                        Reporter.Error.WriteLine($"Unknown option: {args[lastArg]}");
                        success = false;
                    }
                    else
                    {
                        ConfigureDotNetForFirstTimeUse(nugetCacheSentinel);

                        // It's the command, and we're done!
                        command = args[lastArg];
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
                    telemetryClient = new Telemetry(nugetCacheSentinel);
                }
            }

            var appArgs = (lastArg + 1) >= args.Length ? Enumerable.Empty<string>() : args.Skip(lastArg + 1).ToArray();

            if (verbose.HasValue)
            {
                Environment.SetEnvironmentVariable(CommandContext.Variables.Verbose, verbose.ToString());
                Console.WriteLine($"Telemetry is: {(telemetryClient.Enabled ? "Enabled" : "Disabled")}");
            }

            if (string.IsNullOrEmpty(command))
            {
                command = "help";
            }

            telemetryClient.TrackEvent(command, null, null);

            int exitCode;
            Func<string[], int> builtIn;
            if (s_builtIns.TryGetValue(command, out builtIn))
            {
                exitCode = builtIn(appArgs.ToArray());
            }
            else
            {
                CommandResult result = Command.Create("dotnet-" + command, appArgs, FrameworkConstants.CommonFrameworks.NetStandardApp15)
                    .Execute();
                exitCode = result.ExitCode;
            }

            return exitCode;

        }

        private static void ConfigureDotNetForFirstTimeUse(INuGetCacheSentinel nugetCacheSentinel)
        {
            using (PerfTrace.Current.CaptureTiming())
            {
                using (var nugetPackagesArchiver = new NuGetPackagesArchiver())
                {
                    var environmentProvider = new EnvironmentProvider();
                    var commandFactory = new DotNetCommandFactory();
                    var nugetCachePrimer = 
                        new NuGetCachePrimer(commandFactory, nugetPackagesArchiver, nugetCacheSentinel);
                    var dotnetConfigurer = new DotnetFirstTimeUseConfigurer(
                        nugetCachePrimer,
                        nugetCacheSentinel,
                        environmentProvider);

                    dotnetConfigurer.Configure();
                }
            }
        }

        private static void InitializeProcess()
        {
            // by default, .NET Core doesn't have all code pages needed for Console apps.
            // see the .NET Core Notes in https://msdn.microsoft.com/en-us/library/system.diagnostics.process(v=vs.110).aspx
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        internal static bool TryGetBuiltInCommand(string commandName, out Func<string[], int> builtInCommand)
        {
            return s_builtIns.TryGetValue(commandName, out builtInCommand);
        }

        private static void PrintVersion()
        {
            Reporter.Output.WriteLine(Product.Version);
        }

        private static void PrintInfo()
        {
            HelpCommand.PrintVersionHeader();

            var commitSha = GetCommitSha() ?? "N/A";
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine("Product Information:");
            Reporter.Output.WriteLine($" Version:            {Product.Version}");
            Reporter.Output.WriteLine($" Commit SHA-1 hash:  {commitSha}");
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine("Runtime Environment:");
            Reporter.Output.WriteLine($" OS Name:     {RuntimeEnvironment.OperatingSystem}");
            Reporter.Output.WriteLine($" OS Version:  {RuntimeEnvironment.OperatingSystemVersion}");
            Reporter.Output.WriteLine($" OS Platform: {RuntimeEnvironment.OperatingSystemPlatform}");
            Reporter.Output.WriteLine($" RID:         {RuntimeEnvironment.GetRuntimeIdentifier()}");
        }

        private static bool IsArg(string candidate, string longName)
        {
            return IsArg(candidate, shortName: null, longName: longName);
        }

        private static bool IsArg(string candidate, string shortName, string longName)
        {
            return (shortName != null && candidate.Equals("-" + shortName)) || (longName != null && candidate.Equals("--" + longName));
        }

        private static string GetCommitSha()
        {
            var versionFile = DotnetFiles.VersionFile;

            if (File.Exists(versionFile))
            {
                return File.ReadLines(versionFile).FirstOrDefault()?.Substring(0, 10);
            }

            return null;
        }
    }
}
