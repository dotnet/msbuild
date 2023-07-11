// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [UsesVerify]
    public partial class TabCompletionTests : BaseTest
    {
        [Fact]
        public Task RootCommand_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = ParserFactory.CreateParser(myCommand).Parse("new ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task RootCommand_GetStartsWtihSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new c");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Install_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new install ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Uninstall_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new uninstall ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Update_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new update ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task List_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new list ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Search_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new search ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Create_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new create ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task TemplateCommand_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new console ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task DetailsCommand_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            CliCommand myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new details ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }
    }
}
