// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BinlogRedactor.Reporting;
using Microsoft.Build.BinlogRedactor.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Build.BinlogRedactor.Commands
{
    internal static class CommonOptionsExtensions
    {
        private const VerbosityOptions DefaultConsoleVerbosity = VerbosityOptions.normal;

        internal static readonly Option<VerbosityOptions> s_consoleVerbosityOption = new(
            new string[] { "-v", "--verbosity" },
            () => DefaultConsoleVerbosity,
            "Sets the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], and diag[nostic].")
        {
            ArgumentHelpName = "LEVEL"
        };

        private static bool IsGlobalVerbose()
        {
            bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE"), out bool globalVerbose);
            return globalVerbose;
        }

        public static VerbosityOptions GetConsoleVerbosityOptionOrDefault(this ParseResult parseResult)
            => parseResult.GetVerbosityOption(s_consoleVerbosityOption) ?? (IsGlobalVerbose() ? VerbosityOptions.diagnostic : DefaultConsoleVerbosity);

        private static VerbosityOptions? GetVerbosityOption(this ParseResult parseResult, Option<VerbosityOptions> option)
        {
            OptionResult? verbosityOptionResult = parseResult.FindResultFor(option);
            VerbosityOptions? verbosity = null;

            if (verbosityOptionResult != null && !verbosityOptionResult.IsImplicit)
            {
                verbosity = verbosityOptionResult.GetValueOrDefault<VerbosityOptions>();
            }

            return verbosity;
        }
        internal static IHostBuilder AddCancellationTokenProvider(this IHostBuilder builder)
        {
            if (!builder.Properties.TryGetValue(typeof(InvocationContext), out object? val) ||
                val is not InvocationContext invocationContext)
            {
                throw new BinlogRedactorException("HostBuilder doesn't contain InvocationContext",
                    BinlogRedactorErrorCode.InternalError);
            }

            builder.ConfigureServices(services =>
                services.AddSingleton(new CancellationTokenHolder(invocationContext.GetCancellationToken())));

            return builder;
        }

        internal static ILoggingBuilder ConfigureBinlogRedactorLogging(this ILoggingBuilder logging, IHostBuilder host)
        {
            logging.ClearProviders();

            ParseResult parseResult = (host.Properties[typeof(InvocationContext)] as InvocationContext).ParseResult;

            var consoleLogLevel = parseResult.GetConsoleVerbosityOptionOrDefault().ToLogLevel();

            if (consoleLogLevel < LogLevel.None)
            {
                //logging.AddConsole();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                });
            }

            logging.SetMinimumLevel(consoleLogLevel);
            // get rid of chatty logs from system librarires
            logging.AddFilter("Microsoft", LogLevel.Warning);
            logging.AddFilter("Microsoft.Build.BinlogRedactor", consoleLogLevel);

            return logging;
        }

        internal static bool IsDetailedOrDiagnostic(this VerbosityOptions verbosity)
        {
            return verbosity.Equals(VerbosityOptions.diag) ||
                verbosity.Equals(VerbosityOptions.diagnostic) ||
                verbosity.Equals(VerbosityOptions.d) ||
                verbosity.Equals(VerbosityOptions.detailed);
        }

        internal static bool IsQuiet(this VerbosityOptions verbosity)
        {
            return verbosity.Equals(VerbosityOptions.q) ||
                verbosity.Equals(VerbosityOptions.quiet);
        }
        internal static bool IsMinimal(this VerbosityOptions verbosity)
        {
            return verbosity.Equals(VerbosityOptions.m) ||
                verbosity.Equals(VerbosityOptions.minimal);
        }
        internal static bool IsNormal(this VerbosityOptions verbosity)
        {
            return verbosity.Equals(VerbosityOptions.normal) ||
                verbosity.Equals(VerbosityOptions.n);
        }

        /// <summary>
        /// Converts <see cref="VerbosityOptions"/> to Microsoft.Extensions.Logging.<see cref="LogLevel"/>.
        /// </summary>
        internal static LogLevel ToLogLevel(this VerbosityOptions verbosityOptions)
        {
            LogLevel logLevel = LogLevel.Information;
            switch (verbosityOptions)
            {
                case VerbosityOptions.d:
                case VerbosityOptions.detailed:
                    logLevel = LogLevel.Debug;
                    break;
                case VerbosityOptions.diag:
                case VerbosityOptions.diagnostic:
                    logLevel = LogLevel.Trace;
                    break;
                case VerbosityOptions.m:
                case VerbosityOptions.minimal:
                    logLevel = LogLevel.Error;
                    break;
                case VerbosityOptions.n:
                case VerbosityOptions.normal:
                    logLevel = LogLevel.Information;
                    break;
                case VerbosityOptions.q:
                case VerbosityOptions.quiet:
                    logLevel = LogLevel.None;
                    break;
            }
            return logLevel;
        }

        internal static LogLevel? ToLogLevel(this VerbosityOptions? verbosityOptions) =>
            verbosityOptions?.ToLogLevel();
    }
}
