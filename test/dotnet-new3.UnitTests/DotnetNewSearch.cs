// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.NET.TestFramework.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNewSearch : IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _sharedHome;
        private readonly ITestOutputHelper _log;

        public DotnetNewSearch(SharedHomeDirectory sharedHome, ITestOutputHelper log)
        {
            _sharedHome = sharedHome;
            _log = log;
        }

        [Fact]
        public void BasicTest()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--search")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CannotExecuteEmptyCriteria()
        {
            new DotnetNewCommand(_log, "--search")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining(
@"Search failed: not enough information specified for search.
To search for templates, specify partial template name or use one of supported filters: 'author', 'baseline', 'language', 'type', 'tag', 'package'.
Examples:
   dotnet new3 <TEMPLATE_NAME> --search
   dotnet new3 --search --author Microsoft
   dotnet new3 <TEMPLATE_NAME> --search --author Microsoft");
        }

        [Fact]
        public void CannotExecuteSearchWithShortCriteria()
        {
            new DotnetNewCommand(_log, "c", "--search")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute()
                .Should().Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Search failed: template name is too short, minimum 2 characters are required.");
        }

        [Fact]
        public void ExamplePrefersMicrosoftPackage()
        {
            var commandResult = new DotnetNewCommand(_log, "azure", "--search")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "azure"), "'Template Name' or 'Short Name' columns do not contain the criteria");

            var microsoftPackages = tableOutput.Where(row => row[2] == "Microsoft" && row[4].StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase));
            var installationCommands = microsoftPackages.Select(package => $"dotnet new3 --install {package[4]}");

            Func<string, bool> containsOneOfInstallationCommands = (output) => installationCommands.Any(command => output.Contains(command));
            commandResult.Should().HaveStdOutContaining(containsOneOfInstallationCommands, "Checks if the output contains one of the expected installation commands");
        }

        [Fact]
        public void CanShowAllColumns()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--search", "--columns-all")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Type", "Tags", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterTags()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--search", "--columns", "tags", "--tag", "Common")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Tags\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Tags", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Tags" }, "Common"), "'Tags' column does not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Tags"), "'Tags' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterTags_WithoutName()
        {
            var commandResult = new DotnetNewCommand(_log, "--search", "--columns", "tags", "--tag", "Common")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Tags\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Tags", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Tags" }, "Common"), "'Tags' column does not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Tags"), "'Tags' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterAuthor()
        {
            var commandResult = new DotnetNewCommand(_log, "func", "--search", "--columns", "author", "--author", "micro")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "func"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Author" }, "micro"), "'Author' column does not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Author"), "'Author' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterAuthor_WithoutName()
        {
            var commandResult = new DotnetNewCommand(_log, "--search", "--columns", "author", "--author", "micro")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Author" }, "micro"), "'Author' column does not contain the criteria");
            Assert.True(SomeRowsContain(tableOutput, new[] { "Author" }, "Microsoft"), "'Author' column does not contain any rows with 'Microsoft'");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Author"), "'Author' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterLanguage()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--search", "--columns", "language", "--language", "Q#")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Language" }, "Q#"), "'Language' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Language"), "'Language' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterLanguage_WithoutName()
        {
            var commandResult = new DotnetNewCommand(_log, "--search", "--columns", "language", "--language", "F#")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Language" }, "F#"), "'Language' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Language"), "'Language' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterType()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--search", "--columns", "type", "--type", "item")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Type\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Type", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsEqual(tableOutput, new[] { "Type" }, "item"), "'Type' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Type"), "'Type' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterType_WithoutName()
        {
            var commandResult = new DotnetNewCommand(_log, "--search", "--columns", "type", "--type", "item")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Type\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Type", "Package", "Downloads" });
            Assert.True(AllRowsEqual(tableOutput, new[] { "Type" }, "item"), "'Type' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Type"), "'Type' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterPackage()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--search", "--package", "core")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Package" }, "core"), "'Package' column does not contain criteria");

            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterPackage_WithoutName()
        {
            var commandResult = new DotnetNewCommand(_log, "--search", "--package", "core")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Package" }, "core"), "'Package' column does not contain criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanSortByName()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--search")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });

            Assert.True(tableOutput.Count > 2, "At least 2 search hits are expected");

            // rows can be shrunk: ML.NET Console App for Training and ML.NET Console App for Train...
            // in this case ML.NET Console App for Training < ML.NET Console App for Train...
            // therefore use custom comparer 
            var comparer = new ShrinkAwareOrdinalStringComparer();
            //first row is the header
            for (int i = 2; i < tableOutput.Count; i++)
            {
                Assert.True(
                    comparer.Compare(tableOutput[i - 1][0], tableOutput[i][0]) <= 0,
                    $"the following entries of the table are not sorted alphabetically by first column: {tableOutput[i - 1][0]} and {tableOutput[i][0]}.");
            }
        }

        [Fact]
        public void CanFilterByChoiceParameter()
        {
            var commandResult = new DotnetNewCommand(_log, "con", "--search", "--framework")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', --framework")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "con", "--search", "-f")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', -f")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "--search", "-f")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: -f")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterByNonChoiceParameter()
        {
            var commandResult = new DotnetNewCommand(_log, "con", "--search", "--langVersion")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "--search", "--langVersion")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void IgnoresValueForNonChoiceParameter()
        {
            var commandResult = new DotnetNewCommand(_log, "con", "--search", "--langVersion", "smth")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "--search", "--langVersion", "smth")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: --langVersion")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterByChoiceParameterWithValue()
        {
            var commandResult = new DotnetNewCommand(_log, "con", "--search", "-f", "netcoreapp3.1")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: 'con', -f='netcoreapp3.1'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "con"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");

            commandResult = new DotnetNewCommand(_log, "--search", "-f", "net5.0")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .WithDebug()
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutContaining("These templates matched your input: -f='net5.0'")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Language\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package:")
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>");

            tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AtLeastOneRowIsNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
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
            var commandResult1 = new DotnetNewCommand(_log, command1.Split())
             .WithCustomHive(_sharedHome.HomeDirectory)
             .Execute();

            var commandResult2 = new DotnetNewCommand(_log, command2.Split())
               .WithCustomHive(_sharedHome.HomeDirectory)
               .Execute();

            Assert.Equal(commandResult1.StdOut, commandResult2.StdOut);
        }

        [Theory]
        [InlineData("--search foo --columns-all bar", "bar", "foo")]
        [InlineData("--search foo bar", "bar", "foo")]
        [InlineData("foo --search bar", "bar", "foo", true)]
        [InlineData("foo --search bar --language F#", "bar", "foo", true)]
        [InlineData("foo --search --columns-all bar", "bar", "foo")]
        [InlineData("foo --search --columns-all --framework net6.0 bar", "bar", "foo|--framework|net6.0")]
        [InlineData("foo --search --columns-all -other-param --framework net6.0 bar", "bar", "foo|--framework|net6.0|-other-param")]
        public void CannotSearchOnParseError(string command, string invalidArguments, string validArguments, bool invalidSyntax = false)
        {
            var commandResult = new DotnetNewCommand(_log, command.Split())
             .WithCustomHive(_sharedHome.HomeDirectory)
             .Execute();

            if (invalidSyntax)
            {
                commandResult.Should().Fail().And.HaveStdErrContaining("Invalid command syntax: use 'dotnet new3 --search [PARTIAL_NAME] [FILTER_OPTIONS]' instead.");
                return;
            }

            commandResult.Should().Fail()
                .And.HaveStdErrContaining("Error: Invalid option(s):");
            foreach (string arg in invalidArguments.Split('|'))
            {
                commandResult.Should().HaveStdErrContaining(arg);
            }

            foreach (string arg in validArguments.Split('|'))
            {
                commandResult.Should().NotHaveStdErrContaining(arg);
            }
        }

        private static bool AllRowsContain(List<List<string>> tableOutput, string[] columnsNames, string value)
        {
            var columnIndexes = columnsNames.Select(columnName => tableOutput[0].IndexOf(columnName));

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
            var columnIndexes = columnsNames.Select(columnName => tableOutput[0].IndexOf(columnName));

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
            var columnIndexes = columnsNames.Select(columnName => tableOutput[0].IndexOf(columnName));

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
            var columnIndex = tableOutput[0].IndexOf(columnName);
            return tableOutput.All(row => !string.IsNullOrWhiteSpace(row[columnIndex]));
        }

        private static bool AtLeastOneRowIsNotEmpty(List<List<string>> tableOutput, string columnName)
        {
            var columnIndex = tableOutput[0].IndexOf(columnName);
            return tableOutput.Any(row => !string.IsNullOrWhiteSpace(row[columnIndex]));
        }

        private List<List<string>> ParseTableOutput(string stdOut, string[] expectedColumns)
        {
            string[] lines = stdOut.Split(Environment.NewLine);

            int headerLineIndex = Array.FindIndex(lines, line => expectedColumns.All(column => line.Contains(column)));
            string headerLine = lines[headerLineIndex];
            //table ends after empty line
            int lastLineIndex = Array.FindIndex(lines, headerLineIndex + 1, line => line.Length == 0) - 1;
            var columnsIndexes = expectedColumns.Select(column => headerLine.IndexOf(column)).ToArray();

            var parsedTable = new List<List<string>>();
            // first array contain headers
            var headerRow = new List<string>();
            foreach (var expectedColumn in expectedColumns)
            {
                headerRow.Add(expectedColumn.Trim());
            }
            parsedTable.Add(headerRow);

            //we start from 2nd row after header (1st row contains separator)
            for (int i = headerLineIndex + 2; i <= lastLineIndex; i++)
            {
                var parsedRow = new List<string>();
                for (int j = 0; j < columnsIndexes.Length - 1; j++)
                {
                    parsedRow.Add(lines[i].Substring(columnsIndexes[j], columnsIndexes[j + 1] - columnsIndexes[j]).Trim());
                }
                parsedRow.Add(lines[i].Substring(columnsIndexes[columnsIndexes.Length - 1]).Trim());
                parsedTable.Add(parsedRow);
            }
            return parsedTable;
        }

        private class ShrinkAwareOrdinalStringComparer : IComparer<string>
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
                    return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
                }
                
                if (rightIsShrunk && left.StartsWith(right.Substring(0, right.Length - 3), StringComparison.OrdinalIgnoreCase))
                {
                    return -1;
                }
                if (leftIsShrunk && right.StartsWith(left.Substring(0, left.Length - 3), StringComparison.OrdinalIgnoreCase))
                {
                    return 1;
                }
                return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
