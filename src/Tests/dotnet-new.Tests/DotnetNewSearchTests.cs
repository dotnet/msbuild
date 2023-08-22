// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewSearchTests : BaseIntegrationTest, IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _sharedHome;
        private readonly ITestOutputHelper _log;

        public DotnetNewSearchTests(SharedHomeDirectory sharedHome, ITestOutputHelper log) : base(log)
        {
            _sharedHome = sharedHome;
            _log = log;
        }

        [Theory]
        [InlineData("console --search")]
        [InlineData("--search console")]
        [InlineData("search console")]
        public void BasicTest(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'console'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("--search c")]
        [InlineData("search c")]
        public void CannotExecuteSearchWithShortCriteria(string testCase)
        {
            new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("Search failed: template name is too short, minimum 2 characters are required.");
        }

        [Theory]
        [InlineData("--search fofofo", "'fofofo'")]
        [InlineData("search fofofo", "'fofofo'")]
        [InlineData("search fofofo --type item", "'fofofo', --type='item'")]
        [InlineData("search fofofo --language Z#", "'fofofo', --language='Z#'")]
        [InlineData("search -lang Z#", "-lang='Z#'")]
        public void CanDisplayNoResults(string testCase, string criteria)
        {
            new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining($"No templates found matching: {criteria}.");
        }

        [Theory]
        [InlineData("azure --search --columns author")]
        [InlineData("--search azure --columns author")]
        [InlineData("search azure --columns author")]
        public void ExamplePrefersMicrosoftPackage(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "azure"), "'Template Name' or 'Short Name' columns do not contain the criteria");

            IEnumerable<List<string>> microsoftPackages = tableOutput.Where(row => row[2] == "Microsoft" && row[3].StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase));
            IEnumerable<string> installationCommands = microsoftPackages.Select(package => $"new install {package[3].Split(" /")[0]}").ToList();

            bool ContainsOneOfInstallationCommands(string output) => installationCommands.Any(command => output.Contains(command));
            commandResult.Should().HaveStdOutContaining(ContainsOneOfInstallationCommands, "Checks if the output contains one of the expected installation commands");
        }

        [Theory]
        [InlineData("console --search --columns-all")]
        [InlineData("--columns-all --search console")]
        [InlineData("search console --columns-all")]
        public void CanShowAllColumns(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Type", "Tags", "Package Name / Owners", "Trusted", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("console --search --columns tags --tag Common")]
        [InlineData("--search console --columns tags --tag Common")]
        [InlineData("search console --columns tags --tag Common")]
        public void CanFilterTags(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'console', --tag='Common'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Tags\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Tags", "Package Name / Owners", "Trusted", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Tags" }, "Common"), "'Tags' column does not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Tags"), "'Tags' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("--search --columns tags --tag Common")]
        [InlineData("--columns tags --search --tag Common")]
        [InlineData("search --columns tags --tag Common")]
        public void CanFilterTags_WithoutName(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: --tag='Common'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Tags\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Tags", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Tags" }, "Common"), "'Tags' column does not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Tags"), "'Tags' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("func --search --columns author --author micro")]
        [InlineData("--search func --columns author --author micro")]
        [InlineData("search func --columns author --author micro")]
        public void CanFilterAuthor(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'func', --author='micro'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Package Name / Owners", "Trusted", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "func"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Author" }, "micro"), "'Author' column does not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Author"), "'Author' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("--search --columns author --author micro")]
        [InlineData("search --columns author --author micro")]
        public void CanFilterAuthor_WithoutName(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("These templates matched your input: --author='micro'")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Author" }, "micro"), "'Author' column does not contain the criteria");
            Assert.True(SomeRowsContain(tableOutput, new[] { "Author" }, "Microsoft"), "'Author' column does not contain any rows with 'Microsoft'");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Author"), "'Author' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Trusted"), "'Trusted' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("console --search --columns language --language Q#")]
        [InlineData("--search console --columns language --language Q#")]
        [InlineData("search console --columns language --language Q#")]
        public void CanFilterLanguage(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("These templates matched your input: 'console', --language='Q#'")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Language" }, "Q#"), "'Language' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Language"), "'Language' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("--search --columns language --language Q#", "--language")]
        [InlineData("search --columns language --language Q#", "--language")]
        [InlineData("--search --columns language -lang Q#", "-lang")]
        [InlineData("search --columns language -lang Q#", "-lang")]
        public void CanFilterLanguage_WithoutName(string testCase, string optionName)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining($"These templates matched your input: {optionName}='Q#'")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Language" }, "Q#"), "'Language' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Language"), "'Language' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("console --search --columns type --type item")]
        [InlineData("--search console --columns type --type item")]
        [InlineData("search console --columns type --type item")]
        public void CanFilterType(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory).WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("These templates matched your input: 'console', --type='item'")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Type\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Type", "Package Name / Owners", "Trusted", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsEqual(tableOutput, new[] { "Type" }, "item"), "'Type' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Type"), "'Type' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("--search --columns type --type item")]
        [InlineData("search --columns type --type item")]
        public void CanFilterType_WithoutName(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory).WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("These templates matched your input: --type='item'")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Type\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Type", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsEqual(tableOutput, new[] { "Type" }, "item"), "'Type' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Type"), "'Type' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Trusted"), "'Trusted' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("console --search --package core")]
        [InlineData("--search console --package core")]
        [InlineData("search console --package core")]
        public void CanFilterPackage(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("These templates matched your input: 'console', --package='core'")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Package Name / Owners" }, "core"), "'Package Name / Owners' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Trusted"), "'Trusted' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("--search --package core")]
        [InlineData("search --package core")]
        public void CanFilterPackage_WithoutName(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("These templates matched your input: --package='core'")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Package Name / Owners" }, "core"), "'Package Name / Owners' column does not contain criteria");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name \\/ Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Trusted"), "'Trusted' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Theory]
        [InlineData("console --search")]
        [InlineData("--search console")]
        [InlineData("search console")]
        public void CanSortByDownloadCountAndThenByName(string testCase)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });

            Assert.True(tableOutput.Count > 2, "At least 2 search hits are expected");

            // rows can be shrunk: ML.NET Console App for Training and ML.NET Console App for Train...
            // in this case ML.NET Console App for Training < ML.NET Console App for Train...
            // therefore use custom comparer
            var nameComparer = new ShrinkAwareCurrentCultureStringComparer();
            var downloadCountComparer = new DownloadCountComparer();

            var orderedRows = tableOutput
                .Skip(1)
                .Select(x => new { name = x[0], count = x[5] })
                .OrderByDescending(x => x.count, downloadCountComparer)
                .ThenBy(x => x.name, nameComparer);

            for (int i = 1; i < tableOutput.Count; i++)
            {
                Assert.Equal(orderedRows.ElementAt(i - 1).name, tableOutput[i][0]);
                Assert.Equal(orderedRows.ElementAt(i - 1).count, tableOutput[i][5]);
            }
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanFilterByChoiceParameter()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "con", "--search", "--framework")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', --framework")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Trusted"), "'Trusted' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "con", "--search", "-f")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', -f")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "--search", "-f")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: -f")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanFilterByNonChoiceParameter()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "con", "--search", "--langVersion")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "--search", "--langVersion")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void IgnoresValueForNonChoiceParameter()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "con", "--search", "--langVersion", "smth")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "--search", "--langVersion", "smth")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanFilterByChoiceParameterWithValue()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "con", "--search", "-f", "netcoreapp3.1")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', -f='netcoreapp3.1'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            List<List<string>> tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "--search", "-f", "net5.0")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: -f='net5.0'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package Name \\/ Owners\\s+Trusted\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new install [<package>...]");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package Name / Owners", "Trusted", "Downloads" });
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package Name / Owners"), "'Package Name / Owners' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "Template options filtering is not implemented.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CannotSearchTemplatesWithUnknownParameter()
        {
            new DotnetNewCommand(_log, "--search", "--unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: --unknown.");

            new DotnetNewCommand(_log, "con", "--search", "--unknown")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("No templates found matching: 'con', --unknown.");

            new DotnetNewCommand(_log, "con", "--search", "--unknown", "--language", "C#")
              .WithCustomHive(_sharedHome.HomeDirectory)
              .Execute()
              .Should().Fail()
              .And.HaveStdErrContaining("No templates found matching: 'con', language='C#', --unknown.");
        }

        [Theory]
        [InlineData("zoop --search", "--search zoop")]
        [InlineData("zoop --search --language F#", "--search zoop --language F#")]
        [InlineData("zoop --search --columns-all", "--search zoop --columns-all")]
        public void CanFallbackToSearchOption(string command1, string command2)
        {
            CommandResult commandResult1 = new DotnetNewCommand(_log, command1.Split())
             .WithCustomHive(_sharedHome.HomeDirectory)
             .Execute();

            CommandResult commandResult2 = new DotnetNewCommand(_log, command2.Split())
               .WithCustomHive(_sharedHome.HomeDirectory)
               .Execute();

            Assert.Equal(commandResult1.StdOut, commandResult2.StdOut);
        }

        [Theory]
        [InlineData("--search foo --columns-all bar", "bar", "foo")]
        [InlineData("--search foo bar", "bar", "foo")]
        [InlineData("foo --search --columns-all --framework net6.0 bar", "bar|net6.0|foo", "--framework")]
        [InlineData("foo --search --columns-all -other-param --framework net6.0 bar", "bar|net6.0|--framework|foo", "-other-param")]
        [InlineData("search foo --columns-all bar", "bar", "foo")]
        [InlineData("foo --search bar", "foo", "bar")]
        [InlineData("foo --search bar --language F#", "foo", "bar")]
        [InlineData("foo --search --columns-all bar", "foo", "bar")]
        [InlineData("foo search bar", "foo", "bar")]
        public void CannotSearchOnParseError(string command, string invalidArguments, string validArguments)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, command.Split())
             .WithCustomHive(_sharedHome.HomeDirectory)
             .Execute();

            commandResult.Should().Fail();
            foreach (string arg in invalidArguments.Split('|'))
            {
                commandResult.Should().HaveStdErrMatching($"Unrecognized command or (argument\\(s\\)\\:|argument) '{arg}'");
            }

            foreach (string arg in validArguments.Split('|'))
            {
                commandResult.Should()
                    .NotHaveStdErrContaining($"Unrecognized command or argument '{arg}'")
                    .And.NotHaveStdErrContaining($"Unrecognized command or argument(s): '{arg}'");
            }
        }

        [Fact]
        public void CanShowDeprecationMessage_WhenLegacyCommandIsUsed()
        {
            const string deprecationMessage =
@"Warning: use of 'dotnet new --search' is deprecated. Use 'dotnet new search' instead.
For more information, run: 
   dotnet new search -h";

            CommandResult commandResult = new DotnetNewCommand(_log, "--search", "console")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            Assert.StartsWith(deprecationMessage, commandResult.StdOut);
        }

        [Fact]
        public void DoNotShowDeprecationMessage_WhenNewCommandIsUsed()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "search", "console")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Warning")
                .And.NotHaveStdOutContaining("deprecated");
        }

        private static bool AllRowsContain(List<List<string>> tableOutput, string[] columnsNames, string value)
        {
            IEnumerable<int> columnIndexes = columnsNames.Select(columnName => tableOutput[0].IndexOf(columnName));

            for (int i = 1; i < tableOutput.Count; i++)
            {
                if (columnIndexes.Any(index => tableOutput[i][index].Contains(value, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                // template name can be shortened so the name criteria might be truncated.
                if (columnsNames.Contains("Template Name") && tableOutput[i][tableOutput[0].IndexOf("Template Name")].EndsWith("..."))
                {
                    continue;
                }
                // tags can be shortened so the tag criteria might be truncated.
                if (columnsNames.Contains("Tags") && tableOutput[i][tableOutput[0].IndexOf("Tags")].EndsWith("..."))
                {
                    continue;
                }

                // tags can be shortened so the tag criteria might be truncated.
                if (columnsNames.Contains("Tags") && tableOutput[i][tableOutput[0].IndexOf("Tags")].EndsWith("..."))
                {
                    continue;
                }

                // if columns are template name and/or short name and they are empty - skip, grouping in done
                bool criteriaA = columnsNames.Contains("Template Name")
                    && string.IsNullOrWhiteSpace(tableOutput[i][tableOutput[0].IndexOf("Template Name")])
                    || !columnsNames.Contains("Short Name");
                bool criteriaB = columnsNames.Contains("Short Name")
                  && string.IsNullOrWhiteSpace(tableOutput[i][tableOutput[0].IndexOf("Short Name")])
                  || !columnsNames.Contains("Short Name");
                bool criteriaC = columnsNames.Contains("Short Name") || columnsNames.Contains("Template Name");

                if (criteriaA && criteriaB && criteriaC)
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        private static bool AllRowsEqual(List<List<string>> tableOutput, string[] columnsNames, string value)
        {
            IEnumerable<int> columnIndexes = columnsNames.Select(columnName => tableOutput[0].IndexOf(columnName));

            for (int i = 1; i < tableOutput.Count; i++)
            {
                if (columnIndexes.Any(index => tableOutput[i][index].Equals(value, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        private static bool SomeRowsContain(List<List<string>> tableOutput, string[] columnsNames, string value)
        {
            IEnumerable<int> columnIndexes = columnsNames.Select(columnName => tableOutput[0].IndexOf(columnName));

            for (int i = 1; i < tableOutput.Count; i++)
            {
                if (columnIndexes.Any(index => tableOutput[i][index].Contains(value, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AllRowsAreNotEmpty(List<List<string>> tableOutput, string columnName)
        {
            int columnIndex = tableOutput[0].IndexOf(columnName);
            return tableOutput.All(row => !string.IsNullOrWhiteSpace(row[columnIndex]));
        }

        private static bool AtLeastOneRowIsNotEmpty(List<List<string>> tableOutput, string columnName)
        {
            int columnIndex = tableOutput[0].IndexOf(columnName);
            return tableOutput.Any(row => !string.IsNullOrWhiteSpace(row[columnIndex]));
        }

        private static List<List<string>> ParseTableOutput(string stdOut, string[] expectedColumns)
        {
            string[] lines = stdOut.Split(Environment.NewLine);

            int headerLineIndex = Array.FindIndex(lines, line => expectedColumns.All(column => line.Contains(column)));
            string headerLine = lines[headerLineIndex];
            //table ends before empty line
            //or before first [Debug] entry
            //table is written in single call, so there can be no [Debug] entry in the middle
            int lastLineIndex = Array.FindIndex(lines, headerLineIndex + 1, line => (line.Length == 0 || line.Contains("[Debug]"))) - 1;
            int[] columnIndexes = expectedColumns.Select(column => headerLine.IndexOf(column)).ToArray();

            var parsedTable = new List<List<string>>();
            // first array contain headers
            var headerRow = new List<string>();
            foreach (string expectedColumn in expectedColumns)
            {
                headerRow.Add(expectedColumn.Trim());
            }
            parsedTable.Add(headerRow);

            //we start from 2nd row after header (1st row contains separator)
            for (int i = headerLineIndex + 2; i <= lastLineIndex; i++)
            {
                List<string> parsedRow = new(SplitLineByColumns(lines[i], columnIndexes).Select(c => c.Trim()));
                parsedTable.Add(parsedRow);
            }
            return parsedTable;
        }

        /// <summary>
        /// Splits the given input string into multiple columns using the given indices.
        /// Indices do not refer to the number of characters, but to the visual space occupied by characters when drawn.
        /// </summary>
        /// <param name="input">Input string to be splitted.</param>
        /// <param name="indexes">Indices to split the string from.</param>
        /// <returns></returns>
        private static IEnumerable<string> SplitLineByColumns(string input, int[] indexes)
        {
            StringBuilder columnBuilder = new(capacity: 16);
            int processedCharCount = 0;

            int inputLength = input.Aggregate(0, (aggr, next) => aggr + Wcwidth.UnicodeCalculator.GetWidth(next));

            for (int j = 0; j < indexes.Length; j++)
            {
                int unfilledColumnWidth = (j == indexes.Length - 1 ? inputLength : indexes[j + 1]) - indexes[j];
                columnBuilder.Clear();

                while (unfilledColumnWidth > 0)
                {
                    char c = input[processedCharCount++];
                    int charLength = Wcwidth.UnicodeCalculator.GetWidth(c);
                    columnBuilder.Append(c);
                    unfilledColumnWidth -= charLength;
                }

                yield return columnBuilder.ToString();
            }
        }

        private class ShrinkAwareCurrentCultureStringComparer : IComparer<string>
        {
            public int Compare(string? left, string? right)
            {
                if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
                {
                    return 0;
                }

                if (string.IsNullOrEmpty(left))
                {
                    return -1;
                }

                if (string.IsNullOrEmpty(right))
                {
                    return 1;
                }

                bool leftIsShrunk = left.EndsWith("...");
                bool rightIsShrunk = right.EndsWith("...");
                if (!(leftIsShrunk ^ rightIsShrunk))
                {
                    return string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
                }

                if (rightIsShrunk && left.StartsWith(right.Substring(0, right.Length - 3), StringComparison.CurrentCultureIgnoreCase))
                {
                    return -1;
                }
                if (leftIsShrunk && right.StartsWith(left.Substring(0, left.Length - 3), StringComparison.CurrentCultureIgnoreCase))
                {
                    return 1;
                }
                return string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        private class DownloadCountComparer : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                if (x == y || string.IsNullOrWhiteSpace(x) && string.IsNullOrWhiteSpace(y))
                {
                    return 0;
                }
                if (string.IsNullOrWhiteSpace(x))
                {
                    return -1;
                }
                if (string.IsNullOrWhiteSpace(y))
                {
                    return 1;
                }
                int xInt = 0;
                int yInt = 0;

                if (x != "<1k")
                {
                    _ = int.TryParse(x.Trim().AsSpan(0, x.Length - 1), out xInt);
                }
                if (y != "<1k")
                {
                    _ = int.TryParse(y.Trim().AsSpan(0, y.Length - 1), out yInt);
                }
                return xInt.CompareTo(yInt);
            }
        }
    }
}
