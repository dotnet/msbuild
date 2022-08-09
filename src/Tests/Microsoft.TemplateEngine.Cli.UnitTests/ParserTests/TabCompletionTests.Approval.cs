// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.TestHelper;
using VerifyXunit;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public partial class TabCompletionTests
    {
        [Fact]
        public Task RootCommand_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host);

            var parseResult = ParserFactory.CreateParser(myCommand).Parse("new ");
            var result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task RootCommand_GetStartsWtihSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host);

            var parseResult = myCommand.Parse("new c");
            var result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Install_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host);

            var parseResult = myCommand.Parse("new install ");
            var result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Uninstall_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host);

            var parseResult = myCommand.Parse("new uninstall ");
            var result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Update_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host);

            var parseResult = myCommand.Parse("new update ");
            var result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task List_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host);

            var parseResult = myCommand.Parse("new list ");
            var result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Search_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host);

            var parseResult = myCommand.Parse("new search ");
            var result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task Create_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host);

            var parseResult = myCommand.Parse("new create ");
            var result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }

        [Fact]
        public Task TemplateCommand_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", _ => host);

            var parseResult = myCommand.Parse("new console ");
            var result = parseResult.GetCompletions().ToArray();

            return Verify(result);
        }
    }
}
