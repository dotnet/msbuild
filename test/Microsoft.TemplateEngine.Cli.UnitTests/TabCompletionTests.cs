// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TabCompletionTests
    {
        [Fact]
        public void Instantiate_CanSuggestTemplateOption_StartsWith()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new console --framework net5.0");
            var suggestions = myCommand.GetSuggestions(parseResult, "--l").ToArray();

            Assert.Contains("--langVersion", suggestions);
        }

        [Fact]
        public void RootCommand_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new ");
            var result = myCommand.GetSuggestions(parseResult).ToArray();

            Assert.Contains("install", result);
            Assert.DoesNotContain("--install", result);
            Assert.Contains("console", result);
        }

        [Fact]
        public void Install_GetAllSuggestions()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new install ");
            var result = parseResult.CommandResult.Command.GetSuggestions(parseResult).ToArray();

            Assert.Contains("--nuget-source", result);
        }

        [Fact]
        public void Install_GetSuggestionsAfterInteractive()
        {
            ITemplateEngineHost host = TestHost.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(includeTestTemplates: false));
            var myCommand = NewCommandFactory.Create("new", host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new install --interactive ");
            var result = parseResult.CommandResult.Command.GetSuggestions(parseResult).ToArray();

            Assert.Contains("--nuget-source", result);
        }
    }
}
