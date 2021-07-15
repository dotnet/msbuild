// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;
using Xunit.Abstractions;

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

        [Fact]
        public void GivenNewCommandItDisplaysCompletions()
        {
            var expected = new[] {
                "--columns",
                "--dry-run",
                "--force",
                "--help",
                "--install",
                "--language",
                "--list",
                "--interactive",
                "--name",
                "--nuget-source",
                "--output",
                "--type",
                "--uninstall",
                "-?",
                "-h",
                "-i",
                "-l",
                "-lang",
                "-n",
                "-o",
                "-u",
                "/?",
                "/h",
                "--update-check",
                "--update-apply"
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
                "--algorithm",
                "--allow-untrusted-root",
                "--configfile",
                "--owners",
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
    }
}
