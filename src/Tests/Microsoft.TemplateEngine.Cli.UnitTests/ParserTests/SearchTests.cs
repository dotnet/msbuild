// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class SearchTests : BaseTest
    {
        private static Dictionary<string, FilterOptionDefinition> _stringToFilterDefMap = new()
        {
            { "package", FilterOptionDefinition.PackageFilter },
            { "author", FilterOptionDefinition.AuthorFilter },
            { "type", FilterOptionDefinition.TypeFilter },
            { "language", FilterOptionDefinition.LanguageFilter },
            { "tag", FilterOptionDefinition.TagFilter },
            { "baseline", FilterOptionDefinition.BaselineFilter },
        };

        [Theory]
        [InlineData("new search source --author filter-value", "author")]
        [InlineData("new --search source --author filter-value", "author")]
        [InlineData("new source --author filter-value --search", "author")]
        [InlineData("new source --search --author filter-value ", "author")]
        [InlineData("new --author filter-value --search source", "author")]
        [InlineData("new search source --package filter-value", "package")]
        [InlineData("new --search source --package filter-value", "package")]
        [InlineData("new source --package filter-value --search", "package")]
        [InlineData("new source --search --package filter-value ", "package")]
        [InlineData("new --package filter-value --search source", "package")]
        [InlineData("new search source --language filter-value", "language")]
        [InlineData("new --search source --language filter-value", "language")]
        [InlineData("new source --language filter-value --search", "language")]
        [InlineData("new source --search --language filter-value ", "language")]
        [InlineData("new --language filter-value --search source", "language")]
        [InlineData("new search source -lang filter-value", "language")]
        [InlineData("new --search source -lang filter-value", "language")]
        [InlineData("new source -lang filter-value --search", "language")]
        [InlineData("new source --search -lang filter-value ", "language")]
        [InlineData("new -lang filter-value --search source", "language")]
        [InlineData("new search source --tag filter-value", "tag")]
        [InlineData("new --search source --tag filter-value", "tag")]
        [InlineData("new source --tag filter-value --search", "tag")]
        [InlineData("new source --search --tag filter-value ", "tag")]
        [InlineData("new --tag filter-value --search source", "tag")]
        [InlineData("new search source --type filter-value", "type")]
        [InlineData("new --search source --type filter-value", "type")]
        [InlineData("new source --type filter-value --search", "type")]
        [InlineData("new source --search --type filter-value ", "type")]
        [InlineData("new --type filter-value --search source", "type")]
        [InlineData("new search source --baseline filter-value", "baseline")]
        [InlineData("new --search source --baseline filter-value", "baseline")]
        [InlineData("new source --baseline filter-value --search", "baseline")]
        [InlineData("new source --search --baseline filter-value ", "baseline")]
        [InlineData("new --baseline filter-value --search source", "baseline")]
        public void Search_CanParseFilterOption(string command, string expectedFilter)
        {
            FilterOptionDefinition expectedDef = _stringToFilterDefMap[expectedFilter];

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);
            SearchCommandArgs args = new((BaseSearchCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Single(args.AppliedFilters);
            Assert.Contains("filter-value", args.GetFilterValue(expectedDef));
            Assert.Equal("source", args.SearchNameCriteria);
        }

        [Theory]
        [InlineData("new search --author filter-value", "author")]
        [InlineData("new --search --author filter-value", "author")]
        [InlineData("new --author filter-value --search", "author")]
        [InlineData("new search  --package filter-value", "package")]
        [InlineData("new --search  --package filter-value", "package")]
        [InlineData("new  --package filter-value --search", "package")]
        [InlineData("new search  --language filter-value", "language")]
        [InlineData("new --search  --language filter-value", "language")]
        [InlineData("new  --language filter-value --search", "language")]
        [InlineData("new search  --tag filter-value", "tag")]
        [InlineData("new --search  --tag filter-value", "tag")]
        [InlineData("new  --tag filter-value --search", "tag")]
        [InlineData("new search  --type filter-value", "type")]
        [InlineData("new --search  --type filter-value", "type")]
        [InlineData("new  --type filter-value --search", "type")]
        [InlineData("new search  --baseline filter-value", "baseline")]
        [InlineData("new --search  --baseline filter-value", "baseline")]
        [InlineData("new  --baseline filter-value --search", "baseline")]
        public void Search_CanParseFilterOptionWithoutMainCriteria(string command, string expectedFilter)
        {
            FilterOptionDefinition expectedDef = _stringToFilterDefMap[expectedFilter];

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);
            SearchCommandArgs args = new((BaseSearchCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Single(args.AppliedFilters);
            Assert.Contains("filter-value", args.GetFilterValue(expectedDef));
            Assert.Null(args.SearchNameCriteria);
        }

        [Theory]
        [InlineData("new --search cr1 cr2")]
        [InlineData("new search cr1 cr2")]
        public void Search_CannotParseMultipleArgs(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal("Unrecognized command or argument 'cr2'.", parseResult.Errors[0].Message);
        }

        [Fact]
        public void Search_CannotParseArgsAtNewLevel()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            ParseResult parseResult = myCommand.Parse("new smth search");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal("Unrecognized command or argument(s): 'smth'.", parseResult.Errors[0].Message);
        }

        [Theory]
        [InlineData("new --author filter-value search source", "--author")]
        [InlineData("new --package filter-value search source", "--package")]
        [InlineData("new --type filter-value search source", "--type")]
        [InlineData("new --tag filter-value search source", "--tag")]
        [InlineData("new --language filter-value search source", "--language")]
        [InlineData("new -lang filter-value search source", "-lang")]
        public void Search_CannotParseOptionsAtNewLevel(string command, string expectedFilter)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal($"Unrecognized command or argument(s): '{expectedFilter}','filter-value'.", parseResult.Errors[0].Message);
        }

        [Fact]
        public void Search_Legacy_CannotParseArgsAtBothLevels()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new smth --search smth-else");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal("Unrecognized command or argument(s): 'smth'.", parseResult.Errors[0].Message);
        }

        [Theory]
        [InlineData("new --interactive search source", "'--interactive'")]
        [InlineData("new --interactive --search source", "'--interactive'")]
        [InlineData("new foo bar --search source", "'foo'|'bar'")]
        [InlineData("new foo bar search source", "'foo'|'bar'")]
        public void Search_HandleParseErrors(string command, string expectedInvalidTokens)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);
            IEnumerable<string> errorMessages = parseResult.Errors.Select(error => error.Message);

            string[] expectedInvalidTokenSets = expectedInvalidTokens.Split("|");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal(expectedInvalidTokenSets.Length, parseResult.Errors.Count);
            foreach (string tokenSet in expectedInvalidTokenSets)
            {
                Assert.True(errorMessages.Contains($"Unrecognized command or argument(s): {tokenSet}.") || errorMessages.Contains($"Unrecognized command or argument {tokenSet}."));
            }
        }

        [Theory]
        [InlineData("new --search --columns-all")]
        [InlineData("new --columns-all --search")]
        [InlineData("new search --columns-all")]
        public void Search_CanParseColumnsAll(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            SearchCommandArgs args = new((BaseSearchCommand)parseResult.CommandResult.Command, parseResult);

            Assert.True(args.DisplayAllColumns);
        }

        [Theory]
        //https://github.com/dotnet/command-line-api/issues/1503
        [InlineData("new search --columns author type", new[] { "author", "type" })]
        [InlineData("new search --columns author --columns type", new[] { "author", "type" })]
        //[InlineData("new search --columns author,type", new[] { "author", "type" })]
        //[InlineData("new search --columns author, type --columns tag", new[] { "author", "type", "tag" })]
        [InlineData("new --search --columns author --columns type", new[] { "author", "type" })]
        //[InlineData("new --search --columns author,type", new[] { "author", "type" })]
        public void Search_CanParseColumns(string command, string[] expectedColumns)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            SearchCommandArgs args = new((BaseSearchCommand)parseResult.CommandResult.Command, parseResult);

            Assert.NotNull(args.ColumnsToDisplay);
            Assert.False(args.DisplayAllColumns);
            Assert.NotEmpty(args.ColumnsToDisplay);
            Assert.Equal(expectedColumns.Length, args.ColumnsToDisplay.Count);
            foreach (string column in expectedColumns)
            {
                Assert.Contains(column, args.ColumnsToDisplay!);
            }
        }

        [Theory]
        [InlineData("new --search --columns c1 --columns c2")]
        [InlineData("new search --columns c1 c2")]
        public void Search_CannotParseUnknownColumns(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.NotEmpty(parseResult.Errors);
            Assert.Contains("Argument 'c1' not recognized. Must be one of:", parseResult.Errors[0].Message);
        }

        [Fact]
        public void CommandExampleCanShowParentCommandsBeyondNew()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            CliCommand rootCommand = new("dotnet")
            {
                myCommand
            };

            ParseResult parseResult = rootCommand.Parse("dotnet new search template");
            Assert.Equal("dotnet new search my-template", Example.For<NewCommand>(parseResult).WithSubcommand<SearchCommand>().WithArgument(BaseSearchCommand.NameArgument, "my-template"));
        }

        [Fact]
        public void CommandExampleShowsMandatoryArg()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            CliCommand rootCommand = new("dotnet")
            {
                myCommand
            };

            ParseResult parseResult = rootCommand.Parse("dotnet new search template");
            Assert.Equal("dotnet new search [<template-name>]", Example.For<NewCommand>(parseResult).WithSubcommand<SearchCommand>().WithArgument(BaseSearchCommand.NameArgument));
        }

        [Fact]
        public void CommandExampleShowsOptionalArgWhenOptionsAreGiven()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            CliCommand rootCommand = new("dotnet")
            {
                myCommand
            };

            ParseResult parseResult = rootCommand.Parse("dotnet new search template");
            Assert.Equal("dotnet new search [<template-name>] --author Microsoft", Example.For<NewCommand>(parseResult).WithSubcommand<SearchCommand>().WithArgument(BaseSearchCommand.NameArgument).WithOption(SharedOptionsFactory.CreateAuthorOption(), "Microsoft"));
        }
    }
}
