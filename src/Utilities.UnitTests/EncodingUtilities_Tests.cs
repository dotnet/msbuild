// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class EncodingUtilities_Tests
    {
        /// <summary>
        /// Test the CanEncode method with and without ANSI characters to determine if they can be encoded
        /// in the current system encoding.
        /// </summary>
        [WindowsOnlyFact]
        public void CanEncodeTest()
        {
            var defaultEncoding = EncodingUtilities.CurrentSystemOemEncoding;

            string nonAnsiCharacters = "\u521B\u5EFA";
            string pathWithAnsiCharacters = @"c:\windows\system32\cmd.exe";

            EncodingUtilities.CanEncodeString(defaultEncoding.CodePage, nonAnsiCharacters).ShouldBeFalse();
            EncodingUtilities.CanEncodeString(defaultEncoding.CodePage, pathWithAnsiCharacters).ShouldBeTrue();
        }

        /// <summary>
        /// Test for bug where the MSBuild does not respect "en" CultureInfo
        /// </summary>
        [Theory]
        [InlineData("en", "en")]
        [InlineData("jp", "jp")]
        [InlineData("fr", "fr")]
        public void GetExternalOverriddenUILanguageIfSupportableWithEncoding_RespectsOverriddenLanguage(string inputLanguage, string expectedLanguage)
        {
            if (!EncodingUtilities.CurrentPlatformIsWindowsAndOfficiallySupportsUTF8Encoding())
            {
                return; // Do not run test to replicate the behaviour of the invoking method
            }
            const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);
            using TestEnvironment testEnvironment = TestEnvironment.Create();

            // Override the ui language by setting environment variable
            testEnvironment.SetEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, inputLanguage);

            EncodingUtilities.GetExternalOverriddenUILanguageIfSupportableWithEncoding().ShouldBeEquivalentTo(new CultureInfo(expectedLanguage));
        }

        [WindowsOnlyFact]
        public void BatchFileEncoding_EncodingSpecificationTrueEqualsAlways()
        {
            const string content = @"example";

            var alwaysEncoding = EncodingUtilities.BatchFileEncoding(content, EncodingUtilities.UseUtf8Always);
            EncodingUtilities.BatchFileEncoding(content, EncodingUtilities.UseUtf8True).ShouldBe(alwaysEncoding);
        }

#if NETFRAMEWORK
        [WindowsFullFrameworkOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void TrySetConsoleUtf8Encoding_RespectsChangeWave(bool disableWave)
        {
            using TestEnvironment env = TestEnvironment.Create();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disableWave ? ChangeWaves.Wave18_15.ToString() : null);
            ChangeWaves.ResetStateForTests();

            Encoding originalOutputEncoding = Console.OutputEncoding;
            Encoding originalInputEncoding = Console.InputEncoding;
            try
            {
                EncodingUtilities.TrySetConsoleUtf8Encoding();

                Console.OutputEncoding.CodePage.ShouldBe(Encoding.UTF8.CodePage);
                Console.InputEncoding.CodePage.ShouldBe(Encoding.UTF8.CodePage);
                if (disableWave)
                {
                    Console.InputEncoding.GetPreamble().ShouldNotBeEmpty();
                }
                else
                {
                    Console.InputEncoding.GetPreamble().ShouldBeEmpty();
                }
            }
            finally
            {
                Console.OutputEncoding = originalOutputEncoding;
                Console.InputEncoding = originalInputEncoding;
            }
        }

        [WindowsFullFrameworkOnlyFact]
        public void TryNormalizeUtf8ConsoleInputEncoding_StripsBomFromUtf8AndPreservesNonUtf8()
        {
            Encoding originalInputEncoding = Console.InputEncoding;
            try
            {
                Console.InputEncoding = Encoding.UTF8;
                Console.InputEncoding.GetPreamble().ShouldNotBeEmpty();

                EncodingUtilities.TryNormalizeUtf8ConsoleInputEncoding();

                Console.InputEncoding.CodePage.ShouldBe(Encoding.UTF8.CodePage);
                Console.InputEncoding.GetPreamble().ShouldBeEmpty();

                Console.InputEncoding = Encoding.GetEncoding(437);

                EncodingUtilities.TryNormalizeUtf8ConsoleInputEncoding();

                Console.InputEncoding.CodePage.ShouldBe(437);
            }
            finally
            {
                Console.InputEncoding = originalInputEncoding;
            }
        }
#endif
    }
}
