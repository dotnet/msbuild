// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.NET.TestFramework.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
{
    public class DotnetNewLocaleTests
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewLocaleTests(ITestOutputHelper log)
        {
            _log = log;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void TestDefaultLocale()
        {
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            var home = Helpers.CreateTemporaryFolder("Home");
            var thisDir = Path.GetDirectoryName(typeof(DotnetNewLocaleTests).Assembly.Location);
            var testTemplatesFolder = Path.Combine(
                thisDir ?? string.Empty,
                "..",
                "..",
                "..",
                "..",
                "..",
                "test",
                "Microsoft.TemplateEngine.TestTemplates",
                "test_templates",
                "TemplateWithLocalization");

            var commandResult = new DotnetNewCommand(_log, "-i", testTemplatesFolder, "--quiet")
                .WithWorkingDirectory(Helpers.CreateTemporaryFolder())
                .WithEnvironmentVariable(Helpers.HomeEnvironmentVariableName, home)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", string.Empty)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And
                .HaveStdOutMatching("name.*TestAssets.TemplateWithLocalization");
        }

        [Theory]
        [InlineData("en-US", "name")]
        [InlineData("de-DE", "name_de-DE")]
        [InlineData("tr-TR", "name_tr-TR")]
        public void TestDotnetCLIEnvVariable(string dotnetCliEnvVar, string expectedName)
        {
            var home = Helpers.CreateTemporaryFolder("Home");
            var thisDir = Path.GetDirectoryName(typeof(DotnetNewLocaleTests).Assembly.Location);
            var testTemplatesFolder = Path.Combine(
                thisDir ?? string.Empty,
                "..",
                "..",
                "..",
                "..",
                "..",
                "test",
                "Microsoft.TemplateEngine.TestTemplates",
                "test_templates",
                "TemplateWithLocalization");

            var commandResult = new DotnetNewCommand(_log, "-i", testTemplatesFolder, "--quiet")
                .WithWorkingDirectory(Helpers.CreateTemporaryFolder())
                .WithEnvironmentVariable(Helpers.HomeEnvironmentVariableName, home)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", dotnetCliEnvVar)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And
                .HaveStdOutMatching(Regex.Escape(expectedName) + ".*TestAssets.TemplateWithLocalization");
        }
    }
}
