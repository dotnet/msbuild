// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Security;
using Microsoft.Win32;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class UILanguageOverride
    {
        internal const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);
        private const string VSLANG = nameof(VSLANG);
        private const string PreferredUILang = nameof(PreferredUILang);
        private static Encoding DefaultMultilingualEncoding = Encoding.UTF8; // We choose UTF8 as the default encoding as opposed to specific language encodings because it supports emojis & other chars in .NET.

        public static void Setup()
        {
            CultureInfo language = GetOverriddenUILanguage();
            if (language != null)
            {
                ApplyOverrideToCurrentProcess(language);
                FlowOverrideToChildProcesses(language);
            }

            if (
                !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.InvariantCultureIgnoreCase) &&
                CurrentPlatformIsWindowsAndOfficiallySupportsUTF8Encoding()
                )
            {
                Console.OutputEncoding = DefaultMultilingualEncoding;
                Console.InputEncoding = DefaultMultilingualEncoding; // Setting both encodings causes a change in the CHCP, making it so we dont need to P-Invoke ourselves.
                // If the InputEncoding is not set, the encoding will work in CMD but not in Powershell, as the raw CHCP page won't be changed.
            }
        }

        private static void ApplyOverrideToCurrentProcess(CultureInfo language)
        {
            CultureInfo.DefaultThreadCurrentUICulture = language;
            // We don't need to change CurrentUICulture, as it will be changed by DefaultThreadCurrentUICulture on NET Core (but not Framework) apps. 
        }

        private static void FlowOverrideToChildProcesses(CultureInfo language)
        {
            // Do not override any environment variables that are already set as we do not want to clobber a more granular setting with our global setting.
            SetIfNotAlreadySet(DOTNET_CLI_UI_LANGUAGE, language.Name);
            SetIfNotAlreadySet(VSLANG, language.LCID); // for tools following VS guidelines to just work in CLI
            SetIfNotAlreadySet(PreferredUILang, language.Name); // for C#/VB targets that pass $(PreferredUILang) to compiler
        }

        /// <summary>
        /// Look first at UI Language Overrides. (DOTNET_CLI_UI_LANGUAGE and VSLANG). Does NOT check System Locale or OS Display Language.
        /// </summary>
        /// <returns>The custom language that was set by the user.
        /// DOTNET_CLI_UI_LANGUAGE > VSLANG. Returns null if none are set.</returns>
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

        private static bool CurrentPlatformIsWindowsAndOfficiallySupportsUTF8Encoding()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.OSVersion.Version.Major >= 10) // UTF-8 is only officially supported on 10+.
            {
                return CurrentPlatformOfficiallySupportsUTF8Encoding();
            }
            return false;
        }

        private static bool CurrentPlatformOfficiallySupportsUTF8Encoding()
        {
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            try
            {
                using RegistryKey windowsVersionRegistry = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var buildNumber = windowsVersionRegistry.GetValue("CurrentBuildNumber").ToString();
                const int buildNumberThatOfficialySupportsUTF8 = 18363;
                return int.Parse(buildNumber) >= buildNumberThatOfficialySupportsUTF8 || ForceUniversalEncodingOptInEnabled();
            }
            catch (Exception ex) when (ex is SecurityException || ex is ObjectDisposedException)
            {
                // We don't want to break those in VS on older versions of Windows with a non-en language.
                // Allow those without registry permissions to force the encoding, however.
                return ForceUniversalEncodingOptInEnabled();
            }
        }

        private static bool ForceUniversalEncodingOptInEnabled()
        {
            return string.Equals(Environment.GetEnvironmentVariable("DOTNET_CLI_FORCE_UTF8_ENCODING"), "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
