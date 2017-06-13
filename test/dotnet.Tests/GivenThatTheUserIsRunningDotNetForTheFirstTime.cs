// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Tests
{
    public class GivenThatTheUserIsRunningDotNetForTheFirstTime : TestBase
    {
        private static CommandResult _firstDotnetNonVerbUseCommandResult;
        private static CommandResult _firstDotnetVerbUseCommandResult;
        private static DirectoryInfo _nugetFallbackFolder;

        static GivenThatTheUserIsRunningDotNetForTheFirstTime()
        {
            var testDirectory = TestAssets.CreateTestDirectory("Dotnet_first_time_experience_tests");
            var testNuGetHome = Path.Combine(testDirectory.FullName, "nuget_home");

            var command = new DotnetCommand()
                .WithWorkingDirectory(testDirectory);
            command.Environment["HOME"] = testNuGetHome;
            command.Environment["USERPROFILE"] = testNuGetHome;
            command.Environment["APPDATA"] = testNuGetHome;
            command.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "";
            command.Environment["SkipInvalidConfigurations"] = "true";

            _firstDotnetNonVerbUseCommandResult = command.ExecuteWithCapturedOutput("--info");
            _firstDotnetVerbUseCommandResult = command.ExecuteWithCapturedOutput("new --debug:ephemeral-hive");

            _nugetFallbackFolder = new DirectoryInfo(Path.Combine(testNuGetHome, ".dotnet", "NuGetFallbackFolder"));
        }

        [Fact]
        public void UsingDotnetForTheFirstTimeSucceeds()
        {
            _firstDotnetVerbUseCommandResult
                .Should()
                .Pass();
        }

        [Fact]
        public void UsingDotnetForTheFirstTimeWithNonVerbsDoesNotPrintEula()
        {
            string firstTimeNonVerbUseMessage = Cli.Utils.LocalizableStrings.DotNetCommandLineTools;

            _firstDotnetNonVerbUseCommandResult.StdOut
                .Should()
                .StartWith(firstTimeNonVerbUseMessage);
        }

        [Fact]
        public void ItShowsTheAppropriateMessageToTheUser()
        {
            string firstTimeUseWelcomeMessage = NormalizeLineEndings(Configurer.LocalizableStrings.FirstTimeWelcomeMessage);
            // normalizing line endings as what is used in the resources may
            // does not necessarily match how the command stdout capturing logic 
            // handles newlines.
            NormalizeLineEndings(_firstDotnetVerbUseCommandResult.StdOut)
                .Should().Contain(firstTimeUseWelcomeMessage)
                     .And.NotContain("Restore completed in");
        }

        [Fact]
        public void ItCreatesASentinelFileUnderTheNuGetCacheFolder()
        {
            _nugetFallbackFolder
                .Should()
                .HaveFile($"{GetDotnetVersion()}.dotnetSentinel");
    	}

        [Fact]
        public void ItRestoresTheNuGetPackagesToTheNuGetCacheFolder()
        {
            List<string> expectedDirectories = new List<string>()
            {
                "microsoft.netcore.app",
                "microsoft.netcore.platforms",
                "netstandard.library",
                "microsoft.aspnetcore.diagnostics",
                "microsoft.aspnetcore.mvc",
                "microsoft.aspnetcore.routing",
                "microsoft.aspnetcore.server.iisintegration",
                "microsoft.aspnetcore.server.kestrel",
                "microsoft.aspnetcore.staticfiles",
                "microsoft.extensions.configuration.environmentvariables",
                "microsoft.extensions.configuration.json",
                "microsoft.extensions.logging",
                "microsoft.extensions.logging.console",
                "microsoft.extensions.logging.debug",
                "microsoft.extensions.options.configurationextensions",
                //BrowserLink has been temporarily disabled until https://github.com/dotnet/templating/issues/644 is resolved
                //"microsoft.visualstudio.web.browserlink",
            };

            _nugetFallbackFolder
                .Should()
                .HaveDirectories(expectedDirectories);
        }

        private string GetDotnetVersion()
        {
            return new DotnetCommand().ExecuteWithCapturedOutput("--version").StdOut
                .TrimEnd(Environment.NewLine.ToCharArray());
        }

        private static string NormalizeLineEndings(string s)
        {
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
