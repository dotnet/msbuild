// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.TestHelper;
using VerifyXunit;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests.ParserTests
{
    [UsesVerify]
    public partial class TabCompletionTests : IClassFixture<VerifyFixture>
    {
        private readonly VerifyFixture _verifySettings;

        public TabCompletionTests (VerifyFixture verifySettings)
        {
            _verifySettings = verifySettings;
        }

        [Fact]
        public Task RootCommand_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new ");
            var result = parseResult.GetSuggestions().ToArray();

            return Verifier.Verify(result, _verifySettings.Settings);
        }

        [Fact]
        public Task Install_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new install ");
            var result = parseResult.GetSuggestions().ToArray();

            return Verifier.Verify(result, _verifySettings.Settings);
        }

        [Fact]
        public Task Uninstall_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new uninstall ");
            var result = parseResult.GetSuggestions().ToArray();

            return Verifier.Verify(result, _verifySettings.Settings);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact (Skip = "https://github.com/dotnet/command-line-api/blob/main/src/System.CommandLine/Parsing/ParseResultExtensions.cs#L285-L289; the tab completion also contains the results from parent command")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public Task Update_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new update ");
            var result = parseResult.GetSuggestions().ToArray();

            return Verifier.Verify(result, _verifySettings.Settings);
        }

        [Fact]
        public Task List_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new list ");
            var result = parseResult.GetSuggestions().ToArray();

            return Verifier.Verify(result, _verifySettings.Settings);
        }

        [Fact]
        public Task Search_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new search ");
            var result = parseResult.GetSuggestions().ToArray();

            return Verifier.Verify(result, _verifySettings.Settings);
        }

        [Fact]
        public Task Create_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new create ");
            var result = parseResult.GetSuggestions().ToArray();

            return Verifier.Verify(result, _verifySettings.Settings);
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/command-line-api/blob/main/src/System.CommandLine/Parsing/ParseResultExtensions.cs#L285-L289; the tab completion also contains the results from parent command")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public Task TemplateCommand_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new console ");
            var result = parseResult.GetSuggestions().ToArray();

            return Verifier.Verify(result, _verifySettings.Settings);
        }
    }
}
