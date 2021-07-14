// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;

using FluentAssertions;

using Xunit;


namespace Microsoft.DotNet.Cli.Format.Tests
{
    public class GivenDotnetFormatIsInvoked
    {
        [Fact]
        public void WithoutAnyAdditionalArguments()
        {
            var app = new FormatCommand().FromArgs(Array.Empty<string>());
            app.Arugments.Skip(1).ToArray() // We skip the project/solution argument as its path will change
                .ShouldAllBeEquivalentTo(new string[]{
                    "--fix-whitespace",
                    "--fix-style",
                    "--fix-analyzers"
                    });
        }

        [Theory]
        [InlineData("CA1803")]
        [InlineData("CA1803 CA1804 CA1805")]
        public void WithDiagnosticsOption(string diagnostics)
        {
            var app = new FormatCommand().FromArgs(new string[] { "--diagnostics", diagnostics });
            var expectedArgs = new string[]
            {
                "--fix-whitespace",
                "--fix-style",
                "--fix-analyzers",
                "--diagnostics",
                diagnostics
            };
            app.Arugments.Skip(1).ToArray() // We skip the project/solution argument as its path will change
                .ShouldAllBeEquivalentTo(expectedArgs.ToArray());
        }

        [Theory]
        [InlineData("info", "--fix-whitespace --fix-style info --fix-analyzers info")]
        [InlineData("warn", "--fix-whitespace --fix-style warn --fix-analyzers warn")]
        [InlineData("error", "--fix-whitespace --fix-style error --fix-analyzers error")]
        public void WithSeverityOption(string severity, string expected)
        {
            VerifyArguments($"--severity {severity}", expected);
        }

        [Fact]
        public void WithNoRestoreOption()
        {
            VerifyArgumentsWithDefault("--no-restore", "--no-restore");
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file1.cs file2.cs file3.cs")]
        [InlineData(@"path\to\file\file.cs")]
        [InlineData("path/to/file/file.cs")]
        public void WithIncludeOption(string files)
        {
            var app = new FormatCommand().FromArgs(new string[] { "--include", files });
            var expectedArgs = new string[]
            {
                "--include",
                files,
                "--fix-whitespace",
                "--fix-style",
                "--fix-analyzers",
            };
            app.Arugments.Skip(1).ToArray() // We skip the project/solution argument as its path will change
                .ShouldAllBeEquivalentTo(expectedArgs.ToArray());
        }

        [Theory]
        [InlineData("file.cs")]
        [InlineData("file1.cs file2.cs file3.cs")]
        [InlineData(@"path\to\file\file.cs")]
        [InlineData("path/to/file/file.cs")]
        public void WithExcludeOption(string files)
        {
            var app = new FormatCommand().FromArgs(new string[] { "--exclude", files });
            var expectedArgs = new string[]
            {
                "--exclude",
                files,
                "--fix-whitespace",
                "--fix-style",
                "--fix-analyzers",
            };
            app.Arugments.Skip(1).ToArray() // We skip the project/solution argument as its path will change
                .ShouldAllBeEquivalentTo(expectedArgs.ToArray());
        }

        [Fact]
        public void WithIncludeGeneratedOption()
        {
            VerifyArgumentsWithDefault("--include-generated", "--include-generated");
        }

        [Theory]
        [InlineData("--verbosity d", "--verbosity d")]
        [InlineData("--verbosity detailed", "--verbosity detailed")]
        [InlineData("--verbosity diag", "--verbosity diag")]
        [InlineData("--verbosity diagnostic", "--verbosity diagnostic")]
        [InlineData("--verbosity m", "--verbosity m")]
        [InlineData("--verbosity minimal", "--verbosity minimal")]
        [InlineData("--verbosity n", "--verbosity n")]
        [InlineData("--verbosity normal", "--verbosity normal")]
        [InlineData("--verbosity q", "--verbosity q")]
        [InlineData("--verbosity quiet", "--verbosity quiet")]
        [InlineData("-v d", "--verbosity d")]
        [InlineData("-v detailed", "--verbosity detailed")]
        [InlineData("-v diag", "--verbosity diag")]
        [InlineData("-v diagnostic", "--verbosity diagnostic")]
        [InlineData("-v m", "--verbosity m")]
        [InlineData("-v minimal", "--verbosity minimal")]
        [InlineData("-v n", "--verbosity n")]
        [InlineData("-v normal", "--verbosity normal")]
        [InlineData("-v q", "--verbosity q")]
        [InlineData("-v quiet", "--verbosity quiet")]
        public void WithVerbosityOption(string arguments, string expected)
        {
            VerifyArgumentsWithDefault(arguments, expected);
        }

        [Theory]
        [InlineData("--binarylog", "--binarylog")]
        [InlineData("--binarylog <binary-log-path>", "--binarylog <binary-log-path>")]
        public void WithBinarylogOption(string arguments, string expected)
        {
            VerifyArgumentsWithDefault(arguments, expected);
        }

        [Theory]
        [InlineData("--report", "--report")]
        [InlineData("--report <report-path>", "--report <report-path>")]
        public void WithReportOption(string arguments, string expected)
        {
            VerifyArgumentsWithDefault(arguments, expected);
        }

        [Theory]
        [InlineData("-?")]
        [InlineData("-h")]
        [InlineData("--help")]
        public void WithHelpOption(string arguments)
        {
            try
            {
                var app = new FormatCommand().FromArgs(arguments.Split(" "));
            }
            catch (HelpException helpException)
            {
                helpException.Message.ShouldAllBeEquivalentTo("");
            }
        }

        private static void VerifyArgumentsWithDefault(string arguments, string expected)
        {
            var app = new FormatCommand().FromArgs(arguments.Split(" "));
            var expectedArgs = expected.Split(" ").ToList();
            expectedArgs.AddRange(
                new string[]{
                    "--fix-whitespace",
                    "--fix-style",
                    "--fix-analyzers"
                    });
            app.Arugments.Skip(1).ToArray() // We skip the project/solution argument as its path will change
                .ShouldAllBeEquivalentTo(expectedArgs.ToArray());
        }

        private static void VerifyArguments(string arguments, string expected)
        {
            var app = new FormatCommand().FromArgs(arguments.Split(" "));
            var expectedArgs = expected.Split(" ").ToList();
            app.Arugments.Skip(1).ToArray() // We skip the project/solution argument as its path will change
                .ShouldAllBeEquivalentTo(expectedArgs.ToArray());
        }
    }
}
