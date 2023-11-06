// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Win32;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for dealing with encoding.
    /// </summary>
    internal static class EncodingUtilities
    {
        internal static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static Encoding s_currentOemEncoding;

        internal const string UseUtf8Always = "ALWAYS";
        internal const string UseUtf8Never = "NEVER";
        internal const string UseUtf8Detect = "DETECT";
        internal const string UseUtf8System = "SYSTEM";

        /// <summary>
        /// Get the current system locale code page, OEM version. OEM code pages are used for console-based input/output
        /// for historical reasons.
        /// </summary>
        internal static Encoding CurrentSystemOemEncoding
        {
            get
            {
                // if we already have it, no need to do it again
                if (s_currentOemEncoding != null)
                {
                    return s_currentOemEncoding;
                }

                // fall back to default ANSI encoding if we have problems
#if FEATURE_ENCODING_DEFAULT
                s_currentOemEncoding = Encoding.Default;
#else
                s_currentOemEncoding = Encoding.UTF8;
#endif

                try
                {
                    if (NativeMethods.IsWindows)
                    {
#if RUNTIME_TYPE_NETCORE
                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
                        // get the current OEM code page
                        s_currentOemEncoding = Encoding.GetEncoding(NativeMethods.GetOEMCP());
                    }
                }
                // theoretically, GetEncoding may throw an ArgumentException or a NotSupportedException. This should never
                // really happen, since the code page we pass in has just been returned from the "underlying platform",
                // so it really should support it. If it ever happens, we'll just fall back to the default encoding.
                // No point in showing any errors to the users, since they most likely wouldn't be actionable.
                catch (ArgumentException ex)
                {
                    Debug.Assert(false, "GetEncoding(default OEM encoding) threw an ArgumentException in EncodingUtilities.CurrentSystemOemEncoding! Please log a bug against MSBuild.", ex.Message);
                }
                catch (NotSupportedException ex)
                {
                    Debug.Assert(false, "GetEncoding(default OEM encoding) threw a NotSupportedException in EncodingUtilities.CurrentSystemOemEncoding! Please log a bug against MSBuild.", ex.Message);
                }

                return s_currentOemEncoding;
            }
        }

        /// <summary>
        /// Checks two encoding types to determine if they are similar to each other (equal or if
        /// the Encoding Name is the same).
        /// </summary>
        /// <param name="encoding1"></param>
        /// <param name="encoding2"></param>
        /// <returns>True if the two Encoding objects are equal or similar.</returns>
        internal static bool SimilarToEncoding(this Encoding encoding1, Encoding encoding2)
        {
            if (encoding1 == null)
            {
                return encoding2 == null;
            }

            if (encoding2 == null)
            {
                return false;
            }

            if (Equals(encoding1, encoding2))
            {
                return true;
            }

            return encoding1.EncodingName == encoding2.EncodingName;
        }

        /// <summary>
        /// Check if an encoding type is UTF8 (with or without BOM).
        /// </summary>
        /// <param name="encoding"></param>
        /// <returns>True if the encoding is UTF8.</returns>
        internal static bool IsUtf8Encoding(this Encoding encoding)
        {
            return SimilarToEncoding(encoding, Encoding.UTF8);
        }

        /// <summary>
        /// Check the first 3 bytes of a stream to determine if it matches the UTF8 preamble.
        /// </summary>
        /// <param name="stream">Steam to check.</param>
        /// <returns>True when the first 3 bytes of the Stream are equal to the UTF8 preamble (BOM).</returns>
        internal static bool StartsWithPreamble(this Stream stream)
        {
            return StartsWithPreamble(stream, Encoding.UTF8.GetPreamble());
        }

        /// <summary>
        /// Check the first 3 bytes of a stream to determine if it matches the given preamble.
        /// </summary>
        /// <param name="stream">Steam to check.</param>
        /// <param name="preamble">Preamble to look for.</param>
        /// <returns>True when the first 3 bytes of the Stream are equal to the preamble.</returns>
        internal static bool StartsWithPreamble(this Stream stream, byte[] preamble)
        {
            if (preamble == null)
            {
                return false;
            }

            int bytesRead;
            var buffer = new byte[preamble.Length];

            var position = stream.Position;
            if (stream.Position != 0)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            try
            {
                bytesRead = stream.Read(buffer, 0, preamble.Length);
            }
            finally
            {
                stream.Seek(position, SeekOrigin.Begin);
            }

            // Bytes read and preamble must be the same length and contain the same not contain any differences
            return bytesRead == preamble.Length && !buffer.Where((t, i) => preamble[i] != t).Any();
        }

        /// <summary>
        /// Check the first 3 bytes of a file to determine if it matches the 3-byte UTF8 preamble (BOM).
        /// </summary>
        /// <param name="file">Path to file to check.</param>
        /// <returns>True when the first 3 bytes of the file are equal to the UTF8 BOM.</returns>
        internal static bool FileStartsWithPreamble(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return StartsWithPreamble(stream);
            }
        }

        /// <summary>
        /// Checks to see if a string can be encoded in a specified code page.
        /// </summary>
        /// <remarks>Internal for testing purposes.</remarks>
        /// <param name="codePage">Code page for encoding.</param>
        /// <param name="stringToEncode">String to encode.</param>
        /// <returns>True if the string can be encoded in the specified code page.</returns>
        internal static bool CanEncodeString(int codePage, string stringToEncode)
        {
            // We have a System.String that contains some characters. Get a lossless representation
            // in byte-array form.
            var unicodeEncoding = new UnicodeEncoding();
            var unicodeBytes = unicodeEncoding.GetBytes(stringToEncode);

            // Create an Encoding using the desired code page, but throws if there's a
            // character that can't be represented.
            var systemEncoding = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);

            try
            {
                var oemBytes = Encoding.Convert(unicodeEncoding, systemEncoding, unicodeBytes);

                // If Convert didn't throw, we can represent everything in the desired encoding.
                return true;
            }
            catch (EncoderFallbackException)
            {
                // If a fallback encoding was attempted, we need to go to Unicode.
                return false;
            }
        }

        /// <summary>
        /// Find the encoding for the batch file.
        /// </summary>
        /// <remarks>
        /// The "best" encoding is the current OEM encoding, unless it's not capable of representing
        /// the characters we plan to put in the file. If it isn't, we can fall back to UTF-8.
        ///
        /// Why not always UTF-8? Because tools don't always handle it well. See
        /// https://github.com/dotnet/msbuild/issues/397
        /// </remarks>
        internal static Encoding BatchFileEncoding(string contents, string encodingSpecification)
        {
            if (!NativeMethods.IsWindows)
            {
                return EncodingUtilities.Utf8WithoutBom;
            }

            var defaultEncoding = EncodingUtilities.CurrentSystemOemEncoding;

            // When Windows is configured to use UTF-8 by default, the above returns
            // a UTF-8-with-BOM encoding, which cmd.exe can't interpret. Force the no-BOM
            // encoding if the returned encoding would have emitted one (preamble is nonempty).
            // See https://github.com/dotnet/msbuild/issues/4268
            if (defaultEncoding is UTF8Encoding e && e.GetPreamble().Length > 0)
            {
                defaultEncoding = EncodingUtilities.Utf8WithoutBom;
            }

            string useUtf8 = string.IsNullOrEmpty(encodingSpecification) ? EncodingUtilities.UseUtf8Detect : encodingSpecification;

            switch (useUtf8.ToUpperInvariant())
            {
                case EncodingUtilities.UseUtf8Always:
                    return EncodingUtilities.Utf8WithoutBom;
                case EncodingUtilities.UseUtf8Never:
                case EncodingUtilities.UseUtf8System:
                    return defaultEncoding;
                default:
                    return EncodingUtilities.CanEncodeString(defaultEncoding.CodePage, contents)
                        ? defaultEncoding
                        : EncodingUtilities.Utf8WithoutBom;
            }
        }
