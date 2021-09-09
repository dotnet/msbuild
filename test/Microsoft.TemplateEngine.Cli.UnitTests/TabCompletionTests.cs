// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class TabCompletionTests
    {
        [Fact]
        public void Test()
        {
            using EnvironmentSettingsHelper helper = new EnvironmentSettingsHelper();
            var settings = helper.CreateEnvironment();

            var myCommand = New3Command.CreateCommand("new", settings.Host, new TelemetryLogger(null, false), new New3Callbacks());

            var parseResult = myCommand.Parse("new console --framework net5.0");
            var result = myCommand.GetSuggestions(parseResult, "--l");

            Assert.Contains("--Framework", result);
        }
    }
}
