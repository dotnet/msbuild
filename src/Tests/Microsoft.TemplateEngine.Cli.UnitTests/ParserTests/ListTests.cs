// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    public class ListTests : BaseTest
    {
        private static readonly Dictionary<string, FilterOptionDefinition> _stringToFilterDefMap = new()
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

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);
            ListCommandArgs args = new((BaseListCommand)parseResult.CommandResult.Command, parseResult);

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

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);
            ListCommandArgs args = new((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.Single(args.AppliedFilters);
            Assert.Contains("filter-value", args.GetFilterValue(expectedDef));
            Assert.Null(args.ListNameCriteria);
        }

        [Theory]
        [InlineData("new --list cr1 cr2")]
        [InlineData("new list cr1 cr2")]
        public void List_CannotParseMultipleArgs(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal("Unrecognized command or argument 'cr2'.", parseResult.Errors[0].Message);
        }

        [Fact]
        public void List_CannotParseArgsAtNewLevel()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new smth list");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal("Unrecognized command or argument(s): 'smth'.", parseResult.Errors[0].Message);
        }

        [Theory]
        [InlineData("new --author filter-value list source", "--author")]
        [InlineData("new --type filter-value list source", "--type")]
        [InlineData("new --tag filter-value list source", "--tag")]
        [InlineData("new --language filter-value list source", "--language")]
        [InlineData("new -lang filter-value list source", "-lang")]
        public void List_CannotParseOptionsAtNewLevel(string command, string expectedFilter)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal($"Unrecognized command or argument(s): '{expectedFilter}','filter-value'.", parseResult.Errors[0].Message);
        }

        [Fact]
        public void List_Legacy_CannotParseArgsAtBothLevels()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new smth --list smth-else");

            Assert.NotEmpty(parseResult.Errors);
            Assert.Equal("Unrecognized command or argument(s): 'smth'.", parseResult.Errors[0].Message);
        }

        [Theory]
        [InlineData("new --list --columns-all")]
        [InlineData("new --columns-all --list")]
        [InlineData("new list --columns-all")]
        public void List_CanParseColumnsAll(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            ListCommandArgs args = new((BaseListCommand)parseResult.CommandResult.Command, parseResult);

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
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            ListCommandArgs args = new((BaseListCommand)parseResult.CommandResult.Command, parseResult);

            Assert.False(args.DisplayAllColumns);
            Assert.NotNull(args.ColumnsToDisplay);
            Assert.NotEmpty(args.ColumnsToDisplay);
            Assert.Equal(expectedColumns.Length, args.ColumnsToDisplay.Count);
            foreach (string column in expectedColumns)
            {
                Assert.Contains(column, args.ColumnsToDisplay!);
            }
        }

        [Theory]
        [InlineData("new --list --columns c1 --columns c2")]
        [InlineData("new list --columns c1 c2")]
        public void List_CannotParseUnknownColumns(string command)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse(command);

            Assert.NotEmpty(parseResult.Errors);
            Assert.Contains("Argument 'c1' not recognized. Must be one of:", parseResult.Errors[0].Message);
        }

        [Theory]
        [InlineData("new --interactive list source", "'--interactive'")]
        [InlineData("new --interactive --list source", "'--interactive'")]
        [InlineData("new foo bar --list source", "'foo'|'bar'")]
        [InlineData("new foo bar list source", "'foo'|'bar'")]
        public void List_HandleParseErrors(string command, string expectedInvalidTokens)
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

        [Fact]
        public void CommandExampleCanShowParentCommandsBeyondNew()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            NewCommand myCommand = (NewCommand)NewCommandFactory.Create("new", _ => host);
            CliCommand rootCommand = new("dotnet")
            {
                myCommand
            };

            ParseResult parseResult = rootCommand.Parse("dotnet new list");
            Assert.Equal("dotnet new list", Example.For<NewCommand>(parseResult).WithSubcommand<ListCommand>());
        }
    }
}