#nullable enable
        /// <summary>
        /// The .NET SDK and Visual Studio both have environment variables that set a custom language. MSBuild should respect the SDK variable.
        /// To use the corresponding UI culture, in certain cases the console encoding must be changed. This function will change the encoding in these cases.
        /// This code introduces a breaking change in .NET 8 due to the encoding of the console being changed.
        /// If the environment variables are undefined, this function should be a no-op.
        /// </summary>
        /// <returns>
        /// The custom language that was set by the user for an 'external' tool besides MSBuild.
        /// Returns <see langword="null"/> if none are set.
        /// </returns>
        public static CultureInfo? GetExternalOverriddenUILanguageIfSupportableWithEncoding()
        {
            if (!ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_8))
            {
                return null;
            }

            CultureInfo? externalLanguageSetting = GetExternalOverriddenUILanguage();
            if (externalLanguageSetting != null)
            {
                if (CurrentPlatformIsWindowsAndOfficiallySupportsUTF8Encoding())
                {
                    // Setting both encodings causes a change in the CHCP, making it so we don't need to P-Invoke CHCP ourselves.
                    Console.OutputEncoding = Encoding.UTF8;
                    // If the InputEncoding is not set, the encoding will work in CMD but not in PowerShell, as the raw CHCP page won't be changed.
                    Console.InputEncoding = Encoding.UTF8;
                    return externalLanguageSetting;
                }
                else if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return externalLanguageSetting;
                }
            }

            return null;
        }

        public static bool CurrentPlatformIsWindowsAndOfficiallySupportsUTF8Encoding()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.OSVersion.Version.Major >= 10) // UTF-8 is only officially supported on 10+.
            {
                try
                {
                    using RegistryKey? windowsVersionRegistry = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                    string? buildNumber = windowsVersionRegistry?.GetValue("CurrentBuildNumber")?.ToString();
                    const int buildNumberThatOfficiallySupportsUTF8 = 18363;
                    return buildNumber != null && (int.Parse(buildNumber) >= buildNumberThatOfficiallySupportsUTF8 || ForceUniversalEncodingOptInEnabled());
                }
                catch (Exception ex) when (ex is SecurityException or ObjectDisposedException)
                {
                    // We don't want to break those in VS on older versions of Windows with a non-en language.
                    // Allow those without registry permissions to force the encoding, however.
                    return ForceUniversalEncodingOptInEnabled();
                }
            }

            return false;
        }

        private static bool ForceUniversalEncodingOptInEnabled()
        {
            return string.Equals(Environment.GetEnvironmentVariable("DOTNET_CLI_FORCE_UTF8_ENCODING"), "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Look at UI language overrides that can be set by known external invokers. (DOTNET_CLI_UI_LANGUAGE.)
        /// Does NOT check System Locale or OS Display Language.
        /// Ported from the .NET SDK: https://github.com/dotnet/sdk/blob/bcea1face15458814b8e53e8785b52ba464f6538/src/Cli/Microsoft.DotNet.Cli.Utils/UILanguageOverride.cs
        /// </summary>
        /// <returns>The custom language that was set by the user for an 'external' tool besides MSBuild.
        /// Returns null if none are set.</returns>
        private static CultureInfo? GetExternalOverriddenUILanguage()
        {
            // DOTNET_CLI_UI_LANGUAGE=<culture name> is the main way for users to customize the CLI's UI language via the .NET SDK.
            string? dotnetCliLanguage = Environment.GetEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE");
            if (dotnetCliLanguage != null)
            {
                try
                {
                    return new CultureInfo(dotnetCliLanguage);
                }
                catch (CultureNotFoundException) { }
            }

            return null;
        }
    }
}

