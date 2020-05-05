// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class VSTestArgumentConverterTests
    {
        [Theory]
        [MemberData(nameof(DataSource.ArgTestCases), MemberType = typeof(DataSource))]
        public void ConvertArgsShouldConvertValidArgsIntoVSTestParsableArgs(string input, string expectedString)
        {
            string[] args = input.Split(' ');
            string[] expectedArgs = expectedString.Split(' ');

            // Act
            var convertedArgs = new VSTestArgumentConverter().Convert(args, out var ignoredArgs);

            convertedArgs.Should().BeEquivalentTo(convertedArgs);
            ignoredArgs.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(DataSource.VerbosityTestCases), MemberType = typeof(DataSource))]
        public void ConvertArgshouldConvertsVerbosityArgsIntoVSTestParsableArgs(string input, string expectedString)
        {
            string[] args = input.Split(' ');
            string[] expectedArgs = expectedString.Split(' ');

            // Act
            var convertedArgs = new VSTestArgumentConverter().Convert(args, out var ignoredArgs);

            convertedArgs.Should().BeEquivalentTo(convertedArgs);
            ignoredArgs.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(DataSource.IgnoredArgTestCases), MemberType = typeof(DataSource))]
        public void ConvertArgsShouldIgnoreKnownArgsWhileConvertingArgsIntoVSTestParsableArgs(string input, string expectedArgString, string expIgnoredArgString)
        {
            string[] args = input.Split(' ');
            string[] expectedArgs = expectedArgString.Split(' ');
            string[] expIgnoredArgs = expIgnoredArgString.Split(' ');

            // Act
            var convertedArgs = new VSTestArgumentConverter().Convert(args, out var ignoredArgs);

            convertedArgs.Should().BeEquivalentTo(convertedArgs);
            Assert.Equal(expIgnoredArgs, ignoredArgs);
        }

        [Fact]
        public void ConvertArgsThrowsWhenWeTryToParseInlineSettings()
        {
            string[] args = "sometest.dll -s test.settings -- inlineSetting=1".Split(" ");

            // Act
            new VSTestArgumentConverter().Invoking(i => i.Convert(args, out _))
                .ShouldThrow<ArgumentException>()
                .WithMessage("Inline settings should not be passed to Convert.");
        }

        public static class DataSource
        {
            public static IEnumerable<object[]> ArgTestCases { get; } = new List<object[]>
            {
                new object[] { "-h", "--help" },
                new object[] { "sometest.dll -s test.settings", "sometest.dll --settings:test.settings" },
                new object[] { "sometest.dll -t", "sometest.dll --listtests" },
                new object[] { "sometest.dll --list-tests", "sometest.dll --listtests" },
                new object[] { "sometest.dll --filter", "sometest.dll --testcasefilter" },
                new object[] { "sometest.dll -l trx", "sometest.dll --logger:trx" },
                new object[] { "sometest.dll -a c:\adapterpath\temp", "sometest.dll --testadapterpath:c:\adapterpath\temp" },
                new object[] { "sometest.dll --test-adapter-path c:\adapterpath\temp", "sometest.dll --testadapterpath:c:\adapterpath\temp" },
                new object[] { "sometest.dll -f net451", "sometest.dll --framework:net451" },
                new object[] { @"sometest.dll -d c:\temp\log.txt", @"sometest.dll --diag:c:\temp\log.txt" },
                new object[] { @"sometest.dll --results-directory c:\temp\", @"sometest.dll --resultsdirectory:c:\temp\" },
                new object[] { @"sometest.dll -s testsettings -t -a c:\path -f net451 -d log.txt --results-directory c:\temp\", @"sometest.dll --settings:testsettings --listtests --testadapterpath:c:\path --framework:net451 --diag:log.txt --resultsdirectory:c:\temp\" },
                new object[] { @"sometest.dll -s:testsettings -t -a:c:\path -f:net451 -d:log.txt --results-directory:c:\temp\", @"sometest.dll --settings:testsettings --listtests --testadapterpath:c:\path --framework:net451 --diag:log.txt --resultsdirectory:c:\temp\" },
                new object[] { @"sometest.dll --settings testsettings -t --test-adapter-path c:\path --framework net451 --diag log.txt --results-directory c:\temp\", @"sometest.dll --settings:testsettings --listtests --testadapterpath:c:\path --framework:net451 --diag:log.txt --resultsdirectory:c:\temp\" }
            };

            public static IEnumerable<object[]> VerbosityTestCases { get; } = new List<object[]>
            {
                new object[] { "sometest.dll -v q", "sometest.dll --logger:console;verbosity=quiet" },
                new object[] { "sometest.dll -v m", "sometest.dll --logger:console;verbosity=minimal" },
                new object[] { "sometest.dll -v n", "sometest.dll --logger:console;verbosity=normal" },
                new object[] { "sometest.dll -v d", "sometest.dll --logger:console;verbosity=detailed" },
                new object[] { "sometest.dll -v diag", "sometest.dll --logger:console;verbosity=diagnostic" },

                new object[] { "sometest.dll --verbosity q", "sometest.dll --logger:console;verbosity=quiet" },
                new object[] { "sometest.dll -v:q", "sometest.dll --logger:console;verbosity=quiet" },
                new object[] { "sometest.dll --verbosity:q", "sometest.dll --logger:console;verbosity=quiet" },
            };

            public static IEnumerable<object[]> IgnoredArgTestCases { get; } = new List<object[]>
            {
                new object[] { "sometest.dll -c Debug", "sometest.dll", "-c Debug" },
                new object[] { "sometest.dll --configuration Debug", "sometest.dll", "--configuration Debug" },
                new object[] { "sometest.dll --runtime win10-x64", "sometest.dll", "--runtime win10-x64" },
                new object[] { "sometest.dll -o c:\temp2", "sometest.dll", "-o c:\temp2" },
                new object[] { "sometest.dll --output c:\temp2", "sometest.dll", "--output c:\temp2" },
                new object[] { "sometest.dll --interactive", "sometest.dll", "--interactive" },
                new object[] { "sometest.dll --no-build", "sometest.dll", "--no-build" },
                new object[] { "sometest.dll --no-restore", "sometest.dll", "--no-restore" },

                new object[] {
                    @"sometest.dll -s testsettings -t -a c:\path -f net451 -d log.txt --configuration Debug --output C:\foo --runtime win10-x64 --results-directory c:\temp\ --no-build --no-restore --interactive",
                    @"sometest.dll --settings:testsettings --listtests --testadapterpath:c:\path --framework:net451 --diag:log.txt --resultsdirectory:c:\temp\",
                    @"--configuration Debug --output C:\foo --runtime win10-x64 --no-build --no-restore --interactive"
                },
                new object[] {
                    @"sometest.dll --settings testsettings --list-tests -a c:\path -f net451 --diag log.txt --collect coverage --blame --configuration Debug --output C:\foo --runtime win10-x64 --results-directory c:\temp\ --no-build --no-restore --interactive",
                    @"sometest.dll --settings:testsettings --listtests --testadapterpath:c:\path --framework:net451 --diag:log.txt --collect:coverage --blame --resultsdirectory:c:\temp\",
                    @"--configuration Debug --output C:\foo --runtime win10-x64 --no-build --no-restore --interactive"
                }
            };
        }

    }
}
