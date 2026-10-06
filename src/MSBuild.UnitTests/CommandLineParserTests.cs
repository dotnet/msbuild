// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.CommandLine.Experimental;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.CommandLine.UnitTests
{
    public class CommandLineParserTests
    {
        private readonly ITestOutputHelper _output;

        public CommandLineParserTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ParseReturnsInstance()
        {
            CommandLineParser parser = new();
            CommandLineSwitchesAccessor result = parser.Parse(["/targets:targets.txt"]);

            result.Targets.ShouldNotBeNull();
            result.Targets.ShouldBe(["targets.txt"]);
            result.UnrecognizedArguments.ShouldBeEmpty();
            result.UnexpandedResponseFileArguments.ShouldBeEmpty();
        }

        [Fact]
        public void ParseThrowsException()
        {
            CommandLineParser parser = new();

            Should.Throw<CommandLineSwitchException>(() =>
            {
                parser.Parse(["tempproject.proj", "tempproject.proj"]);
            });
        }

        [Fact]
        public void UnknownSwitchesThrowByDefault()
        {
            CommandLineParser parser = new();

            Should.Throw<CommandLineSwitchException>(() => parser.Parse(["-sdk-only", "-noconsolelogger"]));
            Should.Throw<CommandLineSwitchException>(() => parser.Parse(["-sdk-only"], new CommandLineParsingOptions()));

            CommandLineSwitches switches = new();
            parser.GatherCommandLineSwitches(["-sdk-only"], switches);
            Should.Throw<CommandLineSwitchException>(() => switches.ThrowErrors());
        }

        [Fact]
        public void UnknownSwitchesRetainOriginalTokensAndDoNotStopParsing()
        {
            IReadOnlyList<string> arguments = [
                "-sdk-only", "-p:Configuration=Release", "\"--application:quoted value\"",
                "/sdk-only:1", "-sdk-only", "--mt:false", "-noconsolelogger", "project.proj"
            ];
            CommandLineParser parser = new();

            CommandLineSwitchesAccessor result = parser.Parse(arguments, TokenOnlyOptions());

            result.UnrecognizedArguments.ShouldBe(["-sdk-only", "\"--application:quoted value\"", "/sdk-only:1", "-sdk-only"]);
            result.Property.ShouldBe(["Configuration=Release"]);
            result.MultiThreaded.ShouldBe(["false"]);
            result.NoConsoleLogger.ShouldBe(true);
            result.Project.ShouldBe(["project.proj"]);
        }

        [Theory]
        [InlineData("-p:")]
        [InlineData("-help:unexpected")]
        [InlineData("first.proj", "second.proj")]
        public void AllowingUnknownSwitchesStillThrowsForMalformedKnownArguments(string argument, string? secondArgument = null)
        {
            List<string> arguments = ["-sdk-only", argument];
            if (secondArgument is not null)
            {
                arguments.Add(secondArgument);
            }

            CommandLineParser parser = new();
            Should.Throw<CommandLineSwitchException>(() => parser.Parse(arguments, TokenOnlyOptions()));
        }

        [Fact]
        public void DisabledResponseFilesReturnUnexpandedTokensWithoutReadingThem()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var responseFile = env.CreateFile("arguments.rsp", "-p:FromResponseFile=true -help:unexpected");
            string responseArgument = $"@\"{responseFile.Path}\"";
            CommandLineParser parser = new();

            CommandLineSwitchesAccessor result = parser.Parse(
                [responseArgument, "@missing.rsp", "@", responseArgument, "-p:FromCommandLine=true"],
                new CommandLineParsingOptions { ReadResponseFiles = false, ReadLoggingArgumentsFromEnvironment = false });

            result.Property.ShouldBe(["FromCommandLine=true"]);
            result.UnexpandedResponseFileArguments.ShouldBe([responseArgument, "@missing.rsp", "@", responseArgument]);
            result.UnrecognizedArguments.ShouldBeEmpty();
            parser.IncludedResponseFiles.ShouldBeEmpty();
        }

        [Fact]
        public void ResponseFilesAreReadByDefaultIncludingNestedFiles()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var nestedFile = env.CreateFile("nested.rsp", "-p:Nested=true -v:quiet");
            var responseFile = env.CreateFile("arguments.rsp", $"@\"{nestedFile.Path}\" -p:Outer=true");
            CommandLineParser parser = new();

            CommandLineSwitchesAccessor result = parser.Parse(
                ["-noautoresponse", $"@\"{responseFile.Path}\"", "-v:minimal"]);

            result.Property.ShouldBe(["Nested=true", "Outer=true"]);
            result.Verbosity.ShouldBe(["quiet", "minimal"]);
            result.UnexpandedResponseFileArguments.ShouldBeEmpty();
            parser.IncludedResponseFiles.ShouldBe([responseFile.Path, nestedFile.Path]);
        }

        [Fact]
        public void UnknownSwitchesInNestedResponseFilesAreCollected()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var nestedFile = env.CreateFile("nested.rsp", "\"--application:quoted value\" -p:Nested=true");
            var responseFile = env.CreateFile("arguments.rsp", $"-outer-only @\"{nestedFile.Path}\" -outer-only");
            CommandLineParser parser = new();

            CommandLineSwitchesAccessor result = parser.Parse(
                ["-noautoresponse", "-before-only", $"@\"{responseFile.Path}\"", "-after-only"],
                new CommandLineParsingOptions { ThrowOnUnknownSwitches = false });

            result.UnrecognizedArguments.ShouldBe([
                "-before-only", "-outer-only", "\"--application:quoted value\"", "-outer-only", "-after-only"
            ]);
            result.Property.ShouldBe(["Nested=true"]);
        }

        [Theory]
        [InlineData("-sdk-only")]
        [InlineData("-p:")]
        public void ResponseFileErrorsStillThrowByDefault(string contents)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var responseFile = env.CreateFile("arguments.rsp", contents);
            CommandLineParser parser = new();

            Should.Throw<CommandLineSwitchException>(() => parser.Parse(["-noautoresponse", $"@\"{responseFile.Path}\""]));
        }

        [Fact]
        public void AllowingUnknownSwitchesStillRejectsRepeatedResponseFiles()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            var responseFile = env.CreateFile("arguments.rsp", "-sdk-only");
            CommandLineParser parser = new();

            Should.Throw<InitializationException>(() => parser.Parse(
                ["-noautoresponse", $"@\"{responseFile.Path}\"", $"@\"{responseFile.Path}\""],
                new CommandLineParsingOptions { ThrowOnUnknownSwitches = false }));
        }

        [Fact]
        public void AllowingUnknownSwitchesStillRejectsMissingResponseFiles()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            string missingFile = Path.Combine(env.DefaultTestDirectory.Path, "missing.rsp");
            CommandLineParser parser = new();

            Should.Throw<InitializationException>(() => parser.Parse(
                ["-noautoresponse", $"@\"{missingFile}\""],
                new CommandLineParsingOptions { ThrowOnUnknownSwitches = false }));
        }

        [Fact]
        public void DisablingResponseFilesAlsoDisablesAutomaticResponseFiles()
        {
            CommandLineParser parser = new();
            string automaticFile = Path.Combine(Path.GetDirectoryName(typeof(MSBuildApp).Assembly.Location)!, "MSBuild.rsp");
            File.Exists(automaticFile).ShouldBeTrue();

            parser.Parse([]);
            parser.IncludedResponseFiles.ShouldContain(automaticFile);

            parser.Parse([], new CommandLineParsingOptions { ReadResponseFiles = false });
            parser.IncludedResponseFiles.ShouldBeEmpty();
        }

        [Fact]
        public void LoggingArgumentsFromEnvironmentCanBeExcluded()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable(Traits.MSBuildLoggingArgsEnvVarName, "-bl:environment.binlog -check:all");
            CommandLineParser parser = new();

            CommandLineSwitchesAccessor defaultResult = parser.Parse(["-noautoresponse"]);
            defaultResult.BinaryLogger.ShouldBe(["environment.binlog"]);
            defaultResult.Check.ShouldBe(["all"]);

            CommandLineSwitchesAccessor tokenOnlyResult = parser.Parse(["-bl:explicit.binlog"], TokenOnlyOptions());
            tokenOnlyResult.BinaryLogger.ShouldBe(["explicit.binlog"]);
            tokenOnlyResult.Check.ShouldBeNull();
        }

        [Fact]
        public void ParserReuseDoesNotRetainOptionsOrChangeEarlierResults()
        {
            CommandLineParser parser = new();
            CommandLineParsingOptions options = TokenOnlyOptions();
            CommandLineSwitchesAccessor first = parser.Parse(["-first-only", "@first.rsp"], options);
            CommandLineSwitchesAccessor second = parser.Parse(["-second-only", "@second.rsp"], options);

            options.ThrowOnUnknownSwitches = true;
            Should.Throw<CommandLineSwitchException>(() => parser.Parse(["-sdk-only"], options));
            Should.Throw<CommandLineSwitchException>(() => parser.Parse(["-sdk-only"]));

            first.UnrecognizedArguments.ShouldBe(["-first-only"]);
            first.UnexpandedResponseFileArguments.ShouldBe(["@first.rsp"]);
            second.UnrecognizedArguments.ShouldBe(["-second-only"]);
            second.UnexpandedResponseFileArguments.ShouldBe(["@second.rsp"]);
        }

        [Fact]
        public void EnumerableOverloadEnumeratesInputOnlyOnce()
        {
            int enumerations = 0;
            IEnumerable<string> Arguments()
            {
                (++enumerations).ShouldBe(1);
                yield return "-noautoresponse";
                yield return "-targets:targets.txt";
            }

            CommandLineParser parser = new();
            parser.Parse(Arguments()).Targets.ShouldBe(["targets.txt"]);
            enumerations.ShouldBe(1);
        }

        [Fact]
        public void NullArgumentsAreRejected()
        {
            CommandLineParser parser = new();
            Should.Throw<ArgumentNullException>(() => parser.Parse((IReadOnlyList<string>)null!));
            Should.Throw<ArgumentNullException>(() => parser.Parse((IEnumerable<string>)null!));
        }

        private static CommandLineParsingOptions TokenOnlyOptions() => new()
        {
            ThrowOnUnknownSwitches = false,
            ReadResponseFiles = false,
            ReadLoggingArgumentsFromEnvironment = false
        };
    }
}
