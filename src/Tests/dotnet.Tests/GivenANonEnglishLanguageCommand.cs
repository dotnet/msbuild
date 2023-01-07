// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Globalization;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli
{
    public class GivenANonEnglishLanguageCommand : SdkTest
    {
        public const string DOTNET_CLI_UI_LANGUAGE = nameof(DOTNET_CLI_UI_LANGUAGE);
        public const string VSLANG = nameof(VSLANG);
        public const string arbitraryLanguageWithNonEnglishCharset = "ja";

        public GivenANonEnglishLanguageCommand(ITestOutputHelper log) : base(log)
        {

        }

        [WindowsOnlyFact]
        public void GivenANonEnglishLanguageItUsesUTF8OnWindowsSoCharactersAreNotGarbled()
        {
#if NET5_0_OR_GREATER
            if (Environment.OSVersion.Version.Major >= 10)
            {
                var expectedCustomLanguageBehavior = new DotnetCommand(Log)
                    .WithEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, arbitraryLanguageWithNonEnglishCharset)
                    .Execute("--help")
                    .Should()
                    .HaveEncodingEquivalentTo(Encoding.UTF8);

                var expectedCustomVSLanguageBehavior = new DotnetCommand(Log)
                    .WithEnvironmentVariable(DOTNET_CLI_UI_LANGUAGE, arbitraryLanguageWithNonEnglishCharset)
                    .Execute("--help")
                    .Should()
                    .HaveEncodingEquivalentTo(Encoding.UTF8);

                // There is no test for the OS Language only outside of vendors.
                // (We would have to add another CI Machine for just this, or mock CultureInfo.)
            }
            // This test will fail if the OS is not set to us english.
            var expectedLegacyEnglishBehavior = new DotnetCommand(Log)
                .Execute("--help")
                .Should()
                .HaveEncodingEquivalentTo(Encoding.GetEncoding(437)); // 437 is the en-us charset.
#endif
        }
    }
}
