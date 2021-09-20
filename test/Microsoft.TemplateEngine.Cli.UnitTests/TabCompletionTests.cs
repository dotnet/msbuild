// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TabCompletionTests
    {
#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact (Skip = "This test won't pass. It needs environment with console template installed.")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void BasicTest()
        {
            using EnvironmentSettingsHelper helper = new EnvironmentSettingsHelper();
            var settings = helper.CreateEnvironment();

            var myCommand = NewCommandFactory.Create("new", settings.Host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new console --framework net5.0");
            var result = myCommand.GetSuggestions(parseResult, "--l");

            Assert.Contains("--Framework", result);
        }

        [Fact]
        public void RootCommand()
        {
            using EnvironmentSettingsHelper helper = new EnvironmentSettingsHelper();
            var settings = helper.CreateEnvironment(
                virtualize: true,
                additionalComponents: BuiltInTemplatePackagesProviderFactory.Components);
            var myCommand = NewCommandFactory.Create("new", settings.Host, new TelemetryLogger(null, false), new NewCommandCallbacks());

            var parseResult = myCommand.Parse("new ");
            var result = myCommand.GetSuggestions(parseResult).ToArray();

            Assert.Contains("install", result);
            Assert.DoesNotContain("--install", result);
            Assert.Contains("console", result);
        }
    }
}
