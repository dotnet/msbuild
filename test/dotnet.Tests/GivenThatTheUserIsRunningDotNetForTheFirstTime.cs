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

namespace Microsoft.DotNet.Tests
{
	public class GivenThatTheUserIsRunningDotNetForTheFirstTime : TestBase
    {
        private static CommandResult _firstDotnetNonVerbUseCommandResult;
        private static CommandResult _firstDotnetVerbUseCommandResult;
        private static DirectoryInfo _nugetCacheFolder;

        static GivenThatTheUserIsRunningDotNetForTheFirstTime()
        {
            var testDirectory = TestAssetsManager.CreateTestDirectory("Dotnet_first_time_experience_tests");
            var testNugetCache = Path.Combine(testDirectory.Path, "nuget_cache");

            var command = new DotnetCommand()
                .WithWorkingDirectory(testDirectory.Path);
            command.Environment["NUGET_PACKAGES"] = testNugetCache;
            command.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "";
            command.Environment["SkipInvalidConfigurations"] = "true";

            _firstDotnetNonVerbUseCommandResult = command.ExecuteWithCapturedOutput("--info");
            _firstDotnetVerbUseCommandResult = command.ExecuteWithCapturedOutput("new");

            _nugetCacheFolder = new DirectoryInfo(testNugetCache);
        }        

        [Fact]
        public void Using_dotnet_for_the_first_time_succeeds()
        {
            _firstDotnetVerbUseCommandResult
                .Should()
                .Pass();
        }

        [Fact]
        public void Using_dotnet_for_the_first_time_with_non_verbs_doesnt_print_eula()
        {
            const string firstTimeNonVerbUseMessage = @".NET Command Line Tools";

            _firstDotnetNonVerbUseCommandResult.StdOut
                .Should()
                .StartWith(firstTimeNonVerbUseMessage);
        }

        [Fact]
        public void It_shows_the_appropriate_message_to_the_user()
        {
            string firstTimeUseWelcomeMessage = NormalizeLineEndings(@"Welcome to .NET Core!
---------------------
Learn more about .NET Core @ https://aka.ms/dotnet-docs. Use dotnet --help to see available commands or go to https://aka.ms/dotnet-cli-docs.
Telemetry
--------------
The .NET Core tools collect usage data in order to improve your experience. The data is anonymous and does not include commandline arguments. The data is collected by Microsoft and shared with the community.
You can opt out of telemetry by setting a DOTNET_CLI_TELEMETRY_OPTOUT environment variable to 1 using your favorite shell.
You can read more about .NET Core tools telemetry @ https://aka.ms/dotnet-cli-telemetry.
Configuring...
-------------------
A command is running to initially populate your local package cache, to improve restore speed and enable offline access. This command will take up to a minute to complete and will only happen once.");

            // normalizing line endings as git is occasionally replacing line endings in this file causing this test to fail
            NormalizeLineEndings(_firstDotnetVerbUseCommandResult.StdOut)
                .Should().StartWith(firstTimeUseWelcomeMessage)
                     .And.NotContain("Restore completed in");
        }

    	[Fact]
    	public void It_restores_the_nuget_packages_to_the_nuget_cache_folder()
    	{
            _nugetCacheFolder
                .Should()
                .HaveFile($"{GetDotnetVersion()}.dotnetSentinel");            
    	}

        [Fact]
        public void It_creates_a_sentinel_file_under_the_nuget_cache_folder()
        {
            _nugetCacheFolder
                .Should()
                .HaveDirectory("microsoft.netcore.app");
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