// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.TemplateUpdates;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateSearch.Common.TemplateUpdate;

namespace dotnet_new3
{
    public class Program
    {
        private const string HostIdentifier = "dotnetcli-preview";
        private const string HostVersion = "v1.0.0";
        private const string CommandName = "new3";
        private const string LanguageOverrideEnvironmentVar = "DOTNET_CLI_UI_LANGUAGE";
        private const string VsLanguageOverrideEnvironmentVar = "VSLANG";
        private const string CompilerLanguageEnvironmentVar = "PreferredUILang";

        public static int Main(string[] args)
        {
            bool emitTimings = args.Any(x => string.Equals(x, "--debug:emit-timings", StringComparison.OrdinalIgnoreCase));
            bool debugTelemetry = args.Any(x => string.Equals(x, "--debug:emit-telemetry", StringComparison.OrdinalIgnoreCase));

            DefaultTemplateEngineHost host = CreateHost(emitTimings);

            bool debugAuthoring = args.Any(x => string.Equals(x, "--trace:authoring", StringComparison.OrdinalIgnoreCase));
            bool debugInstall = args.Any(x => string.Equals(x, "--trace:install", StringComparison.OrdinalIgnoreCase));
            if (debugAuthoring)
            {
                AddAuthoringLogger(host);
                AddInstallLogger(host);
            }
            else if (debugInstall)
            {
                AddInstallLogger(host);
            }

            return New3Command.Run(CommandName, host, new TelemetryLogger(null, debugTelemetry), FirstRun, args);
        }

        private static DefaultTemplateEngineHost CreateHost(bool emitTimings)
        {
            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", "C#" }
            };

            try
            {
                string versionString = Dotnet.Version().CaptureStdOut().Execute().StdOut;
                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    preferences["dotnet-cli-version"] = versionString.Trim();
                }
            }
            catch
            { }

            var builtIns = new AssemblyComponentCatalog(new[]
            {
                // for assembly: Microsoft.TemplateEngine.Orchestrator.RunnableProjects
                typeof(RunnableProjectGenerator).GetTypeInfo().Assembly,
                // for assembly: Microsoft.TemplateEngine.Edge
                typeof(NupkgInstallUnitDescriptorFactory).GetTypeInfo().Assembly,
                // for assembly: Microsoft.TemplateEngine.Cli
                typeof(DotnetRestorePostActionProcessor).GetTypeInfo().Assembly,
                // for assembly: Microsoft.TemplateSearch.Common
                typeof(NupkgUpdater).GetTypeInfo().Assembly
            });

            ConfigureLocale();

            DefaultTemplateEngineHost host = new DefaultTemplateEngineHost(HostIdentifier, HostVersion, preferences, builtIns, new[] { "dotnetcli" });

            if (emitTimings)
            {
                host.OnLogTiming = (label, duration, depth) =>
                {
                    string indent = string.Join("", Enumerable.Repeat("  ", depth));
                    Console.WriteLine($"{indent} {label} {duration.TotalMilliseconds}");
                };
            }

            return host;
        }

        private static void AddAuthoringLogger(DefaultTemplateEngineHost host)
        {
            Action<string, string[]> authoringLogger = (message, additionalInfo) =>
            {
                Console.WriteLine(string.Format("Authoring: {0}", message));
            };
            host.RegisterDiagnosticLogger("Authoring", authoringLogger);
        }

        private static void AddInstallLogger(DefaultTemplateEngineHost host)
        {
            Action<string, string[]> installLogger = (message, additionalInfo) =>
            {
                Console.WriteLine(string.Format("Install: {0}", message));
            };
            host.RegisterDiagnosticLogger("Install", installLogger);
        }

        private static void FirstRun(IEngineEnvironmentSettings environmentSettings)
        {
            Paths paths = new Paths(environmentSettings);

        }

        private static void ConfigureLocale()
        {
            CultureInfo? selectedCulture = null;

            // VSLANG=<lcid> is set by VS and we respect that as well so that we will respect the VS
            // language preference if we're invoked by VS.
            string? vsLang = Environment.GetEnvironmentVariable(VsLanguageOverrideEnvironmentVar);
            if (vsLang != null && int.TryParse(vsLang, out int vsLcid))
            {
                try
                {
                    selectedCulture = new CultureInfo(vsLcid);
                }
                catch (ArgumentOutOfRangeException) { }
                catch (CultureNotFoundException) { }
            }

            // DOTNET_CLI_UI_LANGUAGE=<culture name> is the main way for users to customize the CLI's UI language.
            // This overrides the locale set by VS.
            string? dotnetCliLanguage = Environment.GetEnvironmentVariable(LanguageOverrideEnvironmentVar);
            if (!string.IsNullOrWhiteSpace(dotnetCliLanguage))
            {
                try
                {
                    selectedCulture = new CultureInfo(dotnetCliLanguage);
                }
                catch (CultureNotFoundException)
                {
                    Console.WriteLine("Environment variable \"" + LanguageOverrideEnvironmentVar + "\" does not correspond to a valid culture." +
                        " Value is: \"" + dotnetCliLanguage + "\"");
                }
            }

            if (selectedCulture != null)
            {
                CultureInfo.DefaultThreadCurrentUICulture = selectedCulture;
                ConfigureLocaleForChildProcesses(selectedCulture);
            }
        }

        private static void ConfigureLocaleForChildProcesses(CultureInfo language)
        {
            // Set language for child dotnetcli processes.
            Environment.SetEnvironmentVariable(LanguageOverrideEnvironmentVar, language.Name);

            // Set language for tools following VS guidelines to just work in CLI.
            Environment.SetEnvironmentVariable(VsLanguageOverrideEnvironmentVar, language.LCID.ToString());

            // Set languatefor C#/VB targets that pass $(PreferredUILang) to compiler.
            Environment.SetEnvironmentVariable(CompilerLanguageEnvironmentVar, language.Name);
        }
    }
}
