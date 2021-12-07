// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class ListTests
    {
        private static Dictionary<string, FilterOptionDefinition> _stringToFilterDefMap = new Dictionary<string, FilterOptionDefinition>()
        {
            { "author", FilterOptionDefinition.AuthorFilter },
            { "type", FilterOptionDefinition.TypeFilter },
            { "language", FilterOptionDefinition.LanguageFilter },
            { "tag", FilterOptionDefinition.TagFilter },
            { "baseline", FilterOptionDefinition.BaselineFilter },
        };

        [Theory]
        [InlineData("new list source --author filter-value", "author")]
        [InlineData("new --list source --author filter-value", "author")]
        [InlineData("new source --author filter-value --list", "author")]
        [InlineData("new source --list --author filter-value ", "author")]
        [InlineData("new --author filter-value --list source", "author")]
        [InlineData("new list source --language filter-value", "language")]
        [InlineData("new --list source --language filter-value", "language")]
        [InlineData("new source --language filter-value --list", "language")]
        [InlineData("new source --list --language filter-value ", "language")]
        [InlineData("new --language filter-value --list source", "language")]
        [InlineData("new list source -lang filter-value", "language")]
        [InlineData("new --list source -lang filter-value", "language")]
        [InlineData("new source -lang filter-value --list", "language")]
        [InlineData("new source --list -lang filter-value ", "language")]
        [InlineData("new -lang filter-value --list source", "language")]
        [InlineData("new list source --tag filter-value", "tag")]
        [InlineData("new --list source --tag filter-value", "tag")]
        [InlineData("new source --tag filter-value --list", "tag")]
        [InlineData("new source --list --tag filter-value ", "tag")]
        [InlineData("new --tag filter-value --list source", "tag")]
        [InlineData("new list source --type filter-value", "type")]
        [InlineData("new --list source --type filter-value", "type")]
        [InlineData("new source --type filter-value --list", "type")]
        [InlineData("new source --list --type filter-value ", "type")]
        [InlineData("new --type filter-value --list source", "type")]
        [InlineData("new list source --baseline filter-value", "baseline")]
        [InlineData("new --list source --baseline filter-value", "baseline")]
        [InlineData("new source --baseline filter-value --list", "baseline")]
        [InlineData("new source --list --baseline filter-value ", "baseline")]
        [InlineData("new --baseline filter-value --list source", "baseline")]
        [InlineData("new -l source --author filter-value", "author")]
        [InlineData("new source --author filter-value -l", "author")]
        [InlineData("new source -l --author filter-value ", "author")]
        [InlineData("new --author filter-value -l source", "author")]
        [InlineData("new -l source --language filter-value", "language")]
        [InlineData("new source --language filter-value -l", "language")]
        [InlineData("new source -l --language filter-value ", "language")]
        [InlineData("new --language filter-value -l source", "language")]
        [InlineData("new -l source -lang filter-value", "language")]
        [InlineData("new source -lang filter-value -l", "language")]
        [InlineData("new source -l -lang filter-value ", "language")]
        [InlineData("new -lang filter-value -l source", "language")]
        [InlineData("new -l source --tag filter-value", "tag")]
        [InlineData("new source --tag filter-value -l", "tag")]
        [InlineData("new source -l --tag filter-value ", "tag")]
        [InlineData("new --tag filter-value -l source", "tag")]
        [InlineData("new -l source --type filter-value", "type")]
        [InlineData("new source --type filter-value -l", "type")]
        [InlineData("new source -l --type filter-value ", "type")]
        [InlineData("new --type filter-value -l source", "type")]
        [InlineData("new -l source --baseline filter-value", "baseline")]
        [InlineData("new source --baseline filter-value -l", "baseline")]
        [InlineData("new source -l --baseline filter-value ", "baseline")]
        [InlineData("new --baseline filter-value -l source", "baseline")]
        public void List_CanParseFilterOption(string command, string expectedFilter)
        {
            FilterOptionDefinition expectedDef = _stringToFilterDefMap[expectedFilter];

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);
            ListCommandArgs args = new ListCommandArgs((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Single(args.AppliedFilters);
            Assert.Contains("filter-value", args.GetFilterValue(expectedDef));
            Assert.Equal("source", args.ListNameCriteria);
        }

        [Theory]
        [InlineData("new list --author filter-value", "author")]
        [InlineData("new --list --author filter-value", "author")]
        [InlineData("new --author filter-value --list", "author")]
        [InlineData("new list  --language filter-value", "language")]
        [InlineData("new --list  --language filter-value", "language")]
        [InlineData("new  --language filter-value --list", "language")]
        [InlineData("new list  --tag filter-value", "tag")]
        [InlineData("new --list  --tag filter-value", "tag")]
        [InlineData("new  --tag filter-value --list", "tag")]
        [InlineData("new list  --type filter-value", "type")]
        [InlineData("new --list  --type filter-value", "type")]
        [InlineData("new  --type filter-value --list", "type")]
        [InlineData("new list  --baseline filter-value", "baseline")]
        [InlineData("new --list  --baseline filter-value", "baseline")]
        [InlineData("new  --baseline filter-value --list", "baseline")]
        [InlineData("new -l --author filter-value", "author")]
        [InlineData("new --author filter-value -l", "author")]
        [InlineData("new -l  --language filter-value", "language")]
        [InlineData("new  --language filter-value -l", "language")]
        [InlineData("new -l  --tag filter-value", "tag")]
        [InlineData("new  --tag filter-value -l", "tag")]
        [InlineData("new -l  --type filter-value", "type")]
        [InlineData("new  --type filter-value -l", "type")]
        [InlineData("new -l  --baseline filter-value", "baseline")]
        [InlineData("new  --baseline filter-value -l", "baseline")]
        public void List_CanParseFilterOptionWithoutMainCriteria(string command, string expectedFilter)
        {
            FilterOptionDefinition expectedDef = _stringToFilterDefMap[expectedFilter];

            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);
            ListCommandArgs args = new ListCommandArgs((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Single(args.AppliedFilters);
            Assert.Contains("filter-value", args.GetFilterValue(expectedDef));
            Assert.Null(args.ListNameCriteria);
        }

        [Theory]
        [InlineData("new --list cr1 cr2")]
        [InlineData("new list cr1 cr2")]
        public void List_CannotParseMultipleArgs(string command)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal("Unrecognized command or argument 'cr2'", parseResult.Errors.First().Message);
        }

        [Fact]
        public void List_CannotParseArgsAtNewLevel()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new smth list");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal("Unrecognized command or argument(s): 'smth'", parseResult.Errors.First().Message);
        }

        [Theory]
        [InlineData("new --author filter-value list source", "--author")]
        [InlineData("new --type filter-value list source", "--type")]
        [InlineData("new --tag filter-value list source", "--tag")]
        [InlineData("new --language filter-value list source", "--language")]
        [InlineData("new -lang filter-value list source", "-lang")]
        public void List_CannotParseOptionsAtNewLevel(string command, string expectedFilter)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal($"Unrecognized command or argument(s): '{expectedFilter}','filter-value'", parseResult.Errors.First().Message);
        }

        [Fact]
        public void List_Legacy_CannotParseArgsAtBothLevels()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new smth --list smth-else");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal("Unrecognized command or argument(s): 'smth'", parseResult.Errors.First().Message);
        }

        [Theory]
        [InlineData("new --list --columns-all")]
        [InlineData("new --columns-all --list")]
        [InlineData("new list --columns-all")]
        public void List_CanParseColumnsAll(string command)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);

            ListCommandArgs args = new ListCommandArgs((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.DisplayAllColumns);
        }

        [Theory]
        [InlineData("new list --columns author type", new[] { "author", "type" })]
        [InlineData("new list --columns author --columns type", new[] { "author", "type" })]
        //https://github.com/dotnet/command-line-api/issues/1503
        //[InlineData("new list --columns author,type", new[] { "author", "type" })]
        //[InlineData("new list --columns author, type --columns tag", new[] { "author", "type", "tag" })]
        [InlineData("new --list --columns author --columns type", new[] { "author", "type" })]
        //[InlineData("new --list --columns author,type", new[] { "author", "type" })]
        public void List_CanParseColumns(string command, string[] expectedColumns)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);

            ListCommandArgs args = new ListCommandArgs((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.False(args.DisplayAllColumns);
            Assert.NotEmpty(args.ColumnsToDisplay);
            Assert.Equal(expectedColumns.Length, args.ColumnsToDisplay?.Count);
            foreach (var column in expectedColumns)
            {
                Assert.Contains(column, args.ColumnsToDisplay);
            }
        }

        [Theory]
        [InlineData("new --list --columns c1 --columns c2")]
        [InlineData("new list --columns c1 c2")]
        public void List_CannotParseUnknownColumns(string command)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);

            Assert.NotEmpty(parseResult.Errors);
            Assert.Contains("Argument 'c1' not recognized. Must be one of:", parseResult.Errors.First().Message);
        }

        [Theory]
        [InlineData("new --interactive list source", "'--interactive'")]
        [InlineData("new --interactive --list source", "'--interactive'")]
        [InlineData("new foo bar --list source", "'foo'|'bar'")]
        [InlineData("new foo bar list source", "'foo'|'bar'")]
        public void List_HandleParseErrors(string command, string expectedInvalidTokens)
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse(command);
            var errorMessages = parseResult.Errors.Select(error => error.Message);

            var expectedInvalidTokenSets = expectedInvalidTokens.Split("|");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal(expectedInvalidTokenSets.Length, parseResult.Errors.Count);
            foreach (var tokenSet in expectedInvalidTokenSets)
            {
                Assert.True(errorMessages.Contains($"Unrecognized command or argument(s): {tokenSet}") || errorMessages.Contains($"Unrecognized command or argument {tokenSet}"));
            }
        }
    }
}
