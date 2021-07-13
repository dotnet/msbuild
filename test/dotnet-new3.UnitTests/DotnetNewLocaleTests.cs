// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
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

            var home = TestUtils.CreateTemporaryFolder("Home");
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

            var commandResult = new DotnetNewCommand(_log, "-i", testTemplatesFolder)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            var home = TestUtils.CreateTemporaryFolder("Home");
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

            var commandResult = new DotnetNewCommand(_log, "-i", testTemplatesFolder)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", dotnetCliEnvVar)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And
                .HaveStdOutMatching(Regex.Escape(expectedName) + ".*TestAssets.TemplateWithLocalization");
        }

        [Fact]
        public void SkipsLocalizationOnInstall_WhenInvalidFormat()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            var workingDir = TestUtils.CreateTemporaryFolder("Home");
            var testTemplateLocation = TestUtils.GetTestTemplateLocation("Invalid/Localization/InvalidFormat");
            new DotnetNewCommand(_log, "-i", testTemplateLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Warning: Failed to read parse localization file").And.HaveStdOutContaining("localize/templatestrings.de-DE.json")
                .And.HaveStdOutContaining($"Success: {testTemplateLocation} installed the following templates:").And.HaveStdOutContaining("TestAssets.Invalid.Localization.InvalidFormat")
                .And.HaveStdOutContaining("name in base configuration");
        }

        [Fact]
        public void SkipsLocalizationOnInstall_WhenLocalizationValidationFails()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            var workingDir = TestUtils.CreateTemporaryFolder("Home");
            var testTemplateLocation = TestUtils.GetTestTemplateLocation("Invalid/Localization/ValidationFailure");

            var expectedErrors = new[]
            {
@$"Warning: [{testTemplateLocation + Path.DirectorySeparatorChar}.template.config/template.json]: id of the post action 'pa2' at index '3' is not unique. Only the first post action that uses this id will be localized.",

@$"Warning: Localization file {testTemplateLocation + Path.DirectorySeparatorChar}.template.config/localize/templatestrings.de-DE.json is not compatible with base configuration {testTemplateLocation + Path.DirectorySeparatorChar}.template.config/template.json, and will be skipped.
  In localization file under the post action with id 'pa1', there are localized strings for manual instruction(s) with ids 'do-not-exist'. These manual instructions do not exist in the template.json file and should be removed from localization file.
  Post action(s) with id(s) 'pa0' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.",

@$"Warning: Localization file {testTemplateLocation + Path.DirectorySeparatorChar}.template.config/localize/templatestrings.tr.json is not compatible with base configuration {testTemplateLocation + Path.DirectorySeparatorChar}.template.config/template.json, and will be skipped.
  Post action(s) with id(s) 'pa6' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file."
  };

            var commandResult = new DotnetNewCommand(_log, "-i", testTemplateLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"Success: {testTemplateLocation} installed the following templates:").And.HaveStdOutContaining("TestAssets.Invalid.Localiation.ValidationFailure");
            foreach (var error in expectedErrors)
            {
                commandResult.Should().HaveStdOutContaining(error);
            }
        }

        [Fact]
        public void SkipsLocalizationOnInstantiate_WhenInvalidFormat()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            var workingDir = TestUtils.CreateTemporaryFolder();
            var validTestTemplateLocation = TestUtils.GetTestTemplateLocation("TemplateWithLocalization");
            var invalidTestTemplateLocation = TestUtils.GetTestTemplateLocation("Invalid/Localization/InvalidFormat");
            var tmpTemplateLocation = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy(validTestTemplateLocation, tmpTemplateLocation, copySubDirs: true);

            new DotnetNewCommand(_log, "-i", tmpTemplateLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "de-DE")
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Warnung: Fehler beim Lesen der Analyselokalisierungsdatei").And.NotHaveStdOutContaining("localize/templatestrings.de-DE.json")
                .And.HaveStdOutContaining($"Erfolg: {tmpTemplateLocation} installierte die folgenden Vorlagen:").And.HaveStdOutContaining("TemplateWithLocalization")
                .And.HaveStdOutContaining("name_de-DE:äÄßöÖüÜ");

            //replace localization with bad file
            File.Copy(
                Path.Combine(invalidTestTemplateLocation, ".template.config", "localize", "templatestrings.de-DE.json"),
                Path.Combine(tmpTemplateLocation, ".template.config", "localize", "templatestrings.de-DE.json"),
                overwrite: true);

            new DotnetNewCommand(_log, "TestAssets.TemplateWithLocalization")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "de-DE")
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Warnung: Fehler beim Lesen der Analyselokalisierungsdatei").And.HaveStdOutContaining("localize/templatestrings.de-DE.json")
                .And.HaveStdOutContaining("Die Vorlage \"name\" wurde erfolgreich erstellt.").And.NotHaveStdOutContaining("name_de-DE:äÄßöÖüÜ");
        }

        [Fact]
        public void SkipsLocalizationOnInstantiate_WhenLocalizationValidationFails()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            var workingDir = TestUtils.CreateTemporaryFolder();
            var validTestTemplateLocation = TestUtils.GetTestTemplateLocation("TemplateWithLocalization");
            var invalidTestTemplateLocation = TestUtils.GetTestTemplateLocation("Invalid/Localization/ValidationFailure");
            var tmpTemplateLocation = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy(validTestTemplateLocation, tmpTemplateLocation, copySubDirs: true);

            var expectedErrors =
Regex.Escape(@$"Warnung: Die Lokalisierungsdatei {tmpTemplateLocation + Path.DirectorySeparatorChar}.template.config/localize/templatestrings.de-DE.json ist nicht mit der Basiskonfiguration {tmpTemplateLocation + Path.DirectorySeparatorChar}.template.config/template.json kompatibel und wird übersprungen.
  In der Lokalisierungsdatei unter der POST-Aktion mit der ID „pa1“ befinden sich lokalisierte Zeichenfolgen für manuelle Anweisungen mit den IDs „do-not-exist“. Diese manuellen Anweisungen sind in der Datei „template.json“ nicht vorhanden und sollten aus der Lokalisierungsdatei entfernt werden.").Replace('„', '.').Replace('“', '.');

            new DotnetNewCommand(_log, "-i", tmpTemplateLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "de-DE")
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining(expectedErrors)
                .And.HaveStdOutContaining($"Erfolg: {tmpTemplateLocation} installierte die folgenden Vorlagen:").And.HaveStdOutContaining("TemplateWithLocalization").And.HaveStdOutContaining("name_de-DE:äÄßöÖüÜ");

            //replace localization with bad file
            File.Copy(
                Path.Combine(invalidTestTemplateLocation, ".template.config", "localize", "templatestrings.de-DE.json"),
                Path.Combine(tmpTemplateLocation, ".template.config", "localize", "templatestrings.de-DE.json"),
                overwrite: true);

            new DotnetNewCommand(_log, "TestAssets.TemplateWithLocalization")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "de-DE")
                .Execute()
                .Should()
                .ExitWith(0)
                .And.HaveStdOutMatching(expectedErrors)
                .And.HaveStdOutContaining("Die Vorlage \"name\" wurde erfolgreich erstellt.").And.NotHaveStdOutContaining("name_de-DE:äÄßöÖüÜ");
        }
    }
}
