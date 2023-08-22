// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tests.Commands
{
    public class CompleteCommandTests : SdkTest
    {
        public CompleteCommandTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void GivenOnlyDotnetItSuggestsTopLevelCommandsAndOptions()
        {
            var expected = new[] {
                "--diagnostics",
                "--help",
                "--info",
                "--list-runtimes",
                "--list-sdks",
                "--version",
                "-?",
                "-d",
                "-h",
                "/?",
                "/h",
                "add",
                "build",
                "build-server",
                "clean",
                "format",
                "sdk",
                "fsi",
                "help",
                "list",
                "msbuild",
                "new",
                "nuget",
                "pack",
                "publish",
                "remove",
                "restore",
                "run",
                "sln",
                "store",
                "test",
                "tool",
                "vstest",
                "workload"
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet " }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenASlashItSuggestsTopLevelOptions()
        {
            var expected = new[] {
                "--diagnostics",
                "--help",
                "--info",
                "--list-runtimes",
                "--list-sdks",
                "--version",
                "-?",
                "-d",
                "-h",
                "build-server", // These should be removed when completion is based on "starts with" rather than "contains".
                                // See https://github.com/dotnet/cli/issues/8958.
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet -" }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        // this test in helix errors accessing the template hive  but this test doesn't work with the ephemeral hive
        [WindowsOnlyFact]
        public void GivenNewCommandItDisplaysCompletions()
        {
            var expected = new[] {
                "--help",
                "-?",
                "-h",
                "/?",
                "/h",
                "install",
                "list",
                "search",
                "uninstall",
                "update"
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet new " }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Contain(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenNuGetCommandItDisplaysCompletions()
        {
            var expected = new[] {
                "--help",
                "--verbosity",
                "--version",
                "-?",
                "-h",
                "-v",
                "/?",
                "/h",
                "delete",
                "locals",
                "push",
                "verify",
                "trust",
                "sign"
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet nuget " }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenNuGetDeleteCommandItDisplaysCompletions()
        {
            var expected = new[] {
                "--api-key",
                "--force-english-output",
                "--help",
                "--no-service-endpoint",
                "--non-interactive",
                "--source",
                "--interactive",
                "-?",
                "-h",
                "-k",
                "-s",
                "/?",
                "/h",
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet nuget delete " }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenNuGetLocalsCommandItDisplaysCompletions()
        {
            var expected = new[] {
                "--clear",
                "--force-english-output",
                "--help",
                "--list",
                "-?",
                "-c",
                "-h",
                "-l",
                "/?",
                "/h",
                "all",
                "global-packages",
                "http-cache",
                "temp",
                "plugins-cache"
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet nuget locals " }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenNuGetPushCommandItDisplaysCompletions()
        {
            var expected = new[] {
                "--api-key",
                "--disable-buffering",
                "--force-english-output",
                "--help",
                "--no-service-endpoint",
                "--no-symbols",
                "--skip-duplicate",
                "--source",
                "--symbol-api-key",
                "--symbol-source",
                "--timeout",
                "--interactive",
                "-?",
                "-d",
                "-h",
                "-k",
                "-n",
                "-s",
                "-sk",
                "-ss",
                "-t",
                "/?",
                "/h",
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet nuget push " }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenNuGetVerifyCommandItDisplaysCompletions()
        {
            var expected = new[] {
                "--all",
                "--certificate-fingerprint",
                "--verbosity",
                "--help",
                "-v",
                "-?",
                "-h",
                "/?",
                "/h",
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet nuget verify " }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenNuGetTrustCommandItDisplaysCompletions()
        {
            var expected = new[] {
                "--configfile",
                "--verbosity",
                "--help",
                "-v",
                "-?",
                "-h",
                "/?",
                "/h",
                "author",
                "certificate",
                "list",
                "remove",
                "repository",
                "source",
                "sync"
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet nuget trust " }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenNuGetSignCommandItDisplaysCompletions()
        {
            var expected = new[] {
                "--certificate-fingerprint",
                "--certificate-path",
                "--certificate-store-name",
                "--certificate-store-location",
                "--certificate-subject-name",
                "--certificate-password",
                "--hash-algorithm",
                "--timestamper",
                "--timestamp-hash-algorithm",
                "--verbosity",
                "--output",
                "--overwrite",
                "-o",
                "--help",
                "-v",
                "-?",
                "-h",
                "/?",
                "/h"
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(new[] { "dotnet nuget sign " }, reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenDotnetAddPackWithPosition()
        {
            var expected = new[] {
                "package"
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(GetArguments("dotnet add pack$ abc"), reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        [Fact]
        public void GivenDotnetToolInWithPosition()
        {
            var expected = new[] {
                "install",
                "uninstall",
            };

            var reporter = new BufferedReporter();
            CompleteCommand.RunWithReporter(GetArguments("dotnet tool in$ abc"), reporter).Should().Be(0);
            reporter.Lines.OrderBy(c => c).Should().Equal(expected.OrderBy(c => c));
        }

        /// <summary>
        /// Converts command annotated with dollar sign($) into string array with "--position" option pointing at dollar sign location.
        /// </summary>
        private string[] GetArguments(string command)
        {
            var indexOfDollar = command.IndexOf("$");
            if (indexOfDollar == -1)
            {
                throw new ArgumentException("Does not contain $", nameof(command));
            }
            return new[] { command.Replace("$", ""), "--position", indexOfDollar.ToString() };
        }
    }
}
