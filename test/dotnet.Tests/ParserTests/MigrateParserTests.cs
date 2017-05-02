// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Migrate;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class MigrateParserTests
    {
        public MigrateParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private readonly ITestOutputHelper output;

        [Fact]
        public void MigrateParserConstructMigrateCommandWithoutError()
        {
            var command = Parser.Instance;

            var result = command.Parse("dotnet migrate --skip-backup -s " +
                                       "-x \"C:\\ConsoleAppOnCore_1\\ConsoleAppOnCore_1.xproj\" " +
                                       "\"C:\\ConsoleAppOnCore_1\\project.json\" " +
                                       "-r \"C:\\report.wfw\" " +
                                       "--format-report-file-json");

            Action a = () => result["dotnet"]["migrate"].Value<MigrateCommandCompose>();
            a.ShouldNotThrow<ParseException>();
        }

        [Fact]
        public void MigrateParseGetResultCorrectlyAsFollowing()
        {
            var command = Parser.Instance;

            var result = command.Parse("dotnet migrate --skip-backup -s " +
                                       "-x \"C:\\ConsoleAppOnCore_1\\ConsoleAppOnCore_1.xproj\" " +
                                       "\"C:\\ConsoleAppOnCore_1\\project.json\" " +
                                       "-r \"C:\\report.wfw\" " +
                                       "--format-report-file-json");

            result["dotnet"]["migrate"]["skip-backup"].Value<bool>().Should().BeTrue();
            result["dotnet"]["migrate"]["skip-project-references"].Value<bool>().Should().BeTrue();
            result["dotnet"]["migrate"]["format-report-file-json"].Value<bool>().Should().BeTrue();
            result["dotnet"]["migrate"]["xproj-file"].Value<string>().Should().Be("C:\\ConsoleAppOnCore_1\\ConsoleAppOnCore_1.xproj");
            result["dotnet"]["migrate"]["report-file"].Value<string>().Should().Be("C:\\report.wfw");
            result["dotnet"]["migrate"].Arguments.Contains("C:\\ConsoleAppOnCore_1\\project.json").Should().BeTrue();
        }
    }
}