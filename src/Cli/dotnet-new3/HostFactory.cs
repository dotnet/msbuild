// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli;

namespace Dotnet_new3
{
    internal class HostFactory
    {
        private const string HostIdentifier = "dotnetcli-preview";
        private const string HostVersion = "v2.0.0";
        private const string LanguageOverrideEnvironmentVar = "DOTNET_CLI_UI_LANGUAGE";
        private const string VsLanguageOverrideEnvironmentVar = "VSLANG";
        private const string CompilerLanguageEnvironmentVar = "PreferredUILang";

        internal static CliTemplateEngineHost CreateHost(bool disableSdkTemplates, string? outputPath)
        {
            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", "C#" }
            };

            try
            {
                string? versionString = GetCLIVersion();
                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    preferences["dotnet-cli-version"] = versionString.Trim();
                }
            }
            catch
            { }

            // Keep this in sync with dotnet/sdk repo
            var builtIns = new List<(Type, IIdentifiedComponent)>();
            builtIns.AddRange(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateEngine.Edge.Components.AllComponents);
            builtIns.AddRange(Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateSearch.Common.Components.AllComponents);
            if (!disableSdkTemplates)
            {
                builtIns.Add((typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackagesProviderFactory()));
            }

            ConfigureLocale();

            var host = new CliTemplateEngineHost(
                HostIdentifier,
                HostVersion,
                preferences,
                builtIns,
                new[] { "dotnetcli" },
                outputPath);

            return host;
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
            // Set language for child dotnet CLI processes.
            Environment.SetEnvironmentVariable(LanguageOverrideEnvironmentVar, language.Name);

            // Set language for tools following VS guidelines to just work in CLI.
            Environment.SetEnvironmentVariable(VsLanguageOverrideEnvironmentVar, language.LCID.ToString());

            // Set language for C#/VB targets that pass $(PreferredUILang) to compiler.
            Environment.SetEnvironmentVariable(CompilerLanguageEnvironmentVar, language.Name);
        }

        private static string? GetCLIVersion()
        {
            Dotnet versionCommand = Dotnet.Version();
            versionCommand.CaptureStdOut();

            Dotnet.Result result = versionCommand.Execute();
            if (result.ExitCode != 0)
            {
                return null;
            }

            return result.StdOut?.ToString();
        }
    }
}
