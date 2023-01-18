// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public partial class TabCompletionTests : BaseTest
    {
        [Fact]
        public Task RootCommand_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            Command myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = ParserFactory.CreateParser(myCommand).Parse("new ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task RootCommand_GetStartsWtihSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            Command myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new c");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Install_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            Command myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new install ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Uninstall_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            Command myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new uninstall ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Update_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            Command myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new update ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task List_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            Command myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new list ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Search_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            Command myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new search ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Create_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            Command myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new create ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task TemplateCommand_GetAllSuggestions()
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            Command myCommand = NewCommandFactory.Create("new", _ => host);

            ParseResult parseResult = myCommand.Parse("new console ");
            System.CommandLine.Completions.CompletionItem[] result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }
    }
}
