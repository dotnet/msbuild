// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
@"Search failed: no template name is specified.
To search for the template, specify template name or use one of supported filters: 'author', 'baseline', 'language', 'type', 'package', 'tag'.
Examples:
        dotnet new3 <template name> --search
        dotnet new3 --search --author Microsoft
        dotnet new3 <template name> --search --author Microsoft");
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "azure"), "'Template Name' or 'Short Name' columns do not contain the criteria");

            var microsoftPackages = tableOutput.Where(row => row[2] == "Microsoft");
            var installationCommands = microsoftPackages.Select(package => $"dotnet new3 -i {package[4]}");

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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Type", "Tags", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Tags", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Tags" }, "Common"), "'Tags' column does not contain the criteria");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Tags"), "'Tags' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Tags", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Tags" }, "Common"), "'Tags' column does not contain the criteria");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Tags"), "'Tags' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterAuthor()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--search", "--columns", "author", "--author", "micro")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Author" }, "micro"), "'Author' column does not contain the criteria");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Author"), "'Author' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
        }

        [Fact]
        public void CanFilterAuthor_WithoutName()
        {
            var commandResult = new DotnetNewCommand(_log, "--search", "--columns", "author", "--author", "micro")
                .WithCustomHive(_sharedHome.HomeDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Searching for the templates...")
                .And.HaveStdOutContaining("Matches from template source: NuGet.org")
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Author\\s+Package\\s+Downloads")
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Author" }, "micro"), "'Author' column does not contain the criteria");
            Assert.True(SomeRowsContain(tableOutput, new[] { "Author" }, "Microsoft"), "'Author' column does not contain any rows with 'Microsoft'");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Author"), "'Author' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Language" }, "Q#"), "'Language' column does not contain criteria");

            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Language"), "'Language' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Language" }, "F#"), "'Language' column does not contain criteria");

            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Language"), "'Language' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Type", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsEqual(tableOutput, new[] { "Type" }, "item"), "'Type' column does not contain criteria");

            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Type"), "'Type' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Type", "Package", "Downloads" });
            Assert.True(AllRowsEqual(tableOutput, new[] { "Type" }, "item"), "'Type' column does not contain criteria");

            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Type"), "'Type' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });

            Assert.True(AllRowsContain(tableOutput, new[] { "Template Name", "Short Name" }, "console"), "'Template Name' or 'Short Name' columns do not contain the criteria");
            Assert.True(AllRowsContain(tableOutput, new[] { "Package" }, "core"), "'Package' column does not contain criteria");

            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
                .And.HaveStdOutContaining("To use the template, run the following command to install the package: dotnet new3 -i <package>");

            var tableOutput = ParseTableOutput(commandResult.StdOut, expectedColumns: new[] { "Template Name", "Short Name", "Author", "Language", "Package", "Downloads" });
            Assert.True(AllRowsContain(tableOutput, new[] { "Package" }, "core"), "'Package' column does not contain criteria");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Template Name"), "'Template Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Short Name"), "'Short Name' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Package"), "'Package' column contains empty values");
            Assert.True(AllRowsAreNotEmpty(tableOutput, "Downloads"), "'Downloads' column contains empty values");
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
    }
}
