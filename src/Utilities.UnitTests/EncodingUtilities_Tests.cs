// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
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
    }
}
