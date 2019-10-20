// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Collections.Generic;


namespace Microsoft.DotNet.InstallationScript.Tests
{
    public class GivenThatIWantToInstallTheSdkFromAScript : TestBase
    {
        private static string InstallationScriptTestsJsonFile = Path.Combine(RepoDirectoriesProvider.RepoRoot, "TestAssets", "InstallationScriptTests", "InstallationScriptTests.json").ToString();

        [Fact]
        public void WhenJsonFileIsPassedToInstallScripts()
        {
            var args = new List<string> { "-dryrun", "-jsonfile", InstallationScriptTestsJsonFile };

            var commandResult = CreateInstallCommand(args)
                            .CaptureStdOut()
                            .CaptureStdErr()
                            .Execute();

            commandResult.Should().Pass();
            commandResult.Should().NotHaveStdOutContaining("dryrun");
            commandResult.Should().NotHaveStdOutContaining("jsonfile");
            commandResult.Should().HaveStdOutContaining("Repeatable invocation:");
            commandResult.Should().HaveStdOutContaining("1.0.0-beta.19463.3");
        }

        [Theory]
        [InlineData("-nopath", "")]
        [InlineData("-verbose", "")]
        [InlineData("-nocdn", "")]
        [InlineData("-azurefeed", "https://dotnetcli.azureedge.net/dotnet")]
        [InlineData("-uncachedfeed", "https://dotnetcli.blob.core.windows.net/dotnet")]
        public void WhenVariousParametersArePassedToInstallScripts(string parameter, string value)
        {
            var args = new List<string> { "-dryrun", parameter };
            if (!string.IsNullOrEmpty(value))
            {
                args.Add(value);
            }

            var commandResult = CreateInstallCommand(args)
                            .CaptureStdOut()
                            .CaptureStdErr()
                            .Execute();

            //  Standard 'dryrun' criterium
            commandResult.Should().Pass();
            commandResult.Should().NotHaveStdOutContaining("dryrun");
            commandResult.Should().HaveStdOutContaining("Repeatable invocation:");

            //  Non-dynamic input parameters should always be on the ouput line
            commandResult.Should().HaveStdOutContainingIgnoreCase(parameter);
        }

        [Theory]
        [InlineData("-runtime", "dotnet")]
        [InlineData("-runtime", "aspnetcore")]
        [InlineData("-sharedruntime", "dotnet")]
        public void WhenRuntimeParametersArePassedToInstallScripts(string runtime, string runtimeType)
        {
            var args = new List<string> { "-dryrun", runtime };
            if (!runtime.Equals("-sharedruntime", StringComparison.OrdinalIgnoreCase))
            {
                args.Add(runtimeType);
            }

            var commandResult = CreateInstallCommand(args)
                            .CaptureStdOut()
                            .CaptureStdErr()
                            .Execute();

            //  Standard 'dryrun' criterium
            commandResult.Should().Pass();
            commandResult.Should().NotHaveStdOutContaining("dryrun");
            commandResult.Should().HaveStdOutContaining("Repeatable invocation:");

            //  Runtime should resolve to the correct 'type'
            commandResult.Should().HaveStdOutContainingIgnoreCase("-runtime");
            commandResult.Should().HaveStdOutContainingIgnoreCase(runtimeType);
        }

        [Theory]
        [InlineData("1.0", "dotnet")]
        [InlineData("1.1", "dotnet")]
        [InlineData("2.0", "dotnet")]
        [InlineData("2.2", "dotnet")]
        [InlineData("Current", "dotnet")]
        [InlineData("LTS", "dotnet")]
        [InlineData("master", "dotnet")]
        [InlineData("release/2.1", "dotnet")]
        [InlineData("release/2.2", "dotnet")]
        [InlineData("release/3.0", "dotnet")]
        [InlineData("Current", "aspnetcore")]
        [InlineData("LTS", "aspnetcore")]
        [InlineData("master", "aspnetcore")]
        [InlineData("release/2.1", "aspnetcore")]
        [InlineData("release/2.2", "aspnetcore")]
        public void WhenChannelResolvesToASpecificRuntimeVersion(string channel, string runtimeType)
        {
            var args = new string[] { "-dryrun", "-channel", channel, "-runtime", runtimeType };

            var commandResult = CreateInstallCommand(args)
                            .CaptureStdOut()
                            .CaptureStdErr()
                            .Execute();

            //  Standard 'dryrun' criterium
            commandResult.Should().Pass();
            commandResult.Should().NotHaveStdOutContaining("dryrun");
            commandResult.Should().HaveStdOutContaining("Repeatable invocation:");

            //  Channel should be translated to a specific Runtime version
            commandResult.Should().HaveStdOutContainingIgnoreCase("-version");
        }

        [Theory]
        [InlineData("1.0")]
        [InlineData("1.1")]
        [InlineData("2.0")]
        [InlineData("2.1")]
        [InlineData("Current")]
        [InlineData("LTS")]
        [InlineData("master")]
        [InlineData("release/1.0.0")]
        [InlineData("release/2.0.0")]
        [InlineData("release/2.0.2")]
        [InlineData("release/2.1.1xx")]
        [InlineData("release/2.1.2xx")]
        [InlineData("release/2.1.3xx")]
        [InlineData("release/2.1.4xx")]
        [InlineData("release/2.1.401")]
        [InlineData("release/2.1.5xx")]
        [InlineData("release/2.1.502")]
        [InlineData("release/2.1.6xx")]
        [InlineData("release/2.1.7xx")]
        [InlineData("release/2.1.8xx")]
        [InlineData("release/2.2.1xx")]
        [InlineData("release/2.2.2xx")]
        [InlineData("release/2.2.3xx")]
        [InlineData("release/2.2.4xx")]
        [InlineData("release/3.0.1xx")]
        public void WhenChannelResolvesToASpecificSDKVersion(string channel)
        {
            var args = new string[] { "-dryrun", "-channel", channel };

            var commandResult = CreateInstallCommand(args)
                            .CaptureStdOut()
                            .CaptureStdErr()
                            .Execute();

            //  Standard 'dryrun' criterium
            commandResult.Should().Pass();
            commandResult.Should().NotHaveStdOutContaining("dryrun");
            commandResult.Should().HaveStdOutContaining("Repeatable invocation:");

            //  Channel should be translated to a specific SDK version
            commandResult.Should().HaveStdOutContainingIgnoreCase("-version");
        }

        private static Command CreateInstallCommand(IEnumerable<string> args)
        {
            var path = "";
            var finalArgs = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = "powershell.exe";
                finalArgs = "-ExecutionPolicy Bypass -NoProfile -NoLogo -Command \"" + Path.Combine(RepoDirectoriesProvider.RepoRoot, "scripts", "obtain", "dotnet-install.ps1") + " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args) + "\"";
            }
            else
            {
                path = Path.Combine(RepoDirectoriesProvider.RepoRoot, "scripts", "obtain", "dotnet-install.sh");
                finalArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args);
            }

            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = finalArgs,
                UseShellExecute = false
            };

            var _process = new Process
            {
                StartInfo = psi
            };

            return new Command(_process);
        }

    }
}
