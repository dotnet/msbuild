// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace Microsoft.DotNet.Cli
{
    internal static class UILanguageOverride
    {
        private const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);
        private const string VSLANG = nameof(VSLANG);
        private const string PreferredUILang = nameof(PreferredUILang);

        public static void Setup()
        {
            CultureInfo language = GetOverriddenUILanguage();
            if (language != null)
            {
                ApplyOverrideToCurrentProcess(language);
                FlowOverrideToChildProcesses(language);
            }
        }

        private static void ApplyOverrideToCurrentProcess(CultureInfo language)
        {
            CultureInfo.DefaultThreadCurrentUICulture = language;
        }

        private static void FlowOverrideToChildProcesses(CultureInfo language)
        {
            // Do not override any environment variables that are already set as we do not want to clobber a more granular setting with our global setting.
            SetIfNotAlreadySet(DOTNET_CLI_UI_LANGUAGE, language.Name);
            SetIfNotAlreadySet(VSLANG, language.LCID); // for tools following VS guidelines to just work in CLI
            SetIfNotAlreadySet(PreferredUILang, language.Name); // for C#/VB targets that pass $(PreferredUILang) to compiler
        }

        private static CultureInfo GetOverriddenUILanguage()
        {
            // DOTNET_CLI_UI_LANGUAGE=<culture name> is the main way for users to customize the CLI's UI language.
            string dotnetCliLanguage = Environment.GetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE);
            if (dotnetCliLanguage != null)
            {
                try
                {
                    return new CultureInfo(dotnetCliLanguage);
                }
                catch (CultureNotFoundException) { }
            }

            // VSLANG=<lcid> is set by VS and we respect that as well so that we will respect the VS 
            // language preference if we're invoked by VS. 
            string vsLang = Environment.GetEnvironmentVariable(VSLANG);
            if (vsLang != null && int.TryParse(vsLang, out int vsLcid))
            {
                try
                {
                    return new CultureInfo(vsLcid);
                }
                catch (ArgumentOutOfRangeException) { }
                catch (CultureNotFoundException) { }
            }

            return null;
        }

        private static void SetIfNotAlreadySet(string environmentVariableName, string value)
        {
            string currentValue = Environment.GetEnvironmentVariable(environmentVariableName);
            if (currentValue == null)
            {
                Environment.SetEnvironmentVariable(environmentVariableName, value);
            }
        }

        private static void SetIfNotAlreadySet(string environmentVariableName, int value)
        {
            SetIfNotAlreadySet(environmentVariableName, value.ToString());
        }
    }
}
