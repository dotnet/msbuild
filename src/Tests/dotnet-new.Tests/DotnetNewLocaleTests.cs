// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class DotnetNewLocaleTests : BaseIntegrationTest
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewLocaleTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void TestDefaultLocale()
        {
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            string home = CreateTemporaryFolder(folderName: "Home");
            string? thisDir = Path.GetDirectoryName(typeof(DotnetNewLocaleTests).Assembly.Location);
            string testTemplatesFolder = GetTestTemplateLocation("TemplateWithLocalization");

            CommandResult commandResult = new DotnetNewCommand(_log, "-i", testTemplatesFolder)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
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
            string home = CreateTemporaryFolder(folderName: "Home");
            string? thisDir = Path.GetDirectoryName(typeof(DotnetNewLocaleTests).Assembly.Location);
            string testTemplatesFolder = GetTestTemplateLocation("TemplateWithLocalization");

            CommandResult commandResult = new DotnetNewCommand(_log, "-i", testTemplatesFolder)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
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
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDir = CreateTemporaryFolder();
            string testTemplateLocation = GetTestTemplateLocation("Invalid/Localization/InvalidFormat");
            new DotnetNewCommand(_log, "-i", testTemplateLocation)
                .WithDebug()
                .WithoutBuiltInTemplates()
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Warning: Failed to read or parse localization file").And.HaveStdOutContaining("localize/templatestrings.de-DE.json")
                .And.HaveStdOutContaining($"Success: {testTemplateLocation} installed the following templates:").And.HaveStdOutContaining("TestAssets.Invalid.Localization.InvalidFormat")
                .And.HaveStdOutContaining("name in base configuration");
        }

        [Fact]
        public void SkipsLocalizationOnInstall_WhenLocalizationValidationFails()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDir = CreateTemporaryFolder();
            string testTemplateLocation = GetTestTemplateLocation("Invalid/Localization/ValidationFailure");

            string[] expectedErrors = new[]
            {
@$"Warning: [{testTemplateLocation + Path.DirectorySeparatorChar}.template.config/template.json]: id of the post action 'pa2' at index '3' is not unique. Only the first post action that uses this id will be localized.",

"""
Error: The template 'name' (TestAssets.Invalid.Localiation.ValidationFailure) has the following validation errors in 'de-DE' localization:
   [Error][LOC001] In localization file under the post action with id 'pa1', there are localized strings for manual instruction(s) with ids 'do-not-exist'. These manual instructions do not exist in the template.json file and should be removed from localization file.
   [Error][LOC002] Post action(s) with id(s) 'pa0' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.
""",
"""
Error: The template 'name' (TestAssets.Invalid.Localiation.ValidationFailure) has the following validation errors in 'tr' localization:
   [Error][LOC002] Post action(s) with id(s) 'pa6' specified in the localization file do not exist in the template.json file. Remove the localized strings from the localization file.
""",
"Warning: Failed to install the 'de-DE' localization the template 'name' (TestAssets.Invalid.Localiation.ValidationFailure): the localization file is not valid. The localization will be skipped.",
"Warning: Failed to install the 'tr' localization the template 'name' (TestAssets.Invalid.Localiation.ValidationFailure): the localization file is not valid. The localization will be skipped.",

  };

            CommandResult commandResult = new DotnetNewCommand(_log, "-i", testTemplateLocation)
                .WithDebug()
                .WithoutBuiltInTemplates()
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"Success: {testTemplateLocation} installed the following templates:").And.HaveStdOutContaining("TestAssets.Invalid.Localiation.ValidationFailure");
            foreach (string? error in expectedErrors)
            {
                commandResult.Should().HaveStdOutContaining(error);
            }
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "need investigation and move to dotnet/templating.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void SkipsLocalizationOnInstantiate_WhenInvalidFormat()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDir = CreateTemporaryFolder();
            string validTestTemplateLocation = GetTestTemplateLocation("TemplateWithLocalization");
            string invalidTestTemplateLocation = GetTestTemplateLocation("Invalid/Localization/InvalidFormat");
            string tmpTemplateLocation = CreateTemporaryFolder();
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

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "need investigation and move to dotnet/templating.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void SkipsLocalizationOnInstantiate_WhenLocalizationValidationFails()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDir = CreateTemporaryFolder();
            string validTestTemplateLocation = GetTestTemplateLocation("TemplateWithLocalization");
            string invalidTestTemplateLocation = GetTestTemplateLocation("Invalid/Localization/ValidationFailure");
            string tmpTemplateLocation = CreateTemporaryFolder();
            TestUtils.DirectoryCopy(validTestTemplateLocation, tmpTemplateLocation, copySubDirs: true);

            string expectedErrors =
Regex.Escape(@$"Warnung: Die Lokalisierungsdatei {tmpTemplateLocation + Path.DirectorySeparatorChar}.template.config/localize/templatestrings.de-DE.json ist nicht mit der Basiskonfiguration {tmpTemplateLocation + Path.DirectorySeparatorChar}.template.config/template.json kompatibel und wird übersprungen.
  In der Lokalisierungsdatei unter der POST-Aktion mit der ID „pa1“ befinden sich lokalisierte Zeichenfolgen für manuelle Anweisungen mit den IDs „do-not-exist“. Diese manuellen Anweisungen sind in der Datei „template.json“ nicht vorhanden und sollten aus der Lokalisierungsdatei entfernt werden.").Replace('„', '.').Replace('“', '.');

            new DotnetNewCommand(_log, "-i", tmpTemplateLocation)
                .WithCustomHive(home)
                .WithDebug()
                .WithoutBuiltInTemplates()
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
